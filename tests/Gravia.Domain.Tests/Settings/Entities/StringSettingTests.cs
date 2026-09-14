using System.Text.Json;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Entities;

namespace Gravia.Domain.Tests.Settings.Entities;

public sealed class StringSettingTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  Нефть\n")]
    [InlineData("\"Example\"\\path")]
    [InlineData("😀e\u0301")]
    public void TextRoundtripsWithoutTrimmingOrNormalization(string value)
    {
        var title = new LocalizedText("en", "Name");
        var path = new SettingsUiPath(new LocalizedText("en", "General"));
        var setting = new StringSetting("name", title, "default", path);
        setting.SetValue(value);
        var json = setting.SerializeValue();

        Assert.Equal(JsonValueKind.String, json.ValueKind);
        Assert.Equal(value, json.GetString());
        setting.Reset();
        Assert.Equal("default", setting.Value);
        setting.DeserializeValue(json);
        Assert.Equal(value, setting.Value);
        Assert.Equal("default", setting.DefaultValue);
    }
}
