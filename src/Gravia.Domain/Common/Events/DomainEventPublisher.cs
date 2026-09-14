namespace Gravia.Domain.Common.Events;

public static class DomainEventPublisher
{
    public static event Action<DomainEvent>? DomainEventRaised;
    
    internal static void Raise(DomainEvent domainEvent)
    {
        DomainEventRaised?.Invoke(domainEvent);
    }
}