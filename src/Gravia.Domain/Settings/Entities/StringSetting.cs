using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Traits.Interfaces;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A string setting.</summary>
public sealed class StringSetting : Setting<string>
{
    public StringSetting(
        string id,
        LocalizedText title,
        string defaultValue,
        SettingsUiPath uiPath,
        LocalizedText? description = null,
        IReadOnlyList<ITrait>? traits = null)
        : base(id, title, defaultValue, uiPath, description, traits)
    {
    }
}
