using System.Text.Json;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Interfaces;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A typed setting with metadata, a current value, and a default value.</summary>
public abstract class Setting<T> : ISetting
{
    private T _value;

    public string Id { get; }
    public LocalizedText Title { get; }
    public SettingsUiPath UiPath { get; }
    public LocalizedText? Description { get; }
    public T Value => _value;
    public T DefaultValue { get; }

    /// <summary>Initializes the metadata and uses the default as the initial value.</summary>
    protected Setting(string id, LocalizedText title, T defaultValue, SettingsUiPath uiPath, LocalizedText? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(uiPath);

        Id = id;
        Title = title;
        UiPath = uiPath;
        Description = description;
        DefaultValue = defaultValue;
        _value = defaultValue;
    }

    /// <summary>Assigns the current value.</summary>
    public void SetValue(T value) => _value = value;

    public void Reset() => SetValue(DefaultValue);

    /// <summary>Serializes the current value only, without setting metadata.</summary>
    public virtual JsonElement SerializeValue()
        => JsonSerializer.SerializeToElement(Value);

    /// <summary>Deserializes and assigns a value. Deserialization failure leaves the current value unchanged.</summary>
    public virtual void DeserializeValue(JsonElement value)
        => SetValue(value.Deserialize<T>()!);
}
