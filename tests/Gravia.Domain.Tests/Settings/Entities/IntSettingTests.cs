using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Entities;
using Gravia.Domain.Settings.Exceptions;

namespace Gravia.Domain.Tests.Settings.Entities;

public sealed class IntSettingTests
{
    private static readonly LocalizedText Title = new("en", "Iterations");

    [Theory]
    [InlineData(null, null, int.MinValue, int.MaxValue)]
    [InlineData(1, null, 1, int.MaxValue)]
    [InlineData(null, 10, int.MinValue, 10)]
    [InlineData(1, 10, 1, 10)]
    [InlineData(5, 5, 5, 5)]
    public void AcceptsInclusiveAndOptionalBounds(int? min, int? max, int initial, int next)
    {
        var description = new LocalizedText("en", "Iteration count");
        var setting = new IntSetting("iterations", Title, initial, min, max, description);

        Assert.Equal(min, setting.Min);
        Assert.Equal(max, setting.Max);
        Assert.Same(description, setting.Description);
        Assert.Equal(initial, setting.Value);
        setting.SetValue(next);
        Assert.Equal(next, setting.Value);
        setting.Reset();
        Assert.Equal(initial, setting.Value);
        Assert.Equal(initial, setting.DefaultValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void RejectsInvalidDefaultAndChangesWithoutLosingCurrentValue(int invalid)
    {
        Assert.Throws<InvalidSettingValueException>(() => new IntSetting("iterations", Title, invalid, 1, 10));
        var setting = new IntSetting("iterations", Title, 5, 1, 10);
        setting.SetValue(7);

        var error = Assert.Throws<InvalidSettingValueException>(() => setting.SetValue(invalid));

        Assert.Equal("setting_value_invalid", error.Code);
        Assert.Equal("iterations", error.Extra["settingId"]);
        Assert.Single(error.Extra);
        Assert.Equal(7, setting.Value);
        Assert.Equal(5, setting.DefaultValue);
    }

    [Fact]
    public void RejectsReversedBoundsAsAnArgumentError()
    {
        Assert.Throws<ArgumentException>(() => new IntSetting("iterations", Title, 5, 10, 1));
    }
}
