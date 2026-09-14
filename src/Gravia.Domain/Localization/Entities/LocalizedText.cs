using System.Collections.Immutable;
using Gravia.Domain.Localization.Consts;

namespace Gravia.Domain.Localization.Entities;

/// <summary>An immutable text with translations and a required default language.</summary>
public sealed class LocalizedText
{
    private readonly ImmutableDictionary<string, string> _translations;

    /// <summary>Available translations, indexed by case-insensitive language codes.</summary>
    public IReadOnlyDictionary<string, string> Translations => _translations;

    /// <summary>Creates a text with its first translation. Any language code is allowed.</summary>
    public LocalizedText(string language, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentNullException.ThrowIfNull(text);
        
        _translations = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase)
            .Add(language, text);
    }
    
    /// <summary>Creates a text with unknown translation.</summary>
    public LocalizedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        
        _translations = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase)
            .Add(Langs.Unknown, text);
    }

    private LocalizedText(ImmutableDictionary<string, string> translations)
    {
        _translations = translations;
    }

    /// <summary>Returns a new text with an added or replaced translation, preserving the default language.</summary>
    public LocalizedText With(string language, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentNullException.ThrowIfNull(text);

        return new LocalizedText(_translations.SetItem(language, text));
    }
    
    public string Resolve(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        return _translations.TryGetValue(language, out var text)
            ? text
            : _translations.First().Value;
    }
}
