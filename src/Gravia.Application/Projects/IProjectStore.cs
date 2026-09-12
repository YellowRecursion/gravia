using Gravia.Domain.Projects;

namespace Gravia.Application.Projects;

/// <summary>
/// Persistence boundary. Paths are normalized, absolute directories.
/// Expected input/storage validation failures use FluentValidation.ValidationException
/// with stable codes on directoryPath; unexpected failures and cancellation propagate.
/// </summary>
public interface IProjectStore
{
    /// <summary>Persists a new project without overwriting existing data. Returns after commit.</summary>
    Task CreateAsync(Project project, string directoryPath, CancellationToken cancellationToken);

    /// <summary>Reads and validates a project, preserving its identity and leaving files untouched.</summary>
    Task<Project> OpenAsync(string directoryPath, CancellationToken cancellationToken);
}
