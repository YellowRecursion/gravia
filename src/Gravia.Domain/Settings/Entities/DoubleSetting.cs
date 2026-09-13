using Gravia.Domain.Localization.Entities;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A floating-point setting.</summary>
public sealed class DoubleSetting : Setting<double>
{
    public DoubleSetting(string id, LocalizedText title, double defaultValue, SettingsUiPath uiPath,
        LocalizedText? description = null)
        : base(id, title, defaultValue, uiPath, description)
    {
    }
}
