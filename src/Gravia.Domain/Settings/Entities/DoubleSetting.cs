using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Exceptions;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A finite floating-point setting with optional inclusive bounds.</summary>
public sealed class DoubleSetting : Setting<double>
{
    public double? Min { get; }
    public double? Max { get; }

    public DoubleSetting(string id, LocalizedText title, double defaultValue,
        double? min = null, double? max = null, LocalizedText? description = null)
        : base(id, title, defaultValue, description)
    {
        if (min is { } lower && !double.IsFinite(lower))
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum must be finite.");
        if (max is { } upper && !double.IsFinite(upper))
            throw new ArgumentOutOfRangeException(nameof(max), "Maximum must be finite.");
        if (min > max) throw new ArgumentException("Minimum must not exceed maximum.", nameof(min));

        Min = min;
        Max = max;
        ValidateValue(defaultValue);
    }

    protected override void ValidateValue(double value)
    {
        if (!double.IsFinite(value))
            throw new InvalidSettingValueException(Id, "The value must be a finite number.");
        if (value < Min || value > Max)
            throw new InvalidSettingValueException(Id, "The value is outside the allowed range.");
    }
}
