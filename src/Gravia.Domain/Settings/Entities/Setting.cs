using Gravia.Domain.Localization;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Interfaces;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A typed setting whose concrete implementation defines valid values.</summary>
/// <remarks>Use immutable value types or immutable objects so changes cannot bypass validation.</remarks>
public abstract class Setting<T> : ISetting
{
    private T _value;

    public string Id { get; }
    public LocalizedText Title { get; }
    public LocalizedText? Description { get; }
    public T Value => _value;
    public T DefaultValue { get; }

    /// <summary>Initializes the metadata and uses the default as the initial value.</summary>
    /// <remarks>
    /// The derived constructor must validate defaultValue after initializing its constraints.
    /// This constructor does not call overridable members on a partially initialized object.
    /// </remarks>
    protected Setting(string id, LocalizedText title, T defaultValue, LocalizedText? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(title);

        Id = id;
        Title = title;
        Description = description;
        DefaultValue = defaultValue;
        _value = defaultValue;
    }

    /// <summary>Validates and assigns a value. Failed validation leaves the current value unchanged.</summary>
    public void SetValue(T value)
    {
        ValidateValue(value);
        _value = value;
    }

    public void Reset() => SetValue(DefaultValue);

    /// <summary>Throws when the value violates this setting's constraints. Must not change state.</summary>
    protected abstract void ValidateValue(T value);
}
