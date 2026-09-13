using Gravia.Domain.Localization.Entities;

namespace Gravia.Domain.Settings.Interfaces;

/// <summary>Common metadata and operations for settings with different value types.</summary>
public interface ISetting
{
    string Id { get; }
    LocalizedText Title { get; }
    LocalizedText? Description { get; }

    /// <summary>Restores the default value using the same validation as an ordinary change.</summary>
    void Reset();
}
