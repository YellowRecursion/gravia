using Gravia.Domain.Localization.Entities;

namespace Gravia.Domain.Settings.Entities;

/// <summary>An integer setting.</summary>
public sealed class IntSetting : Setting<int>
{
    public IntSetting(string id, LocalizedText title, int defaultValue, SettingsUiPath uiPath,
        LocalizedText? description = null)
        : base(id, title, defaultValue, uiPath, description)
    {
    }
}
