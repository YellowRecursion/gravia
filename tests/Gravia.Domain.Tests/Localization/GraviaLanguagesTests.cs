using Gravia.Domain.Localization;
using Gravia.Domain.Localization.Consts;

namespace Gravia.Domain.Tests.Localization;

public sealed class GraviaLanguagesTests
{
    [Fact]
    public void SupportedContainsEnglishAndRussianExactlyOnce()
    {
        Assert.Equal("en", GraviaLanguages.English);
        Assert.Equal("ru", GraviaLanguages.Russian);
        Assert.Equal(new[] { GraviaLanguages.English, GraviaLanguages.Russian }, GraviaLanguages.Supported);
    }

    [Fact]
    public void SupportedCannotBeMutatedThroughListInterfaces()
    {
        var languages = Assert.IsAssignableFrom<IList<string>>(GraviaLanguages.Supported);

        Assert.Throws<NotSupportedException>(() => languages.Add("vi"));
        Assert.Throws<NotSupportedException>(() => languages[0] = "vi");
        Assert.Throws<NotSupportedException>(() => languages.Remove("en"));
        Assert.Throws<NotSupportedException>(() => languages.Clear());
        Assert.Equal(new[] { "en", "ru" }, GraviaLanguages.Supported);
    }
}
