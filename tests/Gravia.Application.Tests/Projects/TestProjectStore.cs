using Gravia.Application.Projects;
using Gravia.Domain.Projects;

namespace Gravia.Application.Tests.Projects;

internal sealed class TestProjectStore : IProjectStore
{
    public static string DirectoryPath => Path.Combine(Path.GetTempPath(), "gravia-application-test");
    public Dictionary<string, Project> Projects { get; } = [];
    public int Calls { get; private set; }
    public CancellationToken LastToken { get; private set; }
    public Exception? Failure { get; set; }

    public Task CreateAsync(Project project, string directoryPath, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        Projects.Add(directoryPath, project);
        return Task.CompletedTask;
    }

    public Task<Project> OpenAsync(string directoryPath, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(Projects[directoryPath]);
    }

    private void Observe(CancellationToken token)
    {
        Calls++;
        LastToken = token;
        token.ThrowIfCancellationRequested();
        if (Failure is not null) throw Failure;
    }
}
