using Gravia.Domain.Localization.Consts;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Entities;

namespace Gravia.Domain.Settings.Consts;

/// <summary>Standard UI locations provided by Gravia.</summary>
public static class SettingsUiPaths
{
    public static SettingsUiPath Appearance { get; } = new(
        new LocalizedText(GraviaLanguages.English, "Appearance")
            .With(GraviaLanguages.Russian, "Внешний вид"));
}
