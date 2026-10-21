using Gravia.Domain.Plugins.Entities;

namespace Gravia.Application.Plugins.Services;

/// <summary>Base class for a plugin instance belonging to an open project.</summary>
public abstract class Plugin
{
    public PluginManifest Manifest { get; }

    protected Plugin(PluginManifest manifest)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    /// <summary>Prepares the plugin for use and makes its capabilities available to Gravia.</summary>
    public abstract Task ActivateAsync(CancellationToken cancellationToken = default);

    /// <summary>Withdraws the plugin's capabilities and releases resources owned by the plugin.</summary>
    public abstract Task DeactivateAsync(CancellationToken cancellationToken = default);
}
