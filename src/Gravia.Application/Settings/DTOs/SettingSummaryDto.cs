using Gravia.Application.Localization.DTOs;
using Gravia.Domain.Settings.Interfaces;

namespace Gravia.Application.Settings.DTOs;

/// <summary>Search and navigation metadata, independent of the current value.</summary>
public sealed record SettingSummaryDto(
    string Id,
    LocalizedTextDto Title,
    LocalizedTextDto? Description,
    LocalizedTextDto[] UiPath);

public static class SettingSummaryMapping
{
    public static SettingSummaryDto ToSummaryDto(this ISetting setting) => new(
        setting.Id,
        setting.Title.ToDto(),
        setting.Description?.ToDto(),
        setting.UiPath.Segments.Select(segment => segment.ToDto()).ToArray());
}
