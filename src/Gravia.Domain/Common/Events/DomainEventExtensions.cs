namespace Gravia.Domain.Common.Events;

public static class DomainEventExtensions
{
    public static void Raise(this DomainEvent domainEvent)
    {
        DomainEventPublisher.Raise(domainEvent);
    }
}