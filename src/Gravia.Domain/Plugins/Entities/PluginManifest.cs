using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Gravia.Domain.Localization.Entities;
using Semver;

namespace Gravia.Domain.Plugins.Entities;

/// <summary>Author-provided metadata and execution configuration from a plugin folder.</summary>
public sealed class PluginManifest
{
    public const string FileName = "plugin.json";

    private string _id = null!;
    private string _version = null!;
    private LocalizedText _name = null!;
    private PluginExecution _execution = new ProcessPluginExecution();
    private ImmutableDictionary<string, string> _dependencies = ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// A unique package name in the Reverse Domain Name format.
    /// </summary>
    /// <example>com.organization.product</example>
    public required string Id
    {
        get => _id;
        init
        {
            if (!IsValidId(value))
                throw new ArgumentException("Plugin ID must be a lowercase reverse-domain name with at least three segments.", nameof(Id));
            _id = value;
        }
    }

    /// <summary>
    /// The plugin version is in the SemVer format.
    /// </summary>
    /// <example>1.2.3</example>
    public required string Version
    {
        get => _version;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Version));
            if (!SemVersion.TryParse(value, SemVersionStyles.Strict, out _))
                throw new ArgumentException("Plugin version must follow Semantic Versioning 2.0.0.", nameof(Version));
            _version = value;
        }
    }

    /// <summary>The localized name shown in the plugin manager.</summary>
    public required LocalizedText Name
    {
        get => _name;
        init => _name = value ?? throw new ArgumentNullException(nameof(Name));
    }

    public LocalizedText? Description { get; init; }

    /// <summary>Required plugin IDs mapped to npm-style version ranges. Empty when there are no dependencies.</summary>
    /// <example>{"org.gravia.properties": "^1.2.3", "org.gravia.models": "1.x.x"}</example>
    public IReadOnlyDictionary<string, string> Dependencies
    {
        get => _dependencies;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Dependencies));
            var dependencies = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (var (id, range) in value)
            {
                if (!IsValidId(id))
                    throw new ArgumentException("Dependencies must contain valid plugin IDs.", nameof(Dependencies));
                if (string.IsNullOrWhiteSpace(range) || !SemVersionRange.TryParseNpm(range, out _))
                    throw new ArgumentException($"Dependency '{id}' must specify a valid npm-style version range.", nameof(Dependencies));
                dependencies.Add(id, range);
            }
            _dependencies = dependencies.ToImmutable();
        }
    }

    /// <summary>Describes execution. Defaults to a process with no launch command for manual connection.</summary>
    public PluginExecution Execution
    {
        get => _execution;
        init => _execution = value ?? throw new ArgumentNullException(nameof(Execution));
    }

    private static bool IsValidId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return Regex.IsMatch(
            id,
            @"\A[a-z][a-z0-9_-]*(?:\.[a-z][a-z0-9_-]*){2,}\z"
        );
    }
}
