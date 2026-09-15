using MediatR;
using Microsoft.Extensions.Logging;

namespace Gravia.Application.Common.Events;

/// <summary>Notifies every handler sequentially, even if another handler fails.</summary>
public sealed class DomainEventNotificationPublisher(ILogger<DomainEventNotificationPublisher> logger)
    : INotificationPublisher
{
    public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification, CancellationToken cancellationToken)
    {
        foreach (var handler in handlerExecutors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await handler.HandlerCallback(notification, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Handler {HandlerType} failed to process domain event {EventType}.",
                    handler.HandlerInstance.GetType().FullName, notification.GetType().FullName);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}
