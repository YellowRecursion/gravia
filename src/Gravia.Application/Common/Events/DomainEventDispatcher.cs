using System.Threading.Channels;
using Gravia.Domain.Common.Events;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gravia.Application.Common.Events;

/// <summary>Processes raised domain events sequentially in the background.</summary>
public sealed class DomainEventDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<DomainEventDispatcher> logger) : BackgroundService
{
    private readonly Channel<DomainEvent> _events = Channel.CreateUnbounded<DomainEvent>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
    private static readonly AsyncLocal<bool> ProcessingEvent = new();
    private readonly object _pendingLock = new();
    private int _pendingEvents;
    private TaskCompletionSource? _drained;

    internal static bool IsProcessingEvent => ProcessingEvent.Value;

    /// <summary>Waits for accepted events and events raised by their handlers, including scope disposal.</summary>
    public Task DrainAsync(CancellationToken cancellationToken = default)
    {
        if (IsProcessingEvent)
            throw new InvalidOperationException("An event handler cannot wait for its own queue.");
        lock (_pendingLock)
            return (_drained?.Task ?? Task.CompletedTask).WaitAsync(cancellationToken);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        DomainEventPublisher.DomainEventRaised += OnDomainEventRaised;
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var domainEvent in _events.Reader.ReadAllAsync(stoppingToken))
            {
                ProcessingEvent.Value = true;
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                    await publisher.Publish(domainEvent, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to process domain event {EventType}.",
                        domainEvent.GetType().Name);
                }
                finally
                {
                    ProcessingEvent.Value = false;
                    if (!stoppingToken.IsCancellationRequested) EventCompleted();
                }
            }
        }
        finally
        {
            // Forced shutdown must release waiters even if queued events could not finish.
            lock (_pendingLock)
                _drained?.TrySetCanceled();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        DomainEventPublisher.DomainEventRaised -= OnDomainEventRaised;
        _events.Writer.TryComplete();

        // Drain accepted events before BackgroundService cancels the processing token.
        if (ExecuteTask is not null)
        {
            try
            {
                await ExecuteTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Domain event processing did not finish before the shutdown deadline.");
            }
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        DomainEventPublisher.DomainEventRaised -= OnDomainEventRaised;
        _events.Writer.TryComplete();
        base.Dispose();
    }

    private void OnDomainEventRaised(DomainEvent domainEvent)
    {
        lock (_pendingLock)
        {
            _drained ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingEvents++;
            if (_events.Writer.TryWrite(domainEvent)) return;
            EventCompleted();
            logger.LogWarning("Domain event {EventType} was not accepted because processing has stopped.",
                domainEvent.GetType().Name);
        }
    }

    private void EventCompleted()
    {
        lock (_pendingLock)
        {
            if (--_pendingEvents != 0) return;
            _drained!.TrySetResult();
            _drained = null;
        }
    }
}
