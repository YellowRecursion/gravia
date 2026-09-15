using FluentValidation;
using Gravia.Application.Projects.Services;
using Gravia.Application.Storage;
using Gravia.Domain.CommonExceptions;
using Gravia.Host.Fusion.Projects;
using Gravia.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Gravia.Host.Tests.Projects;

[CollectionDefinition("Project lifecycle", DisableParallelization = true)]
public sealed class ProjectLifecycleCollection;

[Collection("Project lifecycle")]
public sealed class ProjectsApiTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gravia-project-api-test-" + Guid.NewGuid().ToString("N"));
    private ServiceProvider? _provider;

    public async Task InitializeAsync()
    {
        Assert.Null(ProjectSession.Current);
        Directory.CreateDirectory(_directory);
        await Start();
    }

    public async Task DisposeAsync()
    {
        await Stop();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task CreateSwitchAndQueryUseApplicationPipelineAndDoNotExposeClose()
    {
        var api = _provider!.GetRequiredService<ProjectsApi>();
        Assert.Null((await api.GetCurrent()).Data);
        Assert.Empty((await api.GetRecent()).Data!);

        var created = await api.Create(_directory, "first");
        Assert.True(created.IsSuccess);
        Assert.Equal("first", created.Data!.Name);
        var first = ProjectSession.Current!;
        await first.KeyValueStore.SetAsync("sample", 7);

        var second = await api.Create(_directory, "second");
        Assert.Equal(second.Data, (await api.GetCurrent()).Data);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => first.KeyValueStore.GetAsync<int>("sample"));
        await api.Open(created.Data.Path);
        Assert.Equal(7, await ProjectSession.Current!.KeyValueStore.GetAsync<int>("sample"));
        Assert.Equal(new[] { "first", "second" }, (await api.GetRecent()).Data!.Select(item => item.Name));
        Assert.DoesNotContain(typeof(ProjectsApi).GetMethods(), method => method.Name == "Close");
    }

    [Fact]
    public async Task InvalidCommandAndUnavailableDirectoryLeaveCurrentAndRecentUnchanged()
    {
        var api = _provider!.GetRequiredService<ProjectsApi>();
        var created = await api.Create(_directory, "first");
        var current = ProjectSession.Current;

        await Assert.ThrowsAsync<ValidationException>(() => api.Create(_directory, ""));
        await Assert.ThrowsAsync<ValidationException>(() => api.Open(""));
        await Assert.ThrowsAsync<NotFoundException>(() => api.Open(Path.Combine(_directory, "missing")));

        Assert.Same(current, ProjectSession.Current);
        Assert.Equal(created.Data, Assert.Single((await api.GetRecent()).Data!));
    }

    [Fact]
    public async Task HostRestartReopensLatestProjectFromApplicationKeyValues()
    {
        var api = _provider!.GetRequiredService<ProjectsApi>();
        await api.Create(_directory, "first");
        await api.Create(_directory, "latest");
        var old = ProjectSession.Current!;
        await old.KeyValueStore.SetAsync("sample", "saved");

        await Stop();
        Assert.Null(ProjectSession.Current);
        await Start();

        Assert.NotSame(old, ProjectSession.Current);
        Assert.Equal("latest", ProjectSession.Current!.Name);
        Assert.Equal("saved", await ProjectSession.Current.KeyValueStore.GetAsync<string>("sample"));
        Assert.Equal(new[] { "latest", "first" },
            (await _provider!.GetRequiredService<ProjectsApi>().GetRecent()).Data!.Select(item => item.Name));
    }

    private async Task Start()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHostServices();
        services.AddKeyedSingleton<IDataStore>(DataStoreKey.Application,
            (_, _) => new FileDataStore(Path.Combine(_directory, "application")));
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        foreach (var service in _provider.GetServices<IHostedService>()) await service.StartAsync(default);
    }

    private async Task Stop()
    {
        if (_provider is null) return;
        foreach (var service in _provider.GetServices<IHostedService>().Reverse()) await service.StopAsync(default);
        await _provider.DisposeAsync();
        _provider = null;
    }
}
