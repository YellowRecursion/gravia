using Gravia.Domain.Common;

namespace Gravia.Domain.CommonExceptions;

public sealed class NotFoundException(
    string message,
    string code = "not_found") : DomainException(code, message);
