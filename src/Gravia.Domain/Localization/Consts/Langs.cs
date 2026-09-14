namespace Gravia.Domain.Localization.Consts;

/// <summary>Languages supported by the Gravia interface, not a restriction on plugin translations.</summary>
public static class Langs
{
    public const string Unknown = "-";
    
    public const string English = "en";
    public const string Russian = "ru";

    public static IReadOnlyList<string> Supported { get; } =
        Array.AsReadOnly([English, Russian]);
}
