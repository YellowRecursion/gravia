using System.Text.Json;
using ActualLab.Fusion;
using FluentValidation;
using Gravia.Application.Settings;
using Gravia.Application.Settings.Services;
using Gravia.Application.Logging;
using Gravia.Application.Storage;
using Gravia.Domain.Common.Events;
using Gravia.Domain.CommonExceptions;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Consts;
using Gravia.Domain.Settings.Entities;
using Gravia.Host.Fusion.Settings;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Host.Tests.Settings;

[Collection("Project lifecycle")]
public sealed class ApplicationSettingsApiTests : IAsyncLifetime
{
    private readonly MemoryDataStore _data = new();
    private ServiceProvider _provider = null!;
    private ApplicationSettings _settings = null!;
    private ApplicationSettingsApi _api = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHostServices();
        services.AddKeyedSingleton<IDataStore>(DataStoreKey.Application, _data);
        services.AddTransient<INotificationHandler<QueueBarrier>, QueueBarrierHandler>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions
        { ValidateScopes = true, ValidateOnBuild = true });
        _settings = _provider.GetRequiredService<ApplicationSettings>();
        _api = _provider.GetRequiredService<ApplicationSettingsApi>();
        foreach (var service in _provider.GetServices<IHostedService>())
            await service.StartAsync(default);
        await Drain();
    }

    public async Task DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var service in _provider.GetServices<IHostedService>().Reverse())
            await service.StopAsync(timeout.Token);
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task ValueChangesInvalidateOnlyTheirOwnSettingAndPersist()
    {
        var catalog = await Computed.Capture(() => _api.GetAll());
        var theme = await Computed.Capture(() => _api.Get("gravia.theme"));
        var language = await Computed.Capture(() => _api.Get("gravia.language"));
        var summary = Assert.Single(catalog.Value.Data!, item => item.Id == "gravia.theme");
        Assert.Equal("Тема", summary.Title.Translations["ru"]);
        Assert.NotEmpty(summary.UiPath);
        Assert.DoesNotContain("\"Value\"", JsonSerializer.Serialize(catalog.Value.Data));

        Assert.True((await _api.SetValue("gravia.theme", JsonSerializer.SerializeToElement("dark"))).IsSuccess);
        await Drain();
        Assert.Equal(ConsistencyState.Invalidated, theme.ConsistencyState);
        Assert.Equal(ConsistencyState.Consistent, catalog.ConsistencyState);
        Assert.Equal(ConsistencyState.Consistent, language.ConsistencyState);
        Assert.Equal("dark", (await _api.Get("gravia.theme")).Data!.Value.GetString());
        Assert.Equal("dark", _data.Value("gravia.theme").GetString());
        Assert.Equal("system", theme.Value.Data!.Value.GetString()); // Old DTO is a snapshot.

        theme = await Computed.Capture(() => _api.Get("gravia.theme"));
        _settings.Theme.SetValue("light"); // No RPC call: the same invalidation path must work.
        await Drain();
        Assert.Equal(ConsistencyState.Invalidated, theme.ConsistencyState);
        Assert.Equal("light", _data.Value("gravia.theme").GetString());

        Assert.True((await _api.ResetValue("gravia.theme")).IsSuccess);
        await Drain();
        Assert.Equal("system", (await _api.Get("gravia.theme")).Data!.Value.GetString());
        Assert.Equal("system", _data.Value("gravia.theme").GetString());
    }

    [Fact]
    public async Task RegistrationUpdatesCatalogAndMissingDetailWithoutDeletingStoredValues()
    {
        var catalog = await Computed.Capture(() => _api.GetAll());
        var detail = await Computed.Capture(() => _api.Get("example.count"));
        Assert.Null(detail.Value.Data);
        await _settings.Register(new IntSetting("example.count", new LocalizedText("vi", "Count"),
            10, SettingsUiPaths.Appearance, new LocalizedText("vi", "Description")));
        await Drain();
        Assert.Equal(ConsistencyState.Invalidated, catalog.ConsistencyState);
        Assert.Equal(ConsistencyState.Invalidated, detail.ConsistencyState);
        Assert.Equal(10, (await _api.Get("example.count")).Data!.DefaultValue.GetInt32());

        catalog = await Computed.Capture(() => _api.GetAll());
        await _settings.Register(new IntSetting("example.count", new LocalizedText("en", "Renamed"),
            20, SettingsUiPaths.Appearance));
        await Drain();
        Assert.Equal(ConsistencyState.Invalidated, catalog.ConsistencyState);
        Assert.Equal(10, (await _api.Get("example.count")).Data!.Value.GetInt32());

        catalog = await Computed.Capture(() => _api.GetAll());
        detail = await Computed.Capture(() => _api.Get("example.count"));
        await _settings.Unregister("example.count");
        await Drain();
        Assert.Equal(ConsistencyState.Invalidated, catalog.ConsistencyState);
        Assert.Equal(ConsistencyState.Invalidated, detail.ConsistencyState);
        Assert.Null((await _api.Get("example.count")).Data);
        Assert.Equal(10, _data.Value("example.count").GetInt32());
    }

    [Fact]
    public async Task InvalidInputThrowsValidationExceptionWithoutChangingValue()
    {
        var theme = await Computed.Capture(() => _api.Get("gravia.theme"));
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _api.SetValue("gravia.theme", JsonSerializer.SerializeToElement(123)));
        Assert.Equal("invalid_setting_value", Assert.Single(error.Errors).ErrorCode);
        var notFound = await Assert.ThrowsAsync<NotFoundException>(() => _api.ResetValue("missing"));
        Assert.Equal("not_found", notFound.Code);
        Assert.Empty(notFound.Extra);
        await Drain();
        Assert.Equal(ConsistencyState.Consistent, theme.ConsistencyState);
        Assert.Equal("system", _settings.Theme.Value);
    }

    [Fact]
    public async Task ProjectChangesDoNotInvalidateApplicationSettings()
    {
        using var project = new ProjectSettings(new MemoryDataStore(), NullLogger<ProjectSettings>.Instance);
        var theme = await Computed.Capture(() => _api.Get("gravia.theme"));
        var catalog = await Computed.Capture(() => _api.GetAll());
        var projectTheme = new StringSetting("gravia.theme", new LocalizedText("en", "Theme"),
            "system", SettingsUiPaths.Appearance);
        await project.Register(projectTheme);
        projectTheme.SetValue("dark");
        await Drain();
        Assert.Equal(ConsistencyState.Consistent, theme.ConsistencyState);
        Assert.Equal(ConsistencyState.Consistent, catalog.ConsistencyState);
    }

    [Fact]
    public async Task FailedPersistenceStillInvalidatesTheUiAndLogsTheFailure()
    {
        await _api.SetValue("gravia.theme", JsonSerializer.SerializeToElement("light"));
        await Drain();
        var theme = await Computed.Capture(() => _api.Get("gravia.theme"));
        _data.SaveFailure = new IOException("Test disk is unavailable.");
        try
        {
            await _api.SetValue("gravia.theme", JsonSerializer.SerializeToElement("dark"));
            await Drain();
            Assert.Equal(ConsistencyState.Invalidated, theme.ConsistencyState);
            Assert.Equal("dark", (await _api.Get("gravia.theme")).Data!.Value.GetString());
            Assert.Equal("light", _data.Value("gravia.theme").GetString());
            Assert.Contains(_provider.GetRequiredService<ILogJournal>().Read().Entries,
                entry => entry.Message.Contains("SettingChangedHandler") && entry.Message.Contains("failed"));
        }
        finally { _data.SaveFailure = null; }
    }

    private static async Task Drain()
    {
        var barrier = new QueueBarrier();
        barrier.Raise();
        await barrier.Completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public sealed class QueueBarrier : DomainEvent
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class QueueBarrierHandler : INotificationHandler<QueueBarrier>
    {
        public Task Handle(QueueBarrier notification, CancellationToken cancellationToken)
        {
            notification.Completion.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryDataStore : IDataStore
    {
        private string _json = "{}";
        public Exception? SaveFailure { get; set; }
        public JsonElement Value(string id) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(_json)![id];
        public Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default) where T : class
            => Task.FromResult(JsonSerializer.Deserialize<T>(_json));
        public Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default) where T : class
        {
            if (SaveFailure is not null) throw SaveFailure;
            _json = JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }
        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]?> ReadBytesAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
