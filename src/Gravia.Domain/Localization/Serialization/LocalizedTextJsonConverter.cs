using System.Text.Json;
using System.Text.Json.Serialization;
using Gravia.Domain.Localization.Consts;
using Gravia.Domain.Localization.Entities;

namespace Gravia.Domain.Localization.Serialization;

/// <summary>Stores language-neutral text as a string and translations as a language-to-text object.</summary>
public sealed class LocalizedTextJsonConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new LocalizedText(reader.GetString()!);

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Localized text must be a string or an object of translations.");

        LocalizedText? result = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return result ?? throw new JsonException("Localized text must contain at least one translation.");

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected a language code.");

            var language = reader.GetString()!;
            if (string.IsNullOrWhiteSpace(language))
                throw new JsonException("A language code must not be blank.");

            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                throw new JsonException("A translation must be a string.");

            var text = reader.GetString()!;
            result = result is null ? new LocalizedText(language, text) : result.With(language, text);
        }

        throw new JsonException("Incomplete localized text object.");
    }

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
    {
        if (value.Translations.Count == 1 && value.Translations.TryGetValue(Langs.Unknown, out var text))
        {
            writer.WriteStringValue(text);
            return;
        }

        writer.WriteStartObject();
        foreach (var translation in value.Translations)
            writer.WriteString(translation.Key, translation.Value);
        writer.WriteEndObject();
    }
}
