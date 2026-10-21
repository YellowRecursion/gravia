using System.Text.Json;
using Gravia.Application.Common.Events;
using FluentValidation;
using Gravia.Application.Logging;
using Gravia.Application.Projects.Interfaces;
using Gravia.Application.Projects.Services;
using Gravia.Application.Settings.Commands;
using Gravia.Application.Settings.Queries;
using Gravia.Application.Settings.Services;
using Gravia.Application.Storage;
using Gravia.Application.Tests.Projects;
using Gravia.Domain.CommonExceptions;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Consts;
using Gravia.Domain.Settings.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Application.Tests.Settings;

[Collection("Project lifecycle")]
public sealed class ProjectSettingsRequestsTests : IAsyncLifetime
{
    private readonly Factory _factory = new();
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private ProjectService _projects = null!;
    private ISender _sender = null!;

    public async Task InitializeAsync()
    {
        Assert.Null(ProjectSession.Current);
        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddApplicationServices();
        services.AddSingleton<IProjectSessionFactory>(_factory);
        services.AddKeyedSingleton<IDataStore>(DataStoreKey.Application, new MemoryDataStore());
        services.AddSingleton<ILogJournal>(new UnusedLogJournal());
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _projects = _provider.GetRequiredService<ProjectService>();
        _scope = _provider.CreateAsyncScope();
        _sender = _scope.ServiceProvider.GetRequiredService<ISender>();
        await _provider.GetRequiredService<DomainEventDispatcher>().StartAsync(default);
    }

    public async Task DisposeAsync()
    {
        await _projects.StopAsync(default);
        await _provider.GetRequiredService<DomainEventDispatcher>().StopAsync(default);
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task NoProjectHasEmptyReadsAndRejectsMutations()
    {
        Assert.Empty(await _sender.Send(new GetProjectSettingsQuery()));
        Assert.Null(await _sender.Send(new GetProjectSettingQuery("setting")));
        var setError = await Assert.ThrowsAsync<NotFoundException>(() => _sender.Send(
            new SetProjectSettingValueCommand("setting", JsonSerializer.SerializeToElement(1))));
        var resetError = await Assert.ThrowsAsync<NotFoundException>(() => _sender.Send(
            new ResetProjectSettingValueCommand("setting")));
        Assert.Equal("project_not_open", setError.Code);
        Assert.Equal("project_not_open", resetError.Code);
    }

    [Fact]
    public async Task QueriesReturnOrderedMetadataAndIndependentValueSnapshots()
    {
        await _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "first"));
        var settings = ProjectSession.Current!.Settings;
        var last = Number("z.last");
        await settings.Register(last);
        await settings.Register(Number("a.first"));

        var catalog = await _sender.Send(new GetProjectSettingsQuery());
        var detail = await _sender.Send(new GetProjectSettingQuery(last.Id));

        Assert.Equal(new[] { "a.first", "z.last" }, catalog.Select(item => item.Id));
        Assert.DoesNotContain("\"Value\"", JsonSerializer.Serialize(catalog));
        Assert.Equal(1, detail!.Value.GetInt32());
        Assert.Equal(1, detail.DefaultValue.GetInt32());
        Assert.Null(await _sender.Send(new GetProjectSettingQuery("missing")));
        last.SetValue(8);
        Assert.Equal(1, detail.Value.GetInt32());
    }

    [Fact]
    public async Task CommandsSetAndResetOnlyTheProjectSetting()
    {
        await _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "first"));
        var projectTheme = new StringSetting("gravia.theme", new LocalizedText("en", "Theme"),
            "project-default", SettingsUiPaths.Appearance);
        await ProjectSession.Current!.Settings.Register(projectTheme);
        var application = _provider.GetRequiredService<ApplicationSettings>();
        await application.StartAsync(default);

        await _sender.Send(new SetProjectSettingValueCommand(projectTheme.Id, JsonSerializer.SerializeToElement("light")));
        Assert.Equal("light", projectTheme.Value);
        Assert.Equal("system", application.Theme.Value);

        await _sender.Send(new ResetProjectSettingValueCommand(projectTheme.Id));
        Assert.Equal("project-default", projectTheme.Value);
        Assert.Equal("system", application.Theme.Value);
    }

    [Fact]
    public async Task RejectedValuesAndUnknownIdsDoNotChangeSettings()
    {
        await _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "first"));
        var setting = Number("count");
        await ProjectSession.Current!.Settings.Register(setting);

        var invalid = await Assert.ThrowsAsync<ValidationException>(() => _sender.Send(
            new SetProjectSettingValueCommand(setting.Id, JsonSerializer.SerializeToElement("text"))));
        Assert.Equal("invalid_setting_value", Assert.Single(invalid.Errors).ErrorCode);
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => _sender.Send(
            new ResetProjectSettingValueCommand("missing")));
        Assert.Equal("not_found", missing.Code);
        missing = await Assert.ThrowsAsync<NotFoundException>(() => _sender.Send(
            new SetProjectSettingValueCommand("missing", JsonSerializer.SerializeToElement(4))));
        Assert.Equal("not_found", missing.Code);
        Assert.Equal(1, setting.Value);
    }

    [Fact]
    public async Task ValidatorsRejectEmptyIdsBeforeAccessingTheCurrentProject()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _sender.Send(new GetProjectSettingQuery("")));
        await Assert.ThrowsAsync<ValidationException>(() => _sender.Send(new ResetProjectSettingValueCommand("")));
        await Assert.ThrowsAsync<ValidationException>(() => _sender.Send(
            new SetProjectSettingValueCommand("", JsonSerializer.SerializeToElement(1))));
    }

    [Fact]
    public async Task WaitingCommandCanBeCancelledDuringProjectSwitchWithoutChangingOldProject()
    {
        await _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "first"));
        var old = Number("count");
        await ProjectSession.Current!.Settings.Register(old);
        old.SetValue(9);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _factory.BeforeOpen = async () => { entered.SetResult(); await release.Task; };
        var switching = _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "second"));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var reset = _sender.Send(new ResetProjectSettingValueCommand(old.Id), cancellation.Token);
        try
        {
            Assert.False(reset.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reset);
            Assert.Equal(9, old.Value);
        }
        finally { release.TrySetResult(); }
        await switching.WaitAsync(TimeSpan.FromSeconds(5));
    }


    private static IntSetting Number(string id)
        => new(id, new LocalizedText("en", "Count"), 1, SettingsUiPaths.Appearance);

    private sealed class Factory : IProjectSessionFactory
    {
        public Func<Task>? BeforeOpen { get; set; }
        public Task<ProjectSession> CreateAsync(string parentDirectory, string name, CancellationToken cancellationToken = default)
            => OpenAsync(Path.Combine(parentDirectory, name), cancellationToken);

        public async Task<ProjectSession> OpenAsync(string projectPath, CancellationToken cancellationToken = default)
        {
            if (BeforeOpen is not null) await BeforeOpen();
            var data = new MemoryDataStore();
            return new ProjectSession(projectPath, data,
                new ProjectSettings(data, NullLogger<ProjectSettings>.Instance), new KeyValueStore(data),
                new Gravia.Application.Plugins.Services.PluginManager(projectPath, data,
                    NullLogger<Gravia.Application.Plugins.Services.PluginManager>.Instance));
        }
    }
}
