using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Traits.Interfaces;
using Gravia.Domain.Values;
using Gravia.Domain.Values.Entities;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A setting containing a list of values.</summary>
/// <remarks>
/// Replace the whole list through SetValue. Do not mutate supplied collections or their elements.
/// Collections are not copied; Value and DefaultValue initially reference the same list.
/// </remarks>
public sealed class ListSetting<T> : Setting<IReadOnlyList<T>>
{
    public override TypeDescriptor ValueType { get; }

    public ListSetting(
        string id,
        LocalizedText title,
        IReadOnlyList<T> defaultValue,
        SettingsUiPath uiPath,
        TypeDescriptor elementType,
        LocalizedText? description = null,
        IReadOnlyList<ITrait>? traits = null)
        : base(id, title, defaultValue, uiPath, description, traits)
    {
        ValueType = new ListType(elementType);
    }
}
