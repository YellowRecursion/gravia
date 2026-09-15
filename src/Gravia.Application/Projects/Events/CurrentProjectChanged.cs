using Gravia.Domain.Common.Events;

namespace Gravia.Application.Projects.Events;

/// <summary>The current session was replaced or removed, including reopening the same directory.</summary>
public sealed class CurrentProjectChanged : DomainEvent;
