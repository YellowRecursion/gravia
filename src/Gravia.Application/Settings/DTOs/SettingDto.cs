using System.Text.Json;

namespace Gravia.Application.Settings.DTOs;

public sealed record SettingDto(
    SettingSummaryDto Summary, 
    JsonElement Value, 
    JsonElement DefaultValue);