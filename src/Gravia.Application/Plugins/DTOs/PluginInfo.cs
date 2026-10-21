using Gravia.Domain.Plugins.Entities;

namespace Gravia.Application.Plugins.DTOs;

/// <summary>A snapshot of a project plugin, including load or activation failures.</summary>
public sealed record PluginInfo(string Path, bool Enabled, PluginManifest? Manifest, bool IsActive, string? Error);
