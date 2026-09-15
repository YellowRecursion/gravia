using Gravia.Application.Projects.Interfaces;
using Gravia.Application.Projects.Services;

namespace Gravia.Application.Tests.Projects;

internal sealed class UnusedProjectSessionFactory : IProjectSessionFactory
{
    public Task<ProjectSession> CreateAsync(string parentDirectory, string name, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This test does not open projects.");

    public Task<ProjectSession> OpenAsync(string projectPath, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This test does not open projects.");
}
