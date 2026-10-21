using System.Text.Json;
using Gravia.Application.Plugins.Interfaces;
using Gravia.Application.Storage;
using Gravia.Domain.Plugins.Entities;
using Microsoft.Extensions.Logging;
using Semver;

namespace Gravia.Application.Plugins.Services;

/// <summary>Owns one project's plugin registrations and activated instances. Does not own its data store.</summary>
public sealed class PluginManager : IAsyncDisposable
{
    public const string FileName = "plugins.json";

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    
    private readonly string _projectPath;
    private readonly IDataStore _dataStore;
    private readonly ILogger<PluginManager> _logger;
    private readonly IPluginFactory[] _factories;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<PluginController> _controllers = [];
    private readonly List<PluginController> _activationOrder = [];
    private readonly Lock _disposeLock = new();
    private Task? _disposal;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Controllers for the project's plugins. Presentation is built by query handlers.</summary>
    public IReadOnlyList<PluginController> Controllers => _controllers;

    public PluginManager(string projectPath, IDataStore dataStore, ILogger<PluginManager> logger,
        IEnumerable<IPluginFactory>? factories = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        if (!Path.IsPathFullyQualified(projectPath))
            throw new ArgumentException("The project path must be absolute.", nameof(projectPath));
        _projectPath = Path.GetFullPath(projectPath);
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _factories = factories?.ToArray() ?? [];
    }

    /// <summary>Restores connections and activates plugins. Individual failures are logged.</summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        if (_initialized) { await ActivateAllAsync(cancellationToken); return; }
        var registrations = await _dataStore.ReadAsync<PluginRegistration[]>(FileName, cancellationToken) ?? [];
        var pending = new List<PluginController>();
        foreach (var registration in registrations)
        {
            if (registration is null) throw new JsonException("A plugin registration must not be null.");
            var path = NormalizePath(registration.Path);
            if (pending.Any(entry => PathComparer.Equals(entry.Path, path)))
                throw new JsonException($"Duplicate plugin path '{path}'.");
            pending.Add(CreateController(path));
        }
        // All manifests must be known before activation: dependencies can appear later in the file.
        foreach (var entry in pending)
            await entry.TryLoadManifestAsync(cancellationToken);
        _controllers.AddRange(pending);
        _initialized = true;
        await ActivateAllAsync(cancellationToken);
    }, cancellationToken, requireInitialized: false);

    /// <summary>Saves a validated connection and attempts activation in dependency order.</summary>
    public Task AddAsync(string path, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        path = NormalizePath(path);
        if (_controllers.Any(entry => PathComparer.Equals(entry.Path, path)))
            throw new InvalidOperationException($"Plugin folder '{path}' is already connected.");
        var entry = CreateController(path);
        await entry.LoadManifestAsync(cancellationToken);
        EnsureUniqueId(entry.Manifest!.Id);
        await SaveAsync(_controllers.Append(entry), cancellationToken);
        _controllers.Add(entry);
        await ActivateAllAsync(cancellationToken);
    }, cancellationToken);

    public Task RemoveAsync(string path, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var entry = Find(path);
        EnsureNoDependents(entry);
        var wasLoaded = entry.IsLoaded;
        await StopAsync(entry, cancellationToken);
        try { await SaveAsync(_controllers.Where(item => item != entry), cancellationToken); }
        catch (Exception saveError)
        {
            if (wasLoaded)
            {
                _activationOrder.Add(entry);
                if (!await entry.TryActivateAsync(CancellationToken.None))
                    throw new AggregateException("Plugin removal failed and its activation could not be restored.",
                        saveError, new InvalidOperationException($"Failed to restore plugin '{entry.Path}'. See the log for details."));
            }
            throw;
        }
        _controllers.Remove(entry);
    }, cancellationToken);

    /// <summary>Reloads a manifest and restarts this plugin and its dependents.</summary>
    public Task ReloadAsync(string path, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var target = Find(path);
        var manifest = await target.ReadManifestAsync(cancellationToken);
        EnsureUniqueId(manifest.Id, target);
        var affected = new HashSet<PluginController> { target };
        while (true)
        {
            var added = _controllers.Where(entry => !affected.Contains(entry) &&
                affected.Any(dependency => DependsOn(entry, dependency))).ToArray();
            if (added.Length == 0) break;
            affected.UnionWith(added);
        }
        ValidateReload(target, manifest, affected);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var entry in _activationOrder.AsEnumerable().Reverse().Where(affected.Contains).ToArray())
            await StopAsync(entry, cancellationToken);
        target.Manifest = manifest;
        await ActivateAllAsync(cancellationToken);
    }, cancellationToken);

    private void ValidateReload(PluginController target, PluginManifest replacement, IEnumerable<PluginController> affected)
    {
        var visiting = new HashSet<PluginController>();
        var visited = new HashSet<PluginController>();
        foreach (var entry in affected) Visit(entry);

        PluginManifest ManifestOf(PluginController entry) => entry == target ? replacement :
            entry.Manifest ?? throw new InvalidOperationException($"Manifest unavailable for '{entry.Path}'.");

        void Visit(PluginController entry)
        {
            if (visited.Contains(entry)) return;
            if (!visiting.Add(entry)) throw new InvalidOperationException($"Cyclic plugin dependency at '{entry.Path}'.");
            var manifest = ManifestOf(entry);
            foreach (var (id, range) in manifest.Dependencies)
            {
                var dependency = _controllers.SingleOrDefault(item => (item == target ? replacement : item.Manifest)?.Id == id)
                    ?? throw new InvalidOperationException($"Plugin '{manifest.Id}' requires missing plugin '{id}'.");
                var version = ManifestOf(dependency).Version;
                if (!SemVersionRange.ParseNpm(range).Contains(SemVersion.Parse(version, SemVersionStyles.Strict)))
                    throw new InvalidOperationException($"Plugin '{id}' version {version} does not satisfy '{range}'.");
                Visit(dependency);
            }
            visiting.Remove(entry);
            visited.Add(entry);
        }
    }

    private async Task ActivateAllAsync(CancellationToken token)
    {
        var attempted = new HashSet<PluginController>();
        foreach (var entry in _controllers)
        {
            try { await ActivateAsync(entry, new HashSet<PluginController>(), attempted, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error) { _logger.LogWarning(error, "Cannot resolve dependencies for plugin {Path}.", entry.Path); }
        }
    }

    private async Task<bool> ActivateAsync(PluginController entry, HashSet<PluginController> visiting, HashSet<PluginController> attempted, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (entry.IsLoaded) return true;
        if (!visiting.Add(entry)) throw new InvalidOperationException($"Cyclic plugin dependency at '{entry.Path}'.");
        try
        {
            if (!attempted.Add(entry) || entry.Manifest is not { } manifest) return false;
            EnsureUniqueId(manifest.Id, entry);
            foreach (var (id, range) in manifest.Dependencies)
            {
                var dependency = _controllers.SingleOrDefault(item => item.Manifest?.Id == id)
                    ?? throw new InvalidOperationException($"Plugin '{manifest.Id}' requires missing plugin '{id}'.");
                if (!SemVersionRange.ParseNpm(range).Contains(SemVersion.Parse(dependency.Manifest!.Version, SemVersionStyles.Strict)))
                    throw new InvalidOperationException($"Plugin '{id}' version {dependency.Manifest.Version} does not satisfy '{range}'.");
                if (!await ActivateAsync(dependency, visiting, attempted, token)) return false;
            }
            // Track attempts too: a failed activation may still own resources after failed cleanup.
            _activationOrder.Remove(entry);
            _activationOrder.Add(entry);
            return await entry.TryActivateAsync(token);
        }
        finally { visiting.Remove(entry); }
    }

    private async Task StopAsync(PluginController entry, CancellationToken token)
    {
        await entry.DeactivateAsync(token);
        _activationOrder.Remove(entry);
    }

    private void EnsureNoDependents(PluginController target)
    {
        var dependent = _controllers.FirstOrDefault(entry => entry != target && DependsOn(entry, target));
        if (dependent is not null) throw new InvalidOperationException($"Remove dependent plugin '{dependent.Path}' first.");
    }

    private static bool DependsOn(PluginController entry, PluginController target) => target.Manifest is not null &&
        entry.Manifest?.Dependencies.ContainsKey(target.Manifest.Id) == true;

    private void EnsureUniqueId(string id, PluginController? except = null)
    {
        if (_controllers.Any(entry => entry != except && entry.Manifest?.Id == id))
            throw new InvalidOperationException($"Plugin ID '{id}' is already connected from another folder.");
    }

    private PluginController CreateController(string path) =>
        new(path, _projectPath, _dataStore, _factories, _logger);

    private Task SaveAsync(IEnumerable<PluginController> entries, CancellationToken token) => _dataStore.SaveAsync(FileName,
        entries.Select(entry => new PluginRegistration(entry.Path)).ToArray(), token);

    private PluginController Find(string path) => _controllers.FirstOrDefault(entry => PathComparer.Equals(entry.Path, NormalizePath(path)))
        ?? throw new KeyNotFoundException($"Plugin folder '{path}' is not connected.");

    private static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = path.Replace('\\', '/').TrimEnd('/');
        if (Path.IsPathRooted(path) || path.Contains(':') || path.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException("A plugin path must be a project-relative folder without traversal segments.", nameof(path));
        return path;
    }

    private async Task RunAsync(Func<Task> action, CancellationToken token, bool requireInitialized = true)
    {
        await _gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (requireInitialized && !_initialized) throw new InvalidOperationException("Initialize the plugin manager first.");
            await action();
        }
        finally { _gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            if (_disposal is null || _disposal.IsFaulted || _disposal.IsCanceled)
                _disposal = DisposeCoreAsync();
            return new ValueTask(_disposal);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _disposed = true;
            List<Exception> errors = [];
            foreach (var entry in _activationOrder.AsEnumerable().Reverse().ToArray())
            {
                try { await StopAsync(entry, CancellationToken.None); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 0) throw new AggregateException("Failed to deactivate plugins.", errors);
        }
        finally { _gate.Release(); }
    }
}
