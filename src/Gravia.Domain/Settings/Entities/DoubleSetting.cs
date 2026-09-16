using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Traits.Interfaces;
using Gravia.Domain.Values;
using Gravia.Domain.Values.Entities;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A floating-point setting.</summary>
public sealed class DoubleSetting : Setting<double>
{
    public override TypeDescriptor ValueType { get; } = new DoubleType();

    public DoubleSetting(
        string id,
        LocalizedText title,
        double defaultValue,
        SettingsUiPath uiPath,
        LocalizedText? description = null,
        IReadOnlyList<ITrait>? traits = null)
        : base(id, title, defaultValue, uiPath, description, traits)
    {
    }
}
