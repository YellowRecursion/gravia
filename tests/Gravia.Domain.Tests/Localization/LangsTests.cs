using Gravia.Domain.Localization;
using Gravia.Domain.Localization.Consts;

namespace Gravia.Domain.Tests.Localization;

public sealed class LangsTests
{
    [Fact]
    public void SupportedContainsEnglishAndRussianExactlyOnce()
    {
        Assert.Equal("en", Langs.English);
        Assert.Equal("ru", Langs.Russian);
        Assert.Equal(new[] { Langs.English, Langs.Russian }, Langs.Supported);
    }

    [Fact]
    public void SupportedCannotBeMutatedThroughListInterfaces()
    {
        var languages = Assert.IsAssignableFrom<IList<string>>(Langs.Supported);

        Assert.Throws<NotSupportedException>(() => languages.Add("vi"));
        Assert.Throws<NotSupportedException>(() => languages[0] = "vi");
        Assert.Throws<NotSupportedException>(() => languages.Remove("en"));
        Assert.Throws<NotSupportedException>(() => languages.Clear());
        Assert.Equal(new[] { "en", "ru" }, Langs.Supported);
    }
}
