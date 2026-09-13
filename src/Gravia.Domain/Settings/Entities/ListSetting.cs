using Gravia.Domain.Localization.Entities;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A setting containing a list of values.</summary>
/// <remarks>
/// Replace the whole list through SetValue. Do not mutate supplied collections or their elements.
/// Collections are not copied; Value and DefaultValue initially reference the same list.
/// </remarks>
public sealed class ListSetting<T> : Setting<IReadOnlyList<T>>
{
    public ListSetting(string id, LocalizedText title, IReadOnlyList<T> defaultValue, SettingsUiPath uiPath,
        LocalizedText? description = null)
        : base(id, title, defaultValue, uiPath, description)
    {
    }
}
