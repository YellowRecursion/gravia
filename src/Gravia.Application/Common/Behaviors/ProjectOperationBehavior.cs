using Gravia.Application.Projects.Commands;
using Gravia.Application.Projects.Services;
using MediatR;

namespace Gravia.Application.Common.Behaviors;

/// <summary>Keeps the current project stable for the entire request, including asynchronous work.</summary>
public sealed class ProjectOperationBehavior<TRequest, TResponse>(ProjectService projects)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Lifecycle operations acquire the same gate themselves, including startup and shutdown.
        if (request is OpenProjectCommand or CreateProjectCommand)
            return next(cancellationToken);

        return projects.RunAsync(() => next(cancellationToken), cancellationToken);
    }
}
