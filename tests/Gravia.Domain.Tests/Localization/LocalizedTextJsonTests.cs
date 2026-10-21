using System.Text.Json;
using Gravia.Domain.Localization.Consts;
using Gravia.Domain.Localization.Entities;

namespace Gravia.Domain.Tests.Localization;

public sealed class LocalizedTextJsonTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Temperature")]
    [InlineData("\"Нефть\"\nNhiệt độ\\path")]
    public void NeutralTextRoundTripsAsString(string value)
    {
        var json = JsonSerializer.Serialize(new LocalizedText(value));
        Assert.Equal(value, JsonDocument.Parse(json).RootElement.GetString());

        var restored = JsonSerializer.Deserialize<LocalizedText>(json)!;
        Assert.Equal(value, restored.Translations[Langs.Unknown]);
        Assert.Equal(value, restored.Resolve("ru"));
    }

    [Fact]
    public void TranslationsRoundTripAsFlatObject()
    {
        var text = new LocalizedText("en", "Temperature").With("ru", "Температура");
        var json = JsonSerializer.Serialize(text);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(2, document.RootElement.EnumerateObject().Count());
        Assert.Equal("Temperature", document.RootElement.GetProperty("en").GetString());
        Assert.Equal("Температура", document.RootElement.GetProperty("ru").GetString());

        var restored = JsonSerializer.Deserialize<LocalizedText>(json)!;
        Assert.Equal("Temperature", restored.Resolve("EN"));
        Assert.Equal("Температура", restored.Resolve("RU"));
    }

    [Fact]
    public void SingleExplicitLanguageIsPreservedAsObject()
    {
        var json = JsonSerializer.Serialize(new LocalizedText("vi", "Nhiệt độ"));
        Assert.Equal("Nhiệt độ", JsonDocument.Parse(json).RootElement.GetProperty("vi").GetString());
        Assert.Equal("Nhiệt độ", JsonSerializer.Deserialize<LocalizedText>(json)!.Translations["VI"]);
    }

    [Fact]
    public void NeutralTextWithTranslationPreservesBothEntries()
    {
        var text = new LocalizedText("Temperature").With("ru", "Температура");
        var restored = JsonSerializer.Deserialize<LocalizedText>(JsonSerializer.Serialize(text))!;
        Assert.Equal("Temperature", restored.Translations[Langs.Unknown]);
        Assert.Equal("Температура", restored.Translations["ru"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConverterWorksAutomaticallyForNestedValues(bool web)
    {
        var options = web ? JsonSerializerOptions.Web : JsonSerializerOptions.Default;
        var original = new Container(new LocalizedText("en", "Temperature"), new LocalizedText("Description"));
        var json = JsonSerializer.Serialize(original, options);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("Temperature", document.RootElement.GetProperty(web ? "title" : "Title").GetProperty("en").GetString());
        Assert.Equal("Description", document.RootElement.GetProperty(web ? "description" : "Description").GetString());

        var restored = JsonSerializer.Deserialize<Container>(json, options)!;
        Assert.Equal("Temperature", restored.Title.Resolve("en"));
        Assert.Equal("Description", restored.Description!.Resolve("ru"));
    }

    [Fact]
    public void NullRemainsNullForOptionalText()
    {
        Assert.Null(JsonSerializer.Deserialize<LocalizedText>("null"));
        Assert.Equal("null", JsonSerializer.Serialize<LocalizedText?>(null));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{\"en\":null}")]
    [InlineData("{\"en\":42}")]
    [InlineData("{\"en\":{}}")]
    [InlineData("{\" \":\"text\"}")]
    [InlineData("{\"en\":\"text\"")]
    public void InvalidTextIsRejected(string json)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LocalizedText>(json));

    public sealed record Container(LocalizedText Title, LocalizedText? Description);
}
