using System.Text.Json.Serialization;

namespace Gravia.Domain.Plugins.Entities;

/// <summary>A saved project connection. Paths are relative to the project directory.</summary>
public sealed record PluginRegistration(
    [property: JsonPropertyName("path")] string Path);
