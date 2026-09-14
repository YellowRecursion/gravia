using Gravia.Application.Common.Events;
using Gravia.Domain.Common.Events;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Application.Tests.Common.Events;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task RaiseDoesNotWaitForProcessingAndStopDrainsEventsInOrder()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = new List<int>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var services = CreateServices(async (notification, token) =>
        {
            var domainEvent = (TestEvent)notification;
            if (domainEvent.Number == 1)
            {
                started.SetResult();
                await release.Task.WaitAsync(token);
            }

            processed.Add(domainEvent.Number);
        });
        var dispatcher = services.GetRequiredService<DomainEventDispatcher>();
        await dispatcher.StartAsync(timeout.Token);

        // Run separately so a regression to blocking Raise fails by timeout instead of hanging the test.
        await Task.Run(() => new TestEvent(1).Raise()).WaitAsync(timeout.Token);
        await started.Task.WaitAsync(timeout.Token);
        new TestEvent(2).Raise();

        var stopping = dispatcher.StopAsync(timeout.Token);
        Assert.False(stopping.IsCompleted);
        release.SetResult();
        await stopping;

        Assert.Equal(new[] { 1, 2 }, processed);
    }

    [Fact]
    public async Task HandlerFailureDoesNotStopFollowingEvents()
    {
        var processed = new List<int>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var services = CreateServices((notification, _) =>
        {
            var domainEvent = (TestEvent)notification;
            if (domainEvent.Number == 1)
                throw new InvalidOperationException("Handler failed.");

            processed.Add(domainEvent.Number);
            return Task.CompletedTask;
        });
        var dispatcher = services.GetRequiredService<DomainEventDispatcher>();
        await dispatcher.StartAsync(timeout.Token);

        new TestEvent(1).Raise();
        new TestEvent(2).Raise();
        await dispatcher.StopAsync(timeout.Token);

        Assert.Equal(new[] { 2 }, processed);
    }

    [Fact]
    public async Task ShutdownDeadlineCancelsTheActiveHandler()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var shutdown = new CancellationTokenSource();
        using var services = CreateServices(async (_, token) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally
            {
                finished.SetResult();
            }
        });
        var dispatcher = services.GetRequiredService<DomainEventDispatcher>();
        await dispatcher.StartAsync(timeout.Token);
        new TestEvent(1).Raise();
        await started.Task.WaitAsync(timeout.Token);

        var stopping = dispatcher.StopAsync(shutdown.Token);
        shutdown.Cancel();
        await stopping.WaitAsync(timeout.Token);
        await finished.Task.WaitAsync(timeout.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EachEventGetsItsOwnScopeAndDisposesItEvenOnFailure(bool failFirst)
    {
        var handled = new List<ScopedResource>();
        var registrations = new ServiceCollection();
        registrations.AddScoped<ScopedResource>();
        registrations.AddTransient<IPublisher>(provider => new Mediator(provider));
        registrations.AddTransient<INotificationHandler<TestEvent>>(provider =>
            new ScopedHandler(provider.GetRequiredService<ScopedResource>(), handled, failFirst));
        using var services = registrations.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var dispatcher = new DomainEventDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(), NullLogger<DomainEventDispatcher>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await dispatcher.StartAsync(timeout.Token);
        new TestEvent(1).Raise();
        new TestEvent(2).Raise();
        await dispatcher.StopAsync(timeout.Token);

        Assert.Equal(2, handled.Count);
        Assert.NotSame(handled[0], handled[1]);
        Assert.All(handled, resource => Assert.True(resource.IsDisposed));
    }

    private static ServiceProvider CreateServices(Func<object, CancellationToken, Task> handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPublisher>(new TestPublisher(handler));
        services.AddSingleton(provider => new DomainEventDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<DomainEventDispatcher>.Instance));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class ScopedResource : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            IsDisposed = true;
        }
    }

    private sealed class ScopedHandler(
        ScopedResource resource,
        List<ScopedResource> handled,
        bool failFirst) : INotificationHandler<TestEvent>
    {
        public Task Handle(TestEvent notification, CancellationToken cancellationToken)
        {
            Assert.False(resource.IsDisposed);
            Assert.All(handled, previous => Assert.True(previous.IsDisposed));
            handled.Add(resource);

            if (failFirst && notification.Number == 1)
                throw new InvalidOperationException("Handler failed.");

            return Task.CompletedTask;
        }
    }

    private sealed class TestEvent(int number) : DomainEvent
    {
        public int Number { get; } = number;
    }

    private sealed class TestPublisher(Func<object, CancellationToken, Task> handler) : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => handler(notification, cancellationToken);

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => handler(notification, cancellationToken);
    }
}
