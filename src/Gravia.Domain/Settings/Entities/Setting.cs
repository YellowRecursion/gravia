using System.Text.Json;
using Gravia.Domain.Common.Events;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Events;
using Gravia.Domain.Settings.Interfaces;
using Gravia.Domain.Traits.Interfaces;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A typed setting with metadata, a current value, and a default value.</summary>
public abstract class Setting<T> : ISetting
{
    private readonly IReadOnlyList<ITrait> _traits;

    public string Id { get; }
    public LocalizedText Title { get; }
    public SettingsUiPath UiPath { get; }
    public LocalizedText? Description { get; }
    public T Value { get; private set; }
    public T DefaultValue { get; }
    public IReadOnlyList<ITrait> Traits => _traits;

    /// <summary>Initializes the metadata and uses the default as the initial value.</summary>
    protected Setting(
        string id,
        LocalizedText title,
        T defaultValue,
        SettingsUiPath uiPath,
        LocalizedText? description = null,
        IReadOnlyList<ITrait>? traits = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(uiPath);

        Id = id;
        Title = title;
        UiPath = uiPath;
        Description = description;
        _traits = traits ?? [];
        DefaultValue = defaultValue;
        Validate(defaultValue);
        Value = defaultValue;
    }

    /// <summary>Assigns the current value and raises a change event.</summary>
    public void SetValue(T value)
    {
        SetValueWithoutNotify(value);
        new SettingChanged(this).Raise();
    }

    /// <summary>Assigns the current value without raising a change event.</summary>
    public void SetValueWithoutNotify(T value)
    {
        Validate(value);
        Value = value;
    }

    public void Reset()
    {
        SetValue(DefaultValue);
    }

    /// <summary>Serializes the current value only, without setting metadata.</summary>
    public virtual JsonElement SerializeValue()
    {
        return JsonSerializer.SerializeToElement(Value);
    }

    public virtual JsonElement SerializeDefaultValue()
        => JsonSerializer.SerializeToElement(DefaultValue);

    void ISetting.SetSerializedValue(JsonElement value)
    {
        DeserializeValue(value);
        new SettingChanged(this).Raise();
    }

    /// <summary>Deserializes and assigns a value without notification. Deserialization failure leaves the current value unchanged.</summary>
    public virtual void DeserializeValue(JsonElement value)
    {
        SetValueWithoutNotify(value.Deserialize<T>()!);
    }

    private void Validate(T value)
    {
        foreach (var trait in _traits)
        {
            if (trait is IValidationTrait<T> validationTrait)
                validationTrait.Validate(value);
        }
    }
}
