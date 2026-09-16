using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Traits.Exceptions;
using Gravia.Domain.Traits.Interfaces;

namespace Gravia.Domain.Traits.Entities;

public sealed class ChoiceTrait<TValue> : IValidationTrait<TValue>
{
    public const string TraitType = "choice";

    private readonly ChoiceOption<TValue>[] _options;

    public string Type => TraitType;
    public IReadOnlyList<ChoiceOption<TValue>> Options => _options;

    public ChoiceTrait(params ChoiceOption<TValue>[] options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = [.. options];
    }

    public void Validate(TValue value)
    {
        if (_options.Any(option => EqualityComparer<TValue>.Default.Equals(option.Value, value)))
            return;

        throw new TraitValidationException(
            "value_not_in_choices",
            "The value is not one of the available choices.");
    }
}

public sealed record ChoiceOption<TValue>(TValue Value, LocalizedText Label);
