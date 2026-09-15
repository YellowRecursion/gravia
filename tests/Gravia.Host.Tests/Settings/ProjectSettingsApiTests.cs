using System.Text.Json;
using ActualLab.Fusion;
using Gravia.Application.Projects.Services;
using Gravia.Application.Settings.Services;
using Gravia.Application.Storage;
using Gravia.Domain.Common.Events;
using Gravia.Domain.CommonExceptions;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Consts;
using Gravia.Domain.Settings.Entities;
using Gravia.Host.Fusion.Settings;
using Gravia.Infrastructure.Storage;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Gravia.Host.Tests.Settings;

[Collection("Project lifecycle")]
public sealed class ProjectSettingsApiTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gravia-project-settings-test-" + Guid.NewGuid().ToString("N"));
    private ServiceProvider _provider = null!;
    private ProjectService _projects = null!;
    private ProjectSettingsApi _api = null!;

    public async Task InitializeAsync()
    {
        Assert.Null(ProjectSession.Current);
        Directory.CreateDirectory(_directory);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHostServices();
        services.AddKeyedSingleton<IDataStore>(DataStoreKey.Application,
            (_, _) => new FileDataStore(Path.Combine(_directory, "application")));
        services.AddTransient<INotificationHandler<QueueBarrier>, QueueBarrierHandler>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _projects = _provider.GetRequiredService<ProjectService>();
        _api = _provider.GetRequiredService<ProjectSettingsApi>();
        foreach (var service in _provider.GetServices<IHostedService>()) await service.StartAsync(default);
        await Drain();
    }

    public async Task DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var service in _provider.GetServices<IHostedService>().Reverse()) await service.StopAsync(timeout.Token);
        await _provider.DisposeAsync();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task OpeningProjectInvalidatesEmptyCatalogAndCachedMisses()
    {
        var catalog = await Computed.Capture(() => _api.GetAll());
        var detail = await Computed.Capture(() => _api.Get("shared"));
        Assert.Empty(catalog.Value.Data!);
        Assert.Null(detail.Value.Data);

        await OpenWithSetting("first", "one");

        Assert.Equal(ConsistencyState.Invalidated, catalog.ConsistencyState);
        Assert.Equal(ConsistencyState.Invalidated, detail.ConsistencyState);
        Assert.Equal("one", (await _api.Get("shared")).Data!.Value.GetString());
    }

    [Fact]
    public async Task ValueChangesInvalidateOnlyTheirDetailAndPersistInProjectDirectory()
    {
        var setting = await OpenWithSetting("first", "default");
        await ProjectSession.Current!.Settings.Register(Text("other", "other-default"));
        await Drain();
        var catalog = await Computed.Capture(() => _api.GetAll());
        var detail = await Computed.Capture(() => _api.Get("shared"));
        var other = await Computed.Capture(() => _api.Get("other"));

        Assert.True((await _api.SetValue("shared", JsonSerializer.SerializeToElement("changed"))).IsSuccess);
        await Drain();
        Assert.Equal(ConsistencyState.Invalidated, detail.ConsistencyState);
        Assert.Equal(ConsistencyState.Consistent, catalog.ConsistencyState);
        Assert.Equal(ConsistencyState.Consistent, other.ConsistencyState);
        Assert.Equal("changed", setting.Value);
        Assert.Equal("changed", await StoredValue("shared"));

        detail = await Computed.Capture(() => _api.Get("shared"));
        setting.SetValue("direct-change");
        await Drain();
        Assert.Equal(ConsistencyState.Invalidated, detail.ConsistencyState);
        Assert.Equal("direct-change", await StoredValue("shared"));

        Assert.True((await _api.ResetValue("shared")).IsSuccess);
        await Drain();
        Assert.Equal("default", (await _api.Get("shared")).Data!.Value.GetString());
        Assert.Equal("default", await StoredValue("shared"));
    }

    [Fact]
    public async Task SwitchingProjectsInvalidatesIdenticalIdsAndPreviouslyMissingIds()
    {
        var oldSetting = await OpenWithSetting("first", "one");
        var oldDetail = await Computed.Capture(() => _api.Get("shared"));
        var oldMissing = await Computed.Capture(() => _api.Get("future"));
        var oldCatalog = await Computed.Capture(() => _api.GetAll());

        await OpenWithSetting("second", "two");

        Assert.Equal(ConsistencyState.Invalidated, oldDetail.ConsistencyState);
        Assert.Equal(ConsistencyState.Invalidated, oldMissing.ConsistencyState);
        Assert.Equal(ConsistencyState.Invalidated, oldCatalog.ConsistencyState);
        Assert.Equal("two", (await _api.Get("shared")).Data!.Value.GetString());
        var currentDetail = await Computed.Capture(() => _api.Get("shared"));
        oldSetting.SetValue("stale");
        await Drain();
        Assert.Equal(ConsistencyState.Consistent, currentDetail.ConsistencyState);
        Assert.Equal("two", currentDetail.Value.Data!.Value.GetString());
    }

    [Fact]
    public async Task RegistrationAndRemovalInvalidateCatalogAndDetails()
    {
        await OpenWithSetting("first", "one");
        var catalog = await Computed.Capture(() => _api.GetAll());
        var missing = await Computed.Capture(() => _api.Get("new"));
        var settings = ProjectSession.Current!.Settings;
        await settings.Register(Text("new", "new-value"));
        await Drain();
        Assert.Equal(ConsistencyState.Invalidated, catalog.ConsistencyState);
        Assert.Equal(ConsistencyState.Invalidated, missing.ConsistencyState);
        Assert.Equal("new-value", (await _api.Get("new")).Data!.Value.GetString());

        var detail = await Computed.Capture(() => _api.Get("new"));
        await settings.Unregister("new");
        await Drain();
        Assert.Equal(ConsistencyState.Invalidated, detail.ConsistencyState);
        Assert.Null((await _api.Get("new")).Data);
    }

    [Fact]
    public async Task ApplicationChangesDoNotInvalidateProjectSettingsOrViceVersa()
    {
        await OpenWithSetting("first", "one");
        var app = _provider.GetRequiredService<ApplicationSettingsApi>();
        var appTheme = await Computed.Capture(() => app.Get("gravia.theme"));
        var projectDetail = await Computed.Capture(() => _api.Get("shared"));
        var projectCatalog = await Computed.Capture(() => _api.GetAll());

        await app.SetValue("gravia.theme", JsonSerializer.SerializeToElement("light"));
        await Drain();
        Assert.Equal(ConsistencyState.Consistent, projectDetail.ConsistencyState);
        Assert.Equal(ConsistencyState.Consistent, projectCatalog.ConsistencyState);
        Assert.Equal(ConsistencyState.Invalidated, appTheme.ConsistencyState);

        appTheme = await Computed.Capture(() => app.Get("gravia.theme"));
        await _api.SetValue("shared", JsonSerializer.SerializeToElement("changed"));
        await Drain();
        Assert.Equal(ConsistencyState.Consistent, appTheme.ConsistencyState);
    }

    [Fact]
    public async Task FailedOpenKeepsCachedSettingsOfCurrentProjectConsistent()
    {
        await OpenWithSetting("first", "one");
        var catalog = await Computed.Capture(() => _api.GetAll());
        var detail = await Computed.Capture(() => _api.Get("shared"));

        await Assert.ThrowsAsync<NotFoundException>(() => _projects.OpenAsync(Path.Combine(_directory, "missing")));
        await Drain();

        Assert.Equal(ConsistencyState.Consistent, catalog.ConsistencyState);
        Assert.Equal(ConsistencyState.Consistent, detail.ConsistencyState);
        Assert.Equal("one", detail.Value.Data!.Value.GetString());
    }

    [Fact]
    public async Task SessionRemovalInvalidatesAllSettingsIncludingCachedMisses()
    {
        await OpenWithSetting("first", "one");
        var detail = await Computed.Capture(() => _api.Get("shared"));
        var missing = await Computed.Capture(() => _api.Get("missing"));
        var catalog = await Computed.Capture(() => _api.GetAll());

        await _projects.StopAsync(default);
        await Drain();

        Assert.Equal(ConsistencyState.Invalidated, detail.ConsistencyState);
        Assert.Equal(ConsistencyState.Invalidated, missing.ConsistencyState);
        Assert.Equal(ConsistencyState.Invalidated, catalog.ConsistencyState);
        Assert.Empty((await _api.GetAll()).Data!);
        Assert.Null((await _api.Get("shared")).Data);
    }

    private async Task<StringSetting> OpenWithSetting(string name, string value)
    {
        await _projects.CreateAsync(_directory, name);
        var setting = Text("shared", value);
        await ProjectSession.Current!.Settings.Register(setting);
        await Drain();
        return setting;
    }

    private async Task<string?> StoredValue(string id)
    {
        var values = await ProjectSession.Current!.DataStore.ReadAsync<Dictionary<string, JsonElement>>(SettingsStorage.FileName);
        return values![id].GetString();
    }

    private static StringSetting Text(string id, string value)
        => new(id, new LocalizedText("en", id), value, SettingsUiPaths.Appearance);

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
}
