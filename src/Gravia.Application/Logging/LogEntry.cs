using Microsoft.Extensions.Logging;

namespace Gravia.Application.Logging;

/// <summary>Detached snapshot. Sequence identifies a row; LastSequence is its latest update cursor.</summary>
public sealed record LogEntry(
    long Sequence,
    long LastSequence,
    DateTimeOffset Timestamp,
    DateTimeOffset LastTimestamp,
    long RepeatCount,
    LogLevel Level,
    string Category,
    string Message,
    EventId EventId,
    string? Exception,
    IReadOnlyDictionary<string, string?> Properties,
    bool IsTruncated);

