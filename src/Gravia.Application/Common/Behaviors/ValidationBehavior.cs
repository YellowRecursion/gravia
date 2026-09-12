using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Gravia.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failures = new List<ValidationFailure>();

        // Validators may share scoped dependencies that cannot be used concurrently.
        foreach (var validator in validators)
        {
            var context = new ValidationContext<TRequest>(request);
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
