using System.Text.Json;
using Gravia.Application.Logging;
using Gravia.Application.Settings;
using Gravia.Application.Settings.EventHandlers;
using Gravia.Application.Settings.Events;
using Gravia.Application.Settings.Services;
using Gravia.Application.Storage;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Consts;
using Gravia.Domain.Settings.Entities;
using Gravia.Domain.Settings.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Application.Tests.Settings;

[Collection("Domain events")]
public sealed class SettingsStorageTests
{
    [Fact]
    public async Task StartupLoadsNamedSettingsAndEventsSaveToTheirOwningStore()
    {
        var appData = new MemoryDataStore { Json = """{"gravia.language":"en","absent.plugin":{"amount":12}}""" };
        var projectData = new MemoryDataStore();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddKeyedSingleton<IDataStore>(DataStoreKey.Application, appData);
        services.AddApplicationServices();
        services.AddSingleton<ILogJournal>(new UnusedLogJournal());
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var project = CreateProject(projectData);
        var application = provider.GetRequiredService<ApplicationSettings>();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var hostedService in hostedServices)
            await hostedService.StartAsync(timeout.Token);

        try
        {
            Assert.Same(application, Assert.Single(hostedServices.OfType<ApplicationSettings>()));
            Assert.Equal("en", application.Language.Value);
            Assert.Equal("system", application.Theme.Value);
            Assert.Equal(2, application.Settings.Count);
            Assert.Equal(0, appData.Writes);

            var projectTheme = Text("gravia.theme", "system");
            await project.Register(projectTheme);
            application.Theme.SetValue("dark");
            projectTheme.SetValue("light");
        }
        finally
        {
            foreach (var hostedService in hostedServices.Reverse())
                await hostedService.StopAsync(timeout.Token);
        }

        Assert.Equal("dark", appData.Value("gravia.theme").GetString());
        Assert.Equal("en", appData.Value("gravia.language").GetString());
        Assert.Equal(12, appData.Value("absent.plugin").GetProperty("amount").GetInt32());
        Assert.Equal("light", projectData.Value("gravia.theme").GetString());
        Assert.Equal(1, appData.Writes);
        Assert.Equal(1, projectData.Writes);
    }

    [Fact]
    public async Task ReplacingDefinitionPreservesPendingValueAndIgnoresOldObjectEvents()
    {
        var data = new MemoryDataStore { Json = """{"example.count":3}""" };
        using var storage = CreateProject(data);
        var original = Number("example.count", 1);
        await storage.Register(original);
        Assert.Equal(3, original.Value);
        original.SetValue(7);

        var replacement = Number("example.count", 100);
        await storage.Register(replacement);
        Assert.Equal(7, replacement.Value);
        Assert.Same(replacement, Assert.Single(storage.Settings));
        Assert.Equal(7, data.Value("example.count").GetInt32());

        original.SetValue(99);
        await Save(original);
        Assert.Equal(7, data.Value("example.count").GetInt32());
        replacement.SetValue(8);
        await Save(replacement);
        Assert.Equal(8, data.Value("example.count").GetInt32());
    }

    [Fact]
    public async Task IncompatibleValueResetsToDefaultWithWarningAndPreservesUnknownValues()
    {
        var data = new MemoryDataStore { Json = """{"example.count":"wrong type","unloaded.value":42}""" };
        var logger = new TestLogger<ProjectSettings>();
        using var storage = new ProjectSettings(data, logger);
        var setting = Number("example.count", 10);
        setting.SetValueWithoutNotify(20);

        await storage.Register(setting);

        Assert.Equal(10, setting.Value);
        Assert.Equal(10, data.Value("example.count").GetInt32());
        Assert.Equal(42, data.Value("unloaded.value").GetInt32());
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Levels));
    }

    [Fact]
    public async Task UnregisterPreservesLatestValueForSubsequentRegistration()
    {
        var data = new MemoryDataStore();
        using var storage = CreateProject(data);
        var setting = Number("example.count", 1);
        await storage.Register(setting);
        setting.SetValue(5);

        await storage.Unregister(setting.Id);

        Assert.Empty(storage.Settings);
        Assert.Equal(5, data.Value(setting.Id).GetInt32());
        setting.SetValue(9);
        await Save(setting);
        Assert.Equal(5, data.Value(setting.Id).GetInt32());
        var restored = Number(setting.Id, 1);
        await storage.Register(restored);
        Assert.Equal(5, restored.Value);
    }

    [Fact]
    public async Task MalformedDocumentIsNotOverwrittenAndRegistrationCanBeRetried()
    {
        var data = new MemoryDataStore { Json = "broken" };
        using var storage = CreateProject(data);
        var setting = Number("example.count", 1);

        await Assert.ThrowsAsync<JsonException>(() => storage.Register(setting));
        Assert.Empty(storage.Settings);
        Assert.Equal("broken", data.Json);
        Assert.Equal(0, data.Writes);

        data.Json = """{"example.count":5}""";
        await storage.Register(setting);
        Assert.Equal(5, setting.Value);
    }

    [Fact]
    public async Task FailedSavePreservesFileAndAllowsRetry()
    {
        var data = new MemoryDataStore { Json = """{"example.count":1}""" };
        using var storage = CreateProject(data);
        var setting = Number("example.count", 1);
        await storage.Register(setting);
        setting.SetValue(2);
        data.WriteFailure = new IOException("Cannot save.");

        await Assert.ThrowsAsync<IOException>(() => Save(setting));
        Assert.Equal(1, data.Value(setting.Id).GetInt32());
        Assert.Equal(2, setting.Value);

        data.WriteFailure = null;
        await Save(setting);
        Assert.Equal(2, data.Value(setting.Id).GetInt32());
    }

    [Fact]
    public async Task OneSettingObjectCannotBelongToTwoStores()
    {
        using var first = CreateProject(new MemoryDataStore());
        using var second = CreateProject(new MemoryDataStore());
        var setting = Number("example.count", 1);
        await first.Register(setting);

        await Assert.ThrowsAsync<InvalidOperationException>(() => second.Register(setting));
        Assert.Empty(second.Settings);
        Assert.Same(setting, Assert.Single(first.Settings));
    }

    private static ProjectSettings CreateProject(IDataStore data)
        => new(data, NullLogger<ProjectSettings>.Instance);

    private static IntSetting Number(string id, int value)
        => new(id, new LocalizedText("en", "Count"), value, SettingsUiPaths.Appearance);

    private static StringSetting Text(string id, string value)
        => new(id, new LocalizedText("en", "Text"), value, SettingsUiPaths.Appearance);

    private static Task Save(Gravia.Domain.Settings.Interfaces.ISetting setting)
        => new SettingChangedHandler().Handle(new SettingChanged(setting), CancellationToken.None);

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    private sealed class MemoryDataStore : IDataStore
    {
        public string? Json { get; set; }
        public int Writes { get; private set; }
        public Exception? WriteFailure { get; set; }

        public JsonElement Value(string id)
        {
            using var document = JsonDocument.Parse(Json!);
            return document.RootElement.GetProperty(id).Clone();
        }

        public Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default) where T : class
        {
            Assert.Equal(SettingsStorage.FileName, path);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Json is null ? null : JsonSerializer.Deserialize<T>(Json) ?? throw new JsonException());
        }

        public Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default) where T : class
        {
            Assert.Equal(SettingsStorage.FileName, path);
            cancellationToken.ThrowIfCancellationRequested();
            if (WriteFailure is not null) throw WriteFailure;
            Json = JsonSerializer.Serialize(value);
            Writes++;
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]?> ReadBytesAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
