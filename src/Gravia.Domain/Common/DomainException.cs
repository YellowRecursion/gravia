using System.Collections.ObjectModel;

namespace Gravia.Domain.Common;

/// <summary>
/// An expected rejection caused by a domain rule, independent of any transport.
/// Derive a specific exception for each rule and keep its code stable.
/// </summary>
public abstract class DomainException : Exception
{
    private readonly Dictionary<string, object?> _extra = new(StringComparer.Ordinal);

    protected DomainException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Extra = new ReadOnlyDictionary<string, object?>(_extra);
    }

    /// <summary>A stable, machine-readable code; not a localized message or HTTP status.</summary>
    public string Code { get; }

    /// <summary>
    /// Safe structured details for clients, empty when there are none.
    /// Values should be immutable, serializable data, not entities or exceptions.
    /// This view prevents dictionary edits, but does not deep-copy its values.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Extra { get; }

    /// <summary>
    /// Adds or replaces a detail with a stable camelCase key.
    /// Call only during construction of a derived exception.
    /// Never include secrets or internal diagnostic data.
    /// </summary>
    protected void Add(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _extra[key] = value;
    }
}
