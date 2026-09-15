using Gravia.Application.Projects.Services;

namespace Gravia.Application.Projects.Interfaces;

public interface IProjectSessionFactory
{
    Task<ProjectSession> CreateAsync(string parentDirectory, string name, CancellationToken cancellationToken = default);
    Task<ProjectSession> OpenAsync(string projectPath, CancellationToken cancellationToken = default);
}
