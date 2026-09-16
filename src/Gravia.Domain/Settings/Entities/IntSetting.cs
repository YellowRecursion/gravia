using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Traits.Interfaces;
using Gravia.Domain.Values;
using Gravia.Domain.Values.Entities;

namespace Gravia.Domain.Settings.Entities;

/// <summary>An integer setting.</summary>
public sealed class IntSetting : Setting<int>
{
    public override TypeDescriptor ValueType { get; } = new IntType();

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
