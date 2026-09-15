using Gravia.Application.Settings.Services;
using Gravia.Application.Storage;

namespace Gravia.Application.Projects.Services;

/// <summary>Owns the services of one open project directory.</summary>
public sealed class ProjectSession : IAsyncDisposable
{
    private static ProjectSession? _current;
    private readonly object _disposeLock = new();
    private Task? _disposal;

    public static ProjectSession? Current => Volatile.Read(ref _current);
    public string ProjectPath { get; }
    public string Name => new DirectoryInfo(ProjectPath).Name;
    public IDataStore DataStore { get; }
    public KeyValueStore KeyValueStore { get; }
    public ProjectSettings Settings { get; }

    public ProjectSession(string projectPath, IDataStore dataStore, ProjectSettings settings, KeyValueStore keyValueStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        if (!Path.IsPathFullyQualified(projectPath))
            throw new ArgumentException("The project path must be absolute.", nameof(projectPath));
        ProjectPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectPath));
        DataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        KeyValueStore = keyValueStore ?? throw new ArgumentNullException(nameof(keyValueStore));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    internal static void SetCurrent(ProjectSession? session) => Volatile.Write(ref _current, session);

    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
            return new ValueTask(_disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        Interlocked.CompareExchange(ref _current, null, this);
        List<Exception> errors = [];
        try { await Settings.FlushAsync(); }
        catch (Exception error) { errors.Add(error); }

        // Dependants finish before their shared store is released. Attempt every cleanup.
        foreach (var resource in new object[] { Settings, KeyValueStore, DataStore })
        {
            try
            {
                if (resource is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
                else if (resource is IDisposable disposable)
                    disposable.Dispose();
            }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count > 0) throw new AggregateException("Failed to release project resources.", errors);
    }
}
