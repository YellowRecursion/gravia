namespace Gravia.Domain.Traits.Interfaces;

public interface IValidationTrait<in TValue> : ITrait
{
    void Validate(TValue value);
}
