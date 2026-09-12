using Microsoft.Extensions.Logging;

namespace Gravia.Application.Logging;

public sealed record LogMessage(
    LogLevel Level,
    string Category,
    string Message,
    EventId EventId = default,
    string? Exception = null,
    IReadOnlyDictionary<string, string?>? Properties = null,
    bool IsTruncated = false);
