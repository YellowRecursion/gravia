using Gravia.Domain.Plugins.Entities;

namespace Gravia.Application.Plugins.Services;

/// <summary>Base class for a plugin instance belonging to an open project.</summary>
public abstract class Plugin
{
    public PluginManifest Manifest { get; }

    /// <summary>Whether the plugin has registered its capabilities in Gravia.</summary>
    public bool IsLoaded { get; private set; }

    protected Plugin(PluginManifest manifest)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    /// <summary>Prepares the plugin for use and makes its capabilities available to Gravia.</summary>
    public async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        IsLoaded = false;
        await OnActivateAsync(cancellationToken);
        IsLoaded = true;
    }

    /// <summary>Withdraws the plugin's capabilities and releases resources owned by the plugin.</summary>
    public Task DeactivateAsync(CancellationToken cancellationToken = default)
    {
        IsLoaded = false;
        return OnDeactivateAsync(cancellationToken);
    }

    protected abstract Task OnActivateAsync(CancellationToken cancellationToken);
    protected abstract Task OnDeactivateAsync(CancellationToken cancellationToken);
}
