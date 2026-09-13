using Gravia.Domain.Localization.Entities;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A non-empty path of localized section names used only for placement in the settings UI.</summary>
/// <remarks>Does not determine setting identity, storage, visibility, or access.</remarks>
public sealed class SettingsUiPath
{
    public IReadOnlyList<LocalizedText> Segments { get; }

    public SettingsUiPath(params LocalizedText[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Length == 0)
            throw new ArgumentException("At least one UI path segment is required.", nameof(segments));

        Segments = Array.AsReadOnly(segments);
    }
}
