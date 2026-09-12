namespace Gravia.Host.Errors;

public enum ErrorKind
{
    Domain,
    Validation,
    Cancelled,
    Internal
}

/// <summary>Transport-neutral classification, not an HTTP or MCP wire contract.</summary>
public sealed record ErrorDetails(
    ErrorKind Kind,
    string Code,
    string Message,
    string TraceId,
    IReadOnlyDictionary<string, object?> Extra,
    IReadOnlyList<FieldError> Errors);

public sealed record FieldError(string Field, string Code, string Message);
