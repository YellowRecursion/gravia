using Gravia.Domain.Localization.Entities;

namespace Gravia.Domain.Settings.Entities;

/// <summary>An immutable choice identified independently of its localized title.</summary>
public sealed record SettingOption
{
    public string Id { get; }
    public LocalizedText Title { get; }

    public SettingOption(string id, LocalizedText title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(title);

        Id = id;
        Title = title;
    }
}
