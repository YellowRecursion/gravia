using System.Text.Json;
using Gravia.Domain.Settings.Interfaces;
using Gravia.Domain.Values;

namespace Gravia.Application.Settings.DTOs;

public sealed record SettingDto(
    SettingSummaryDto Summary,
    JsonElement Value,
    JsonElement DefaultValue,
    TypeDescriptor ValueType,
    JsonElement[] Traits);

public static class SettingMapping
{
    private static readonly JsonSerializerOptions TraitJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static SettingDto ToDto(this ISetting setting) => new(
        setting.ToSummaryDto(),
        setting.SerializeValue(),
        setting.SerializeDefaultValue(),
        setting.ValueType,
        setting.Traits.Select(trait =>
            JsonSerializer.SerializeToElement(trait, trait.GetType(), TraitJsonOptions)).ToArray());
}
