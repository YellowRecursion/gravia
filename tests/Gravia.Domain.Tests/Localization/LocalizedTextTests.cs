using Gravia.Domain.Localization;
using Gravia.Domain.Localization.Consts;
using Gravia.Domain.Localization.Entities;

namespace Gravia.Domain.Tests.Localization;

public sealed class LocalizedTextTests
{
    [Fact]
    public void ConstructorStoresTheDefaultTranslation()
    {
        var text = new LocalizedText("en", "Temperature");

        Assert.Equal("en", text.DefaultLanguage);
        Assert.Equal(new KeyValuePair<string, string>("en", "Temperature"), Assert.Single(text.Translations));
        Assert.Equal("Temperature", text.Resolve("en"));
    }

    [Fact]
    public void WithAddsTranslationWithoutChangingTheOriginal()
    {
        var original = new LocalizedText("en", "Temperature");
        var translated = original.With("ru", "Температура");

        Assert.NotSame(original, translated);
        Assert.Single(original.Translations);
        Assert.False(original.Translations.ContainsKey("ru"));
        Assert.Equal("en", translated.DefaultLanguage);
        Assert.Equal(2, translated.Translations.Count);
        Assert.Equal("Temperature", translated.Resolve("en"));
        Assert.Equal("Температура", translated.Resolve("ru"));
    }

    [Fact]
    public void WithReplacesTranslationIgnoringCaseWithoutChangingTheOriginal()
    {
        var original = new LocalizedText("en", "Temperature").With("ru", "Темп.");
        var updated = original.With("RU", "Температура");

        Assert.Equal(2, updated.Translations.Count);
        Assert.Equal("Температура", updated.Resolve("ru"));
        Assert.Equal("Темп.", original.Resolve("ru"));
        Assert.Equal("en", updated.DefaultLanguage);
    }

    [Fact]
    public void ReplacingDefaultTranslationAlsoUpdatesFallback()
    {
        var original = new LocalizedText("en", "Old");
        var updated = original.With("EN", "New");

        Assert.Single(updated.Translations);
        Assert.Equal("en", updated.DefaultLanguage);
        Assert.Equal("New", updated.Resolve("vi"));
        Assert.Equal("Old", original.Resolve("vi"));
    }

    [Theory]
    [InlineData("ru")]
    [InlineData("en")]
    [InlineData("fr")]
    public void MissingLanguageFallsBackToThePluginsOwnLanguage(string language)
    {
        var text = new LocalizedText("vi", "Nhiệt độ");

        Assert.DoesNotContain("vi", GraviaLanguages.Supported);
        Assert.Equal("Nhiệt độ", text.Resolve(language));
    }

    [Fact]
    public void LanguageLookupIgnoresCaseIncludingRegionalCodes()
    {
        var text = new LocalizedText("en", "Temperature").With("pt-BR", "Temperatura");

        Assert.Equal("Temperature", text.Resolve("EN"));
        Assert.Equal("Temperatura", text.Resolve("PT-br"));
        Assert.Equal("Temperatura", text.Translations["PT-BR"]);
    }

    [Fact]
    public void RegionalVariantsAreNotInferred()
    {
        var text = new LocalizedText("vi", "Nhiệt độ")
            .With("en", "Temperature")
            .With("pt-BR", "Temperatura");

        Assert.Equal("Nhiệt độ", text.Resolve("en-US"));
        Assert.Equal("Nhiệt độ", text.Resolve("pt"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\"Нефть\"\nNhiệt độ\\path")]
    public void TextIsPreservedVerbatimIncludingEmptyTranslations(string value)
    {
        var text = new LocalizedText("en", value).With("ru", value);

        Assert.Equal(value, text.Resolve("en"));
        Assert.Equal(value, text.Resolve("ru"));
        Assert.Equal(value, text.Resolve("vi"));
    }

    [Fact]
    public void EmptyTranslationIsNotTreatedAsMissing()
    {
        var text = new LocalizedText("en", "Fallback").With("ru", "");

        Assert.Equal("", text.Resolve("ru"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void InvalidLanguageIsRejectedByAllOperations(string? language)
    {
        var text = new LocalizedText("en", "Temperature");

        Assert.ThrowsAny<ArgumentException>(() => new LocalizedText(language!, "Temperature"));
        Assert.ThrowsAny<ArgumentException>(() => text.With(language!, "Температура"));
        Assert.ThrowsAny<ArgumentException>(() => text.Resolve(language!));
        Assert.Single(text.Translations);
    }

    [Fact]
    public void NullTextIsRejected()
    {
        var text = new LocalizedText("en", "Temperature");

        Assert.Throws<ArgumentNullException>(() => new LocalizedText("en", null!));
        Assert.Throws<ArgumentNullException>(() => text.With("ru", null!));
        Assert.Single(text.Translations);
    }

    [Fact]
    public void TranslationsCannotBeMutatedThroughDictionaryInterfaces()
    {
        var text = new LocalizedText("en", "Temperature");
        var dictionary = Assert.IsAssignableFrom<IDictionary<string, string>>(text.Translations);

        Assert.Throws<NotSupportedException>(() => dictionary.Add("ru", "Температура"));
        Assert.Throws<NotSupportedException>(() => dictionary["en"] = "Changed");
        Assert.Throws<NotSupportedException>(() => dictionary.Remove("en"));
        Assert.Throws<NotSupportedException>(() => dictionary.Clear());
        Assert.Equal("Temperature", text.Resolve("en"));
    }
}
