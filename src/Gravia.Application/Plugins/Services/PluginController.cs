using System.Text.Json;
using Gravia.Application.Plugins.Interfaces;
using Gravia.Application.Storage;
using Gravia.Domain.Plugins.Entities;
using Microsoft.Extensions.Logging;

namespace Gravia.Application.Plugins.Services;

/// <summary>Manages the manifest and instance of one plugin. Operations are coordinated by PluginManager.</summary>
public sealed class PluginController
{
    private readonly string _directory;
    private readonly IDataStore _dataStore;
    private readonly IPluginFactory[] _factories;
    private readonly ILogger _logger;

    public string Path { get; }
    public PluginManifest? Manifest { get; internal set; }
    public Plugin? Plugin { get; private set; }

    internal bool IsLoaded => Plugin?.IsLoaded == true;

    internal PluginController(string path, string projectPath, IDataStore dataStore,
        IPluginFactory[] factories, ILogger logger)
    {
        Path = path;
        _directory = System.IO.Path.GetFullPath(path, projectPath);
        _dataStore = dataStore;
        _factories = factories;
        _logger = logger;
    }

    internal async Task LoadManifestAsync(CancellationToken token)
        => Manifest = await ReadManifestAsync(token);

    internal async Task<PluginManifest> ReadManifestAsync(CancellationToken token)
    {
        var data = await _dataStore.ReadBytesAsync($"{Path}/{PluginManifest.FileName}", token)
            ?? throw new FileNotFoundException($"Plugin manifest not found in '{Path}'.");
        return JsonSerializer.Deserialize<PluginManifest>(data, JsonSerializerOptions.Web)
            ?? throw new JsonException("A plugin manifest must not be null.");
    }

    internal async Task<bool> TryLoadManifestAsync(CancellationToken token)
    {
        try { await LoadManifestAsync(token); return true; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Failed to read manifest for plugin {Path}.", Path);
            return false;
        }
    }

    internal async Task<bool> TryActivateAsync(CancellationToken token)
    {
        try
        {
            if (IsLoaded) return true;
            var manifest = Manifest ?? throw new InvalidOperationException($"Manifest unavailable for '{Path}'.");
            await DeactivateAsync(token);
            var factories = _factories.Where(factory => factory.Supports(manifest.Execution)).ToArray();
            if (factories.Length != 1)
                throw new NotSupportedException($"Expected one factory for '{manifest.Execution.GetType().Name}', found {factories.Length}.");
            Plugin = await factories[0].CreateAsync(_directory, manifest, token)
                ?? throw new InvalidOperationException("The plugin factory returned null.");
            try { await Plugin.ActivateAsync(token); }
            catch
            {
                try { await DeactivateAsync(CancellationToken.None); }
                catch { /* Deactivation logged the cleanup failure; preserve the activation failure. */ }
                throw;
            }
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Failed to activate plugin {Path}.", Path);
            return false;
        }
    }

    internal async Task DeactivateAsync(CancellationToken token)
    {
        if (Plugin is null) return;
        try
        {
            await Plugin.DeactivateAsync(token);
            Plugin = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Failed to deactivate plugin {Path}.", Path);
            throw;
        }
    }
}
