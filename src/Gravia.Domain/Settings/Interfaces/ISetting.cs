using System.Text.Json;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Entities;

namespace Gravia.Domain.Settings.Interfaces;

/// <summary>Common metadata and operations for settings with different value types.</summary>
public interface ISetting
{
    string Id { get; }
    LocalizedText Title { get; }
    /// <summary>Placement in the UI, independent of the setting's identity and storage.</summary>
    SettingsUiPath UiPath { get; }
    LocalizedText? Description { get; }

    /// <summary>Restores the default value.</summary>
    void Reset();

    /// <summary>Serializes the current value only, without setting metadata.</summary>
    JsonElement SerializeValue();

    JsonElement SerializeDefaultValue();

    /// <summary>Assigns a serialized value and raises a change event.</summary>
    void SetSerializedValue(JsonElement value);

    /// <summary>Deserializes and assigns a value without notification. Deserialization failure leaves the current value unchanged.</summary>
    void DeserializeValue(JsonElement value);
}
