using Gravia.Domain.Localization.Entities;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A string setting.</summary>
public sealed class StringSetting : Setting<string>
{
    public StringSetting(string id, LocalizedText title, string defaultValue, SettingsUiPath uiPath,
        LocalizedText? description = null)
        : base(id, title, defaultValue, uiPath, description)
    {
    }
}
