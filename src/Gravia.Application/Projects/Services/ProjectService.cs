using Gravia.Application.Projects.DTOs;
using Gravia.Application.Projects.Interfaces;
using Gravia.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gravia.Application.Projects.Services;

public sealed class ProjectService(
    IProjectSessionFactory factory,
    [FromKeyedServices(DataStoreKey.Application)] KeyValueStore applicationValues,
    ILogger<ProjectService> logger) : IHostedService
{
    public const string RecentProjectsKey = "gravia.recentProjects";
    private static readonly SemaphoreSlim LifecycleGate = new(1, 1);
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private bool _stopped;

    public ProjectDto? GetCurrent()
        => ProjectSession.Current is { } session ? ToDto(session) : null;

    public async Task<ProjectDto[]> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        var paths = await ReadRecentPathsAsync(cancellationToken);
        return paths.Select(path => new ProjectDto(new DirectoryInfo(path).Name, path)).ToArray();
    }

    public Task<ProjectDto> CreateAsync(string parentDirectory, string name, CancellationToken cancellationToken = default)
        => SwitchAsync(() => factory.CreateAsync(parentDirectory, name, cancellationToken), cancellationToken);

    public Task<ProjectDto> OpenAsync(string projectPath, CancellationToken cancellationToken = default)
        => SwitchAsync(() => factory.OpenAsync(projectPath, cancellationToken), cancellationToken);

    private async Task<ProjectDto> SwitchAsync(Func<Task<ProjectSession>> prepare, CancellationToken cancellationToken)
    {
        await LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_stopped, this);
            var next = await prepare();
            var previous = ProjectSession.Current;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (previous is not null)
                    await previous.Settings.FlushAsync(cancellationToken);
            }
            catch
            {
                await ReleaseAsync(next);
                throw;
            }

            // Opening the current directory is a no-op: retain its in-memory services.
            if (previous is not null && PathComparer.Equals(previous.ProjectPath, next.ProjectPath))
            {
                await ReleaseAsync(next);
                next = previous;
            }
            else
            {
                ProjectSession.SetCurrent(next);
                if (previous is not null) await ReleaseAsync(previous);
            }

            // The project is open. A history-write failure must not report a failed switch.
            try
            {
                var paths = await ReadRecentPathsAsync(CancellationToken.None);
                var recent = paths.Prepend(next.ProjectPath).Distinct(PathComparer).Take(5).ToArray();
                await applicationValues.SetAsync(RecentProjectsKey, recent);
            }
            catch (Exception error)
            {
                logger.LogError(error, "The project is open, but recent projects could not be saved.");
            }
            return ToDto(next);
        }
        finally { LifecycleGate.Release(); }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var paths = await ReadRecentPathsAsync(cancellationToken);
            if (paths.Length > 0) await OpenAsync(paths[0], cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogError(error, "The most recent project could not be opened. Choose a project to continue.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await LifecycleGate.WaitAsync(cancellationToken);
        try
        {
            _stopped = true;
            var session = ProjectSession.Current;
            ProjectSession.SetCurrent(null);
            if (session is not null) await ReleaseAsync(session);
        }
        finally { LifecycleGate.Release(); }
    }

    private async Task<string[]> ReadRecentPathsAsync(CancellationToken cancellationToken)
    {
        var stored = await applicationValues.GetAsync<string[]?>(RecentProjectsKey, [], cancellationToken) ?? [];
        List<string> paths = [];
        foreach (var path in stored)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) continue;
            try { paths.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))); }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { }
        }
        return paths.Distinct(PathComparer).Take(5).ToArray();
    }

    private async Task ReleaseAsync(ProjectSession session)
    {
        try { await session.DisposeAsync(); }
        catch (Exception error) { logger.LogError(error, "Failed to close project {ProjectPath}.", session.ProjectPath); }
    }

    private static ProjectDto ToDto(ProjectSession session) => new(session.Name, session.ProjectPath);
}
