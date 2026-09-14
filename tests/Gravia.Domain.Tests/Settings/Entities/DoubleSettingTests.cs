using System.Text.Json;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Entities;

namespace Gravia.Domain.Tests.Settings.Entities;

public sealed class DoubleSettingTests
{
    [Theory]
    [InlineData(double.MinValue)]
    [InlineData(0)]
    [InlineData(1.25)]
    [InlineData(double.MaxValue)]
    public void FiniteValuesRoundtripAsJsonNumbersWithoutChangingDefault(double value)
    {
        var title = new LocalizedText("en", "Tolerance");
        var path = new SettingsUiPath(new LocalizedText("en", "General"));
        var setting = new DoubleSetting("tolerance", title, 0.01, path);
        setting.SetValue(value);
        var json = setting.SerializeValue();

        Assert.Equal(JsonValueKind.Number, json.ValueKind);
        Assert.Equal(value, json.GetDouble());
        setting.Reset();
        Assert.Equal(0.01, setting.Value);
        setting.DeserializeValue(json);
        Assert.Equal(value, setting.Value);
        Assert.Equal(0.01, setting.DefaultValue);
    }
}
