using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Entities;

namespace Gravia.Domain.Tests.Settings.Entities;

public sealed class SettingOptionTests
{
    [Fact]
    public void PreservesIdAndLocalizedTitle()
    {
        var title = new LocalizedText("en", "Dark").With("ru", "Тёмная");
        var option = new SettingOption("dark", title);

        Assert.Equal("dark", option.Id);
        Assert.Same(title, option.Title);
        Assert.Equal("Тёмная", option.Title.Resolve("ru"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void RejectsInvalidId(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SettingOption(id!, new LocalizedText("en", "Title")));
    }

    [Fact]
    public void RejectsNullTitle()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingOption("dark", null!));
    }
}
