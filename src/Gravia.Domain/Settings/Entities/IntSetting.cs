using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Exceptions;

namespace Gravia.Domain.Settings.Entities;

/// <summary>An integer setting with optional inclusive bounds.</summary>
public sealed class IntSetting : Setting<int>
{
    public int? Min { get; }
    public int? Max { get; }

    public IntSetting(string id, LocalizedText title, int defaultValue,
        int? min = null, int? max = null, LocalizedText? description = null)
        : base(id, title, defaultValue, description)
    {
        if (min > max) throw new ArgumentException("Minimum must not exceed maximum.", nameof(min));

        Min = min;
        Max = max;
        ValidateValue(defaultValue);
    }

    protected override void ValidateValue(int value)
    {
        if (value < Min || value > Max)
            throw new InvalidSettingValueException(Id, "The value is outside the allowed range.");
    }
}
