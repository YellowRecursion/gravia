using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Traits.Interfaces;

namespace Gravia.Domain.Settings.Entities;

/// <summary>An integer setting.</summary>
public sealed class IntSetting : Setting<int>
{
    public IntSetting(
        string id,
        LocalizedText title,
        int defaultValue,
        SettingsUiPath uiPath,
        LocalizedText? description = null,
        IReadOnlyList<ITrait>? traits = null)
        : base(id, title, defaultValue, uiPath, description, traits)
    {
    }
}
