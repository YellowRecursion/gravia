using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Exceptions;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A non-null string setting with optional inclusive length bounds.</summary>
/// <remarks>Length is measured in UTF-16 code units using string.Length. Text is not trimmed or normalized.</remarks>
public sealed class StringSetting : Setting<string>
{
    public int? MinLength { get; }
    public int? MaxLength { get; }

    public StringSetting(string id, LocalizedText title, string defaultValue,
        int? minLength = null, int? maxLength = null, LocalizedText? description = null)
        : base(id, title, defaultValue, description)
    {
        if (minLength < 0) throw new ArgumentOutOfRangeException(nameof(minLength));
        if (maxLength < 0) throw new ArgumentOutOfRangeException(nameof(maxLength));
        if (minLength > maxLength)
            throw new ArgumentException("Minimum length must not exceed maximum length.", nameof(minLength));

        MinLength = minLength;
        MaxLength = maxLength;
        ValidateValue(defaultValue);
    }

    protected override void ValidateValue(string value)
    {
        if (value is null)
            throw new InvalidSettingValueException(Id, "The value must not be null.");
        if (value.Length < MinLength || value.Length > MaxLength)
            throw new InvalidSettingValueException(Id, "The text length is outside the allowed range.");
    }
}
