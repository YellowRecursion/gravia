using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Traits.Interfaces;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A setting containing values indexed by string keys.</summary>
/// <remarks>
/// Replace the whole dictionary through SetValue. Do not mutate supplied collections or their values.
/// Collections are not copied; Value and DefaultValue initially reference the same dictionary.
/// </remarks>
public sealed class DictionarySetting<T> : Setting<IReadOnlyDictionary<string, T>>
{
    public DictionarySetting(
        string id,
        LocalizedText title,
        IReadOnlyDictionary<string, T> defaultValue,
        SettingsUiPath uiPath,
        LocalizedText? description = null,
        IReadOnlyList<ITrait>? traits = null)
        : base(id, title, defaultValue, uiPath, description, traits)
    {
    }
}
