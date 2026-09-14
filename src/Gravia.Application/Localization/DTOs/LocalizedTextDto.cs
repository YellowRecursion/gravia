using Gravia.Domain.Localization.Entities;

namespace Gravia.Application.Localization.DTOs;

public sealed record LocalizedTextDto(IReadOnlyDictionary<string, string> Translations);

public static class LocalizedTextMapping
{
    public static LocalizedTextDto ToDto(this LocalizedText text) => new(text.Translations);
}
