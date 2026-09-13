using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Entities;
using Gravia.Domain.Settings.Exceptions;

namespace Gravia.Domain.Tests.Settings.Entities;

public sealed class StringSettingTests
{
    private static readonly LocalizedText Title = new("en", "Name");

    [Theory]
    [InlineData(null, null, "", "  Нефть\n")]
    [InlineData(2, null, "ab", "abcd")]
    [InlineData(null, 3, "", "abc")]
    [InlineData(1, 3, "a", "abc")]
    [InlineData(2, 2, "ab", "cd")]
    [InlineData(0, 0, "", "")]
    public void AcceptsInclusiveAndOptionalBounds(int? min, int? max, string initial, string next)
    {
        var setting = new StringSetting("name", Title, initial, min, max);

        Assert.Equal(min, setting.MinLength);
        Assert.Equal(max, setting.MaxLength);
        Assert.Equal(initial, setting.Value);
        setting.SetValue(next);
        Assert.Equal(next, setting.Value);
        setting.Reset();
        Assert.Equal(initial, setting.Value);
        Assert.Equal(initial, setting.DefaultValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abcd")]
    [InlineData(null)]
    public void RejectsInvalidDefaultAndChangesWithoutLosingCurrentValue(string? invalid)
    {
        Assert.Throws<InvalidSettingValueException>(() => new StringSetting("name", Title, invalid!, 1, 3));
        var setting = new StringSetting("name", Title, "a", 1, 3);
        setting.SetValue("ab");

        var error = Assert.Throws<InvalidSettingValueException>(() => setting.SetValue(invalid!));

        Assert.Equal("setting_value_invalid", error.Code);
        Assert.Equal("name", error.Extra["settingId"]);
        Assert.Equal("ab", setting.Value);
        Assert.Equal("a", setting.DefaultValue);
    }

    [Fact]
    public void NullIsRejectedEvenWithoutLengthBounds()
    {
        Assert.Throws<InvalidSettingValueException>(() => new StringSetting("name", Title, null!));
        var setting = new StringSetting("name", Title, "");
        Assert.Throws<InvalidSettingValueException>(() => setting.SetValue(null!));
        Assert.Equal("", setting.Value);
    }

    [Fact]
    public void LengthUsesUtf16CodeUnitsWithoutTrimmingOrNormalization()
    {
        var setting = new StringSetting("name", Title, "😀", 2, 2);
        Assert.Equal("😀", setting.Value);
        setting.SetValue("e\u0301");
        Assert.Equal("e\u0301", setting.Value);
        setting.SetValue("  ");
        Assert.Equal("  ", setting.Value);
        Assert.Throws<InvalidSettingValueException>(() => setting.SetValue("é"));
    }

    [Fact]
    public void NegativeLengthsAreArgumentErrors()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringSetting("name", Title, "", minLength: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringSetting("name", Title, "", maxLength: -1));
    }

    [Fact]
    public void ReversedLengthsAreArgumentErrors()
    {
        Assert.Throws<ArgumentException>(() => new StringSetting("name", Title, "ab", 3, 1));
    }
}
