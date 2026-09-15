using Gravia.Application.Common.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gravia.Application.Tests.Common.Events;

public sealed class DomainEventNotificationPublisherTests
{
    [Fact]
    public async Task AwaitsEachHandlerBeforeStartingTheNext()
    {
        var logger = new TestLogger();
        var publisher = new DomainEventNotificationPublisher(logger);
        var notification = new TestNotification();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handled = new List<int>();
        var publishing = publisher.Publish([
            Executor(async (value, token) =>
            {
                Assert.Same(notification, value);
                Assert.Equal(timeout.Token, token);
                entered.SetResult();
                await release.Task.WaitAsync(token);
                handled.Add(1);
            }),
            Executor((_, _) => { handled.Add(2); return Task.CompletedTask; })
        ], notification, timeout.Token);
        await entered.Task.WaitAsync(timeout.Token);
        try
        {
            Assert.False(publishing.IsCompleted);
            Assert.Empty(handled);
        }
        finally { release.TrySetResult(); }
        await publishing.WaitAsync(timeout.Token);
        Assert.Equal(new[] { 1, 2 }, handled);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task LogsSynchronousAndAsynchronousFailuresAndContinuesOtherHandlers()
    {
        var logger = new TestLogger();
        var publisher = new DomainEventNotificationPublisher(logger);
        var first = new IOException("Cannot save.");
        var second = new InvalidOperationException("Handler failed.");
        var handled = new List<int>();
        await publisher.Publish([
            Executor((_, _) => throw first),
            Executor((_, _) => { handled.Add(1); return Task.CompletedTask; }),
            Executor(async (_, _) => { await Task.Yield(); throw second; }),
            Executor((_, _) => { handled.Add(2); return Task.CompletedTask; })
        ], new TestNotification(), default);

        Assert.Equal(new[] { 1, 2 }, handled);
        Assert.Equal(new Exception[] { first, second }, logger.Entries.Select(entry => entry.Exception));
        Assert.All(logger.Entries, entry =>
        {
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Contains(typeof(TestNotification).FullName!, entry.Message);
            Assert.Contains(typeof(TestHandler).FullName!, entry.Message);
        });
    }

    [Fact]
    public async Task AlreadyCancelledTokenDoesNotInvokeAnyHandlers()
    {
        var logger = new TestLogger();
        var publisher = new DomainEventNotificationPublisher(logger);
        var invoked = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.Publish([
            Executor((_, _) => { invoked = true; return Task.CompletedTask; })
        ], new TestNotification(), new CancellationToken(true)));
        Assert.False(invoked);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task HandlerCancellationPropagatesWithoutLoggingOrCallingRemainingHandlers()
    {
        var logger = new TestLogger();
        var publisher = new DomainEventNotificationPublisher(logger);
        var invoked = false;
        var cancellation = new OperationCanceledException();
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.Publish([
            Executor((_, _) => Task.FromException(cancellation)),
            Executor((_, _) => { invoked = true; return Task.CompletedTask; })
        ], new TestNotification(), default));
        Assert.Same(cancellation, error);
        Assert.False(invoked);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationIsObservedEvenIfTheLastHandlerReturnsNormally(bool includeNext)
    {
        var logger = new TestLogger();
        var publisher = new DomainEventNotificationPublisher(logger);
        using var cancellation = new CancellationTokenSource();
        var invoked = false;
        var handlers = new List<NotificationHandlerExecutor>
        {
            Executor((_, _) => { cancellation.Cancel(); return Task.CompletedTask; })
        };
        if (includeNext)
            handlers.Add(Executor((_, _) => { invoked = true; return Task.CompletedTask; }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            publisher.Publish(handlers, new TestNotification(), cancellation.Token));
        Assert.False(invoked);
        Assert.Empty(logger.Entries);
    }

    private static NotificationHandlerExecutor Executor(Func<INotification, CancellationToken, Task> callback)
        => new(new TestHandler(), callback);

    private sealed record TestNotification : INotification;
    private sealed class TestHandler;

    private sealed class TestLogger : ILogger<DomainEventNotificationPublisher>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, exception, formatter(state, exception)));
    }
}
