using Gravia.Application.Plugins.Services;
using Gravia.Domain.Plugins.Entities;

namespace Gravia.Application.Plugins.Interfaces;

/// <summary>Creates an inactive plugin for a supported execution mechanism.</summary>
public interface IPluginFactory
{
    bool Supports(PluginExecution execution);

    /// <param name="directory">Absolute plugin directory.</param>
    /// <remarks>Creation must not activate the plugin or leave resources behind on failure.</remarks>
    Task<Plugin> CreateAsync(string directory, PluginManifest manifest, CancellationToken cancellationToken = default);
}
