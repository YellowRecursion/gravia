using Microsoft.Extensions.Logging;

namespace Gravia.Application.Logging;

/// <summary>Immutable, detached record. Every occurrence has its own sequence.</summary>
public sealed record LogEntry(
    long Sequence,
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Category,
    string Message,
    EventId EventId,
    string? Exception,
    IReadOnlyDictionary<string, string?> Properties,
    bool IsTruncated);
