using System.Text.RegularExpressions;
using Gravia.Domain.Localization.Entities;

namespace Gravia.Domain.Plugins.Entities;

/// <summary>Author-provided metadata and execution configuration from a plugin folder.</summary>
public sealed class PluginManifest
{
    public const string FileName = "plugin.json";

    private string _id = null!;
    private string _version = null!;
    private LocalizedText _name = null!;
    private PluginExecution _execution = null!;

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
            if (!VersionPattern.IsMatch(value))
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
    
    /// <summary>Describes how the plugin is executed; contains no running process or connection.</summary>
    public required PluginExecution Execution
    {
        get => _execution;
        init => _execution = value ?? throw new ArgumentNullException(nameof(Execution));
    }

    // SemVer's recommended pattern, using ASCII digits and strict .NET string anchors.
    // https://semver.org/#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
    private static readonly Regex VersionPattern = new(
        @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

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
