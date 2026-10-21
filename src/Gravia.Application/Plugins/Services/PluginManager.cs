using System.Text.Json;
using Gravia.Application.Plugins.DTOs;
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
    private readonly List<Entry> _entries = [];
    private readonly List<Entry> _activationOrder = [];
    private readonly object _disposeLock = new();
    private IReadOnlyList<PluginInfo> _plugins = Array.Empty<PluginInfo>();
    private Task? _disposal;
    private bool _initialized;
    private bool _disposed;

    public IReadOnlyList<PluginInfo> Plugins => Volatile.Read(ref _plugins);

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

    /// <summary>Restores connections and activates enabled plugins. Individual plugin failures remain visible in Plugins.</summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        if (_initialized) { await ActivateEnabledAsync(cancellationToken); return; }
        var registrations = await _dataStore.ReadAsync<PluginRegistration[]>(FileName, cancellationToken) ?? [];
        var pending = new List<Entry>();
        foreach (var registration in registrations)
        {
            if (registration is null) throw new JsonException("A plugin registration must not be null.");
            var path = NormalizePath(registration.Path);
            if (pending.Any(entry => PathComparer.Equals(entry.Path, path)))
                throw new JsonException($"Duplicate plugin path '{path}'.");
            pending.Add(new Entry(path, registration.Enabled));
        }
        // All manifests must be known before activation: dependencies can appear later in the file.
        foreach (var entry in pending)
        {
            try { entry.Manifest = await ReadManifestAsync(entry.Path, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error) { Fail(entry, error); }
        }
        _entries.AddRange(pending);
        _initialized = true;
        await ActivateEnabledAsync(cancellationToken);
    }, cancellationToken, requireInitialized: false);

    /// <summary>Adds a validated folder as a disabled connection. Does not start it.</summary>
    public Task AddAsync(string path, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        path = NormalizePath(path);
        if (_entries.Any(entry => PathComparer.Equals(entry.Path, path)))
            throw new InvalidOperationException($"Plugin folder '{path}' is already connected.");
        var manifest = await ReadManifestAsync(path, cancellationToken);
        EnsureUniqueId(manifest.Id);
        var entry = new Entry(path, false) { Manifest = manifest };
        await SaveAsync(_entries.Append(entry), cancellationToken);
        _entries.Add(entry);
    }, cancellationToken);

    /// <summary>Saves the desired enabled state. Activation failures are reported through Plugins.</summary>
    public Task SetEnabledAsync(string path, bool enabled, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var entry = Find(path);
        if (!enabled) EnsureNoEnabledDependents(entry);
        var registrations = _entries.Select(item => new PluginRegistration(item.Path, item == entry ? enabled : item.Enabled)).ToArray();
        await _dataStore.SaveAsync(FileName, registrations, cancellationToken);
        entry.Enabled = enabled;
        if (enabled) await ActivateEnabledAsync(cancellationToken);
        else await StopAsync(entry, cancellationToken);
    }, cancellationToken);

    public Task RemoveAsync(string path, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var entry = Find(path);
        EnsureNoEnabledDependents(entry);
        await StopAsync(entry, cancellationToken);
        await SaveAsync(_entries.Where(item => item != entry), cancellationToken);
        _entries.Remove(entry);
    }, cancellationToken);

    /// <summary>Reloads a manifest and restarts this plugin and its enabled dependents.</summary>
    public Task ReloadAsync(string path, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var target = Find(path);
        var affected = new HashSet<Entry> { target };
        while (true)
        {
            var added = _entries.Where(entry => entry.Enabled && !affected.Contains(entry) &&
                affected.Any(dependency => DependsOn(entry, dependency))).ToArray();
            if (added.Length == 0) break;
            affected.UnionWith(added);
        }
        foreach (var entry in StopOrder().Where(affected.Contains).ToArray())
            await StopAsync(entry, cancellationToken);
        target.Manifest = null;
        target.Error = null;
        try
        {
            var manifest = await ReadManifestAsync(target.Path, cancellationToken);
            EnsureUniqueId(manifest.Id, target);
            target.Manifest = manifest;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) { Fail(target, error); }
        await ActivateEnabledAsync(cancellationToken);
    }, cancellationToken);

    private async Task ActivateEnabledAsync(CancellationToken token)
    {
        var attempted = new HashSet<Entry>();
        foreach (var entry in _entries.Where(entry => entry.Enabled))
        {
            try { await ActivateAsync(entry, new HashSet<Entry>(), attempted, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error) { Fail(entry, error); }
        }
    }

    private async Task ActivateAsync(Entry entry, HashSet<Entry> visiting, HashSet<Entry> attempted, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (entry.Active) return;
        if (!visiting.Add(entry)) throw new InvalidOperationException($"Cyclic plugin dependency at '{entry.Path}'.");
        try
        {
            if (!attempted.Add(entry)) throw new InvalidOperationException(entry.Error ?? $"Plugin '{entry.Path}' could not be activated.");
            var manifest = entry.Manifest ?? throw new InvalidOperationException(entry.Error ?? $"Manifest unavailable for '{entry.Path}'.");
            EnsureUniqueId(manifest.Id, entry);
            foreach (var (id, range) in manifest.Dependencies)
            {
                var dependency = _entries.SingleOrDefault(item => item.Manifest?.Id == id)
                    ?? throw new InvalidOperationException($"Plugin '{manifest.Id}' requires missing plugin '{id}'.");
                if (!dependency.Enabled)
                    throw new InvalidOperationException($"Plugin '{manifest.Id}' requires enabled plugin '{id}'.");
                if (!SemVersionRange.ParseNpm(range).Contains(SemVersion.Parse(dependency.Manifest!.Version, SemVersionStyles.Strict)))
                    throw new InvalidOperationException($"Plugin '{id}' version {dependency.Manifest.Version} does not satisfy '{range}'.");
                await ActivateAsync(dependency, visiting, attempted, token);
            }
            if (entry.Instance is not null) await StopAsync(entry, token);
            var factories = _factories.Where(factory => factory.Supports(manifest.Execution)).ToArray();
            if (factories.Length != 1)
                throw new NotSupportedException($"Expected one factory for '{manifest.Execution.GetType().Name}', found {factories.Length}.");
            entry.Instance = await factories[0].CreateAsync(Path.GetFullPath(entry.Path, _projectPath), manifest, token)
                ?? throw new InvalidOperationException("The plugin factory returned null.");
            _activationOrder.Add(entry);
            try { await entry.Instance.ActivateAsync(token); }
            catch
            {
                try { await StopAsync(entry, CancellationToken.None); }
                catch (Exception cleanup) { _logger.LogError(cleanup, "Failed to clean up plugin {Path} after activation failure.", entry.Path); }
                throw;
            }
            entry.Active = true;
            entry.Error = null;
        }
        catch (Exception error)
        {
            if (error is not OperationCanceledException || !token.IsCancellationRequested) Fail(entry, error);
            throw;
        }
        finally { visiting.Remove(entry); Publish(); }
    }

    private async Task StopAsync(Entry entry, CancellationToken token)
    {
        if (entry.Instance is null) return;
        try { await entry.Instance.DeactivateAsync(token); }
        catch (Exception error) { Fail(entry, error); throw; }
        entry.Instance = null;
        entry.Active = false;
        entry.Error = null;
        _activationOrder.Remove(entry);
    }

    private IEnumerable<Entry> StopOrder() => _activationOrder.AsEnumerable().Reverse()
        .Concat(_entries.Where(entry => entry.Instance is not null)).Distinct();

    private void EnsureNoEnabledDependents(Entry target)
    {
        var dependent = _entries.FirstOrDefault(entry => entry != target &&
            (entry.Enabled || entry.Instance is not null) && DependsOn(entry, target));
        if (dependent is not null) throw new InvalidOperationException($"Disable dependent plugin '{dependent.Path}' first.");
    }

    private static bool DependsOn(Entry entry, Entry target) => target.Manifest is not null &&
        entry.Manifest?.Dependencies.ContainsKey(target.Manifest.Id) == true;

    private void EnsureUniqueId(string id, Entry? except = null)
    {
        if (_entries.Any(entry => entry != except && entry.Manifest?.Id == id))
            throw new InvalidOperationException($"Plugin ID '{id}' is already connected from another folder.");
    }

    private async Task<PluginManifest> ReadManifestAsync(string path, CancellationToken token)
    {
        var data = await _dataStore.ReadBytesAsync($"{path}/{PluginManifest.FileName}", token)
            ?? throw new FileNotFoundException($"Plugin manifest not found in '{path}'.");
        return JsonSerializer.Deserialize<PluginManifest>(data, JsonSerializerOptions.Web)
            ?? throw new JsonException("A plugin manifest must not be null.");
    }

    private Task SaveAsync(IEnumerable<Entry> entries, CancellationToken token) => _dataStore.SaveAsync(FileName,
        entries.Select(entry => new PluginRegistration(entry.Path, entry.Enabled)).ToArray(), token);

    private Entry Find(string path) => _entries.FirstOrDefault(entry => PathComparer.Equals(entry.Path, NormalizePath(path)))
        ?? throw new KeyNotFoundException($"Plugin folder '{path}' is not connected.");

    private static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = path.Replace('\\', '/').TrimEnd('/');
        if (Path.IsPathRooted(path) || path.Contains(':') || path.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException("A plugin path must be a project-relative folder without traversal segments.", nameof(path));
        return path;
    }

    private void Fail(Entry entry, Exception error)
    {
        entry.Error = error.Message;
        _logger.LogWarning(error, "Plugin {Path} is unavailable.", entry.Path);
    }

    private void Publish() => Volatile.Write(ref _plugins, Array.AsReadOnly(_entries.Select(entry =>
        new PluginInfo(entry.Path, entry.Enabled, entry.Manifest, entry.Active, entry.Error)).ToArray()));

    private async Task RunAsync(Func<Task> action, CancellationToken token, bool requireInitialized = true)
    {
        await _gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (requireInitialized && !_initialized) throw new InvalidOperationException("Initialize the plugin manager first.");
            await action();
        }
        finally { Publish(); _gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeLock) return new ValueTask(_disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _disposed = true;
            List<Exception> errors = [];
            foreach (var entry in StopOrder().ToArray())
            {
                try { await StopAsync(entry, CancellationToken.None); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 0) throw new AggregateException("Failed to deactivate plugins.", errors);
        }
        finally { Publish(); _gate.Release(); }
    }

    private sealed class Entry(string path, bool enabled)
    {
        public string Path { get; } = path;
        public bool Enabled { get; set; } = enabled;
        public PluginManifest? Manifest { get; set; }
        public Plugin? Instance { get; set; }
        public bool Active { get; set; }
        public string? Error { get; set; }
    }
}
