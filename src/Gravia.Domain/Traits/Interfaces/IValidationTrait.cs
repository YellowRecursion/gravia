namespace Gravia.Domain.Traits.Interfaces;

/// <summary>A trait that validates candidate values before their owner applies them.</summary>
/// <typeparam name="TValue">The type of values accepted by the validator.</typeparam>
public interface IValidationTrait<in TValue> : ITrait
{
    /// <summary>Returns normally for an accepted value or throws when validation fails.</summary>
    /// <param name="value">The candidate value to validate.</param>
    /// <exception cref="Gravia.Domain.Traits.Exceptions.TraitValidationException">
    /// The candidate value violates the trait's rule.
    /// </exception>
    void Validate(TValue value);
}
