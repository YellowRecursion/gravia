using Gravia.Domain.Common;

namespace Gravia.Domain.Traits.Exceptions;

public sealed class TraitValidationException(string code, string message)
    : DomainException(code, message);
