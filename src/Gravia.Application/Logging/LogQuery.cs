using Microsoft.Extensions.Logging;

namespace Gravia.Application.Logging;

public sealed record LogQuery
{
    public long AfterSequence { get; init; }
    public int Limit { get; init; } = 100;
    public LogLevel MinimumLevel { get; init; } = LogLevel.Trace;
    public string? Category { get; init; }
    public string? Text { get; init; }
    public IReadOnlyDictionary<string, string?>? Properties { get; init; }
}

