using System.Collections.Immutable;

namespace Gravia.Domain.Localization.Entities;

/// <summary>An immutable text with translations and a required default language.</summary>
public sealed class LocalizedText
{
    private readonly ImmutableDictionary<string, string> _translations;

    public string DefaultLanguage { get; }

    /// <summary>Available translations, indexed by case-insensitive language codes.</summary>
    public IReadOnlyDictionary<string, string> Translations => _translations;

    /// <summary>Creates a text with its default translation. Any language code is allowed.</summary>
    public LocalizedText(string language, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentNullException.ThrowIfNull(text);

        DefaultLanguage = language;
        _translations = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase)
            .Add(language, text);
    }

    private LocalizedText(string defaultLanguage, ImmutableDictionary<string, string> translations)
    {
        DefaultLanguage = defaultLanguage;
        _translations = translations;
    }

    /// <summary>Returns a new text with an added or replaced translation, preserving the default language.</summary>
    public LocalizedText With(string language, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentNullException.ThrowIfNull(text);

        return new LocalizedText(DefaultLanguage, _translations.SetItem(language, text));
    }

    /// <summary>Returns the requested translation, or the default translation if it is unavailable.</summary>
    /// <remarks>Language codes are matched exactly, ignoring case; regional variants are not inferred.</remarks>
    public string Resolve(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        return _translations.TryGetValue(language, out var text)
            ? text
            : _translations[DefaultLanguage];
    }
}
