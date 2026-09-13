using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Entities;
using Gravia.Domain.Settings.Exceptions;

namespace Gravia.Domain.Tests.Settings.Entities;

public sealed class DoubleSettingTests
{
    private static readonly LocalizedText Title = new("en", "Tolerance");

    [Theory]
    [InlineData(null, null, double.MinValue, double.MaxValue)]
    [InlineData(0.5, null, 0.5, double.MaxValue)]
    [InlineData(null, 1.5, double.MinValue, 1.5)]
    [InlineData(0.5, 1.5, 0.5, 1.5)]
    [InlineData(0.5, 0.5, 0.5, 0.5)]
    public void AcceptsInclusiveAndOptionalBounds(double? min, double? max, double initial, double next)
    {
        var setting = new DoubleSetting("tolerance", Title, initial, min, max);

        Assert.Equal(min, setting.Min);
        Assert.Equal(max, setting.Max);
        Assert.Equal(initial, setting.Value);
        setting.SetValue(next);
        Assert.Equal(next, setting.Value);
        setting.Reset();
        Assert.Equal(initial, setting.Value);
        Assert.Equal(initial, setting.DefaultValue);
    }

    [Theory]
    [InlineData(0.49)]
    [InlineData(1.51)]
    public void RejectsInvalidDefaultAndChanges(double invalid)
    {
        Assert.Throws<InvalidSettingValueException>(() => new DoubleSetting("tolerance", Title, invalid, 0.5, 1.5));
        var setting = new DoubleSetting("tolerance", Title, 1, 0.5, 1.5);
        setting.SetValue(1.25);

        var error = Assert.Throws<InvalidSettingValueException>(() => setting.SetValue(invalid));

        Assert.Equal("setting_value_invalid", error.Code);
        Assert.Equal("tolerance", error.Extra["settingId"]);
        Assert.Equal(1.25, setting.Value);
        Assert.Equal(1, setting.DefaultValue);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteValuesAreRejectedEvenWithoutBounds(double invalid)
    {
        Assert.Throws<InvalidSettingValueException>(() => new DoubleSetting("tolerance", Title, invalid));
        var setting = new DoubleSetting("tolerance", Title, 1);
        Assert.Throws<InvalidSettingValueException>(() => setting.SetValue(invalid));
        Assert.Equal(1, setting.Value);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteBoundsAreArgumentErrors(double invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DoubleSetting("tolerance", Title, 1, min: invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DoubleSetting("tolerance", Title, 1, max: invalid));
    }

    [Fact]
    public void RejectsReversedBoundsAsAnArgumentError()
    {
        Assert.Throws<ArgumentException>(() => new DoubleSetting("tolerance", Title, 1, 2, 0));
    }
}
