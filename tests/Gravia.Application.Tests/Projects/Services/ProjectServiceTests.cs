using Gravia.Application.Projects.Interfaces;
using Gravia.Application.Projects.Services;
using Gravia.Application.Settings.Services;
using Gravia.Application.Storage;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Consts;
using Gravia.Domain.Settings.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Application.Tests.Projects.Services;

[CollectionDefinition("Project lifecycle", DisableParallelization = true)]
public sealed class ProjectLifecycleCollection;

[Collection("Project lifecycle")]
public sealed class ProjectServiceTests : IAsyncLifetime
{
    private readonly MemoryDataStore _appData = new();
    private readonly Factory _factory = new();
    private readonly TestLogger _logger = new();
    private readonly KeyValueStore _values;
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _values = new KeyValueStore(_appData);
        _service = new ProjectService(_factory, _values, _logger);
    }

    public Task InitializeAsync()
    {
        Assert.Null(ProjectSession.Current);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _service.StopAsync(default);
        await _values.DisposeAsync();
    }

    [Fact]
    public async Task RecentProjectsContainFiveDistinctPathsInMostRecentlyOpenedOrder()
    {
        await _values.SetAsync("unrelated", 42);
        for (var i = 1; i <= 7; i++) await _service.OpenAsync(ProjectPath(i));
        await _service.OpenAsync(ProjectPath(4) + Path.DirectorySeparatorChar);

        var expected = new[] { 4, 7, 6, 5, 3 }.Select(ProjectPath).ToArray();
        Assert.Equal(expected, (await _service.GetRecentAsync()).Select(project => project.Path));
        Assert.Equal(expected, await _values.GetAsync<string[]>(ProjectService.RecentProjectsKey));
        Assert.Equal(42, await _values.GetAsync<int>("unrelated"));
        Assert.Equal(ProjectPath(4), ProjectSession.Current!.ProjectPath);
    }

    [Fact]
    public async Task FailedOpenPreservesCurrentSessionAndHistory()
    {
        await _service.OpenAsync(ProjectPath(1));
        var original = ProjectSession.Current;
        var before = await _values.GetAsync<string[]>(ProjectService.RecentProjectsKey);
        _factory.Failure = new IOException("Unavailable directory.");

        await Assert.ThrowsAsync<IOException>(() => _service.OpenAsync(ProjectPath(2)));

        Assert.Same(original, ProjectSession.Current);
        Assert.Equal(0, ((MemoryDataStore)original!.DataStore).DisposeCount);
        Assert.Equal(before, await _values.GetAsync<string[]>(ProjectService.RecentProjectsKey));
    }

    [Fact]
    public async Task OpeningSamePathRetainsTheExistingServices()
    {
        await _service.OpenAsync(ProjectPath(1));
        var original = ProjectSession.Current;
        await original!.KeyValueStore.SetAsync("value", 12);

        await _service.OpenAsync(ProjectPath(1) + Path.DirectorySeparatorChar);

        Assert.Same(original, ProjectSession.Current);
        Assert.Equal(12, await original.KeyValueStore.GetAsync<int>("value"));
        Assert.Single(await _service.GetRecentAsync());
    }

    [Fact]
    public async Task CreateOpensTheCreatedDirectoryAndRemembersIt()
    {
        var parent = Path.GetTempPath();
        var project = await _service.CreateAsync(parent, "New project");
        Assert.Equal(Path.Combine(parent, "New project"), project.Path);
        Assert.Equal("New project", project.Name);
        Assert.Equal(project, Assert.Single(await _service.GetRecentAsync()));
    }

    [Fact]
    public async Task StartupReopensOnlyTheMostRecentProject()
    {
        await _values.SetAsync(ProjectService.RecentProjectsKey, new[] { ProjectPath(2), ProjectPath(1) });
        await _service.StartAsync(default);
        Assert.Equal(ProjectPath(2), ProjectSession.Current!.ProjectPath);
        Assert.Equal(new[] { ProjectPath(2) }, _factory.Opened);
    }

    [Fact]
    public async Task UnavailableLatestProjectLeavesApplicationEmptyAndDoesNotTryOlderEntries()
    {
        var recent = new[] { ProjectPath(2), ProjectPath(1) };
        await _values.SetAsync(ProjectService.RecentProjectsKey, recent);
        _factory.Failure = new IOException("Unavailable.");

        await _service.StartAsync(default);

        Assert.Null(ProjectSession.Current);
        Assert.Equal(new[] { ProjectPath(2) }, _factory.Opened);
        Assert.Equal(recent, await _values.GetAsync<string[]>(ProjectService.RecentProjectsKey));
        Assert.Contains(LogLevel.Error, _logger.Levels);
    }

    [Fact]
    public async Task StartupWithoutHistoryDoesNotCreateAProjectOrWriteHistory()
    {
        await _service.StartAsync(default);
        Assert.Null(ProjectSession.Current);
        Assert.Empty(_factory.Opened);
        Assert.False(await _values.ExistsAsync(ProjectService.RecentProjectsKey));
    }

    [Fact]
    public async Task SwitchSavesPreviousSettingsAndReleasesPreviousResources()
    {
        await _service.OpenAsync(ProjectPath(1));
        var old = ProjectSession.Current!;
        var setting = new IntSetting("number", new LocalizedText("en", "Number"), 1, SettingsUiPaths.Appearance);
        await old.Settings.Register(setting);
        setting.SetValue(10);

        await _service.OpenAsync(ProjectPath(2));

        Assert.Equal(1, ((MemoryDataStore)old.DataStore).DisposeCount);
        var stored = await old.DataStore.ReadAsync<Dictionary<string, int>>(SettingsStorage.FileName);
        Assert.Equal(10, stored!["number"]);
        Assert.Equal(ProjectPath(2), ProjectSession.Current!.ProjectPath);
    }

    [Fact]
    public async Task FailureSavingPreviousSettingsKeepsItCurrentAndReleasesCandidate()
    {
        await _service.OpenAsync(ProjectPath(1));
        var old = ProjectSession.Current!;
        await old.Settings.Register(new IntSetting("number", new LocalizedText("en", "Number"), 1, SettingsUiPaths.Appearance));
        ((MemoryDataStore)old.DataStore).WriteFailure = new IOException("Read-only.");
        try
        {
            await Assert.ThrowsAsync<IOException>(() => _service.OpenAsync(ProjectPath(2)));
            Assert.Same(old, ProjectSession.Current);
            Assert.Equal(1, ((MemoryDataStore)_factory.Created.Last().DataStore).DisposeCount);
            Assert.Equal(ProjectPath(1), Assert.Single(await _service.GetRecentAsync()).Path);
        }
        finally { ((MemoryDataStore)old.DataStore).WriteFailure = null; }
    }

    [Fact]
    public async Task HistoryWriteFailureIsLoggedWithoutReportingAFailedOpen()
    {
        _appData.WriteFailure = new IOException("History unavailable.");
        var project = await _service.OpenAsync(ProjectPath(1));
        Assert.Equal(project.Path, ProjectSession.Current!.ProjectPath);
        Assert.Contains(LogLevel.Error, _logger.Levels);
    }

    [Fact]
    public async Task StopReleasesCurrentSessionButPreservesHistoryForNextStartup()
    {
        await _service.OpenAsync(ProjectPath(1));
        var data = (MemoryDataStore)ProjectSession.Current!.DataStore;
        await _service.StopAsync(default);
        Assert.Null(ProjectSession.Current);
        Assert.Equal(1, data.DisposeCount);
        Assert.Equal(ProjectPath(1), Assert.Single(await _service.GetRecentAsync()).Path);
    }

    [Fact]
    public async Task ConcurrentOpenWaitsUntilPreviousOpenFinishes()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _factory.BeforeOpen = async path =>
        {
            if (path == ProjectPath(1)) { entered.SetResult(); await release.Task; }
        };
        var first = _service.OpenAsync(ProjectPath(1));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = _service.OpenAsync(ProjectPath(2));
        try { Assert.False(second.IsCompleted); }
        finally { release.TrySetResult(); }
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ProjectPath(2), ProjectSession.Current!.ProjectPath);
        Assert.Equal(new[] { ProjectPath(2), ProjectPath(1) }, (await _service.GetRecentAsync()).Select(item => item.Path));
    }

    private static string ProjectPath(int number) => Path.Combine(Path.GetTempPath(), $"Project-{number}");

    private sealed class Factory : IProjectSessionFactory
    {
        public Exception? Failure { get; set; }
        public Func<string, Task>? BeforeOpen { get; set; }
        public List<string> Opened { get; } = [];
        public List<ProjectSession> Created { get; } = [];
        public Task<ProjectSession> CreateAsync(string parentDirectory, string name, CancellationToken cancellationToken = default)
            => OpenAsync(Path.Combine(parentDirectory, name), cancellationToken);

        public async Task<ProjectSession> OpenAsync(string projectPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Opened.Add(projectPath);
            if (BeforeOpen is not null) await BeforeOpen(projectPath);
            if (Failure is not null) throw Failure;
            var data = new MemoryDataStore();
            var session = new ProjectSession(projectPath, data,
                new ProjectSettings(data, NullLogger<ProjectSettings>.Instance), new KeyValueStore(data));
            Created.Add(session);
            return session;
        }
    }

    private sealed class TestLogger : ILogger<ProjectService>
    {
        public List<LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }
}
