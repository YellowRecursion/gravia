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

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        DomainEventPublisher.DomainEventRaised += OnDomainEventRaised;
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var domainEvent in _events.Reader.ReadAllAsync(stoppingToken))
        {
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
        if (!_events.Writer.TryWrite(domainEvent))
        {
            logger.LogWarning("Domain event {EventType} was not accepted because processing has stopped.",
                domainEvent.GetType().Name);
        }
    }
}
