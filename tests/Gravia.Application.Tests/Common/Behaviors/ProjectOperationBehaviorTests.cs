using Gravia.Application.Common.Events;
using Gravia.Application.Logging;
using Gravia.Application.Projects.Interfaces;
using Gravia.Application.Projects.Queries;
using Gravia.Application.Projects.Services;
using Gravia.Application.Settings.Services;
using Gravia.Application.Storage;
using Gravia.Application.Tests.Projects;
using Gravia.Domain.Common.Events;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Application.Tests.Common.Behaviors;

[Collection("Project lifecycle")]
public sealed class ProjectOperationBehaviorTests : IAsyncLifetime
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
        services.AddTransient<IRequestHandler<TestProjectQuery, ProjectSession?>, TestProjectQueryHandler>();
        services.AddTransient<INotificationHandler<TestProjectEvent>, TestProjectEventHandler>();
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
    public async Task AnyRequestKeepsItsProjectAcrossAwaitsAndNestedRequests()
    {
        await _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "first"));
        var original = ProjectSession.Current!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = _sender.Send(new TestProjectQuery(async token =>
        {
            var project = ProjectSession.Current!;
            entered.SetResult();
            await release.Task.WaitAsync(token);
            Assert.Same(project, ProjectSession.Current);
            var nested = await _sender.Send(new GetCurrentProjectQuery(), token);
            Assert.Equal(project.ProjectPath, nested!.Path);
            await project.KeyValueStore.SetAsync("result", 42, token);
            return project;
        }));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var switching = _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "second"));
        try
        {
            Assert.False(switching.IsCompleted);
            Assert.Same(original, ProjectSession.Current);
        }
        finally { release.TrySetResult(); }
        Assert.Same(original, await running.WaitAsync(TimeSpan.FromSeconds(5)));
        await switching.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotSame(original, ProjectSession.Current);
        Assert.Equal(1, ((MemoryDataStore)original.DataStore).DisposeCount);
    }

    [Fact]
    public async Task AnyRequestArrivingDuringSwitchWaitsAndUsesNewProject()
    {
        await _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "first"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _factory.BeforeOpen = async () => { entered.SetResult(); await release.Task; };
        var switching = _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "second"));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var waiting = _sender.Send(new TestProjectQuery(_ => Task.FromResult(ProjectSession.Current)));
        try { Assert.False(waiting.IsCompleted); }
        finally { release.TrySetResult(); }
        await switching.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(ProjectSession.Current, await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("second", ProjectSession.Current!.Name);
    }

    [Fact]
    public async Task SwitchWaitsForEventHandlersIncludingTheirRequestsAndChildEvents()
    {
        await _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "first"));
        var original = ProjectSession.Current!;
        await _provider.GetRequiredService<DomainEventDispatcher>().DrainAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        new TestProjectEvent(async (sender, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            var current = await sender.Send(new GetCurrentProjectQuery(), token);
            new TestProjectEvent(async (nestedSender, nestedToken) =>
            {
                var nested = await nestedSender.Send(new GetCurrentProjectQuery(), nestedToken);
                observed.SetResult(current?.Path == nested?.Path ? nested?.Path : null);
            }).Raise();
        }).Raise();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var switching = _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "second"));
        try { Assert.False(switching.IsCompleted); }
        finally { release.TrySetResult(); }
        await switching.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(original.ProjectPath, await observed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotSame(original, ProjectSession.Current);
    }

    [Fact]
    public async Task RequestCannotChangeItsOwnProjectHalfwayThrough()
    {
        await _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "first"));
        var original = ProjectSession.Current;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sender.Send(new TestProjectQuery(async token =>
        {
            await _projects.OpenAsync(Path.Combine(Path.GetTempPath(), "second"), token);
            return ProjectSession.Current;
        })));
        Assert.Same(original, ProjectSession.Current);
    }

    public sealed record TestProjectQuery(Func<CancellationToken, Task<ProjectSession?>> Execute)
        : IRequest<ProjectSession?>;

    public sealed class TestProjectQueryHandler : IRequestHandler<TestProjectQuery, ProjectSession?>
    {
        public Task<ProjectSession?> Handle(TestProjectQuery request, CancellationToken cancellationToken)
            => request.Execute(cancellationToken);
    }

    public sealed class TestProjectEvent(Func<ISender, CancellationToken, Task> execute) : DomainEvent
    {
        public Func<ISender, CancellationToken, Task> Execute { get; } = execute;
    }

    public sealed class TestProjectEventHandler(ISender sender) : INotificationHandler<TestProjectEvent>
    {
        public Task Handle(TestProjectEvent notification, CancellationToken cancellationToken)
            => notification.Execute(sender, cancellationToken);
    }

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
                new ProjectSettings(data, NullLogger<ProjectSettings>.Instance), new KeyValueStore(data));
        }
    }
}
