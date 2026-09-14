using System.Text.Json;
using Gravia.Domain.Settings.Interfaces;

namespace Gravia.Application.Settings.DTOs;

public sealed record SettingDto(
    SettingSummaryDto Summary,
    JsonElement Value,
    JsonElement DefaultValue);

public static class SettingMapping
{
    public static SettingDto ToDto(this ISetting setting) => new(
        setting.ToSummaryDto(),
        setting.SerializeValue(),
        setting.SerializeDefaultValue());
}
