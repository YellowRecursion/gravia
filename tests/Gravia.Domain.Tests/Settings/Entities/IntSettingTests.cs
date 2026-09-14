using System.Text.Json;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Entities;

namespace Gravia.Domain.Tests.Settings.Entities;

public sealed class IntSettingTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void ValuesRoundtripAsJsonNumbersWithoutChangingDefault(int value)
    {
        var title = new LocalizedText("en", "Iterations");
        var path = new SettingsUiPath(new LocalizedText("en", "General"));
        var setting = new IntSetting("iterations", title, 10, path);
        setting.SetValue(value);
        var json = setting.SerializeValue();

        Assert.Equal(JsonValueKind.Number, json.ValueKind);
        Assert.Equal(value, json.GetInt32());
        setting.Reset();
        Assert.Equal(10, setting.Value);
        setting.DeserializeValue(json);
        Assert.Equal(value, setting.Value);
        Assert.Equal(10, setting.DefaultValue);
    }
}
