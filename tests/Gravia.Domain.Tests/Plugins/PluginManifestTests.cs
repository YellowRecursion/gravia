using System.Text.Json;
using System.Text.Json.Nodes;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Plugins.Entities;
using Semver;

namespace Gravia.Domain.Tests.Plugins;

public sealed class PluginManifestTests
{
    private const string ExampleJson = """
        {
          "id": "org.gravia.example",
          "version": "0.1.0-rc.1+build.001",
          "name": { "en": "Example", "ru": "Пример" },
          "description": "A plugin",
          "execution": {
            "type": "process",
            "command": "runtime/python.exe",
            "arguments": ["folder with spaces/main.py", "", "--name", "a\"b"]
          }
        }
        """;

    [Fact]
    public void ManifestRoundTripsWithProcessDiscriminatorAndLocalizedText()
    {
        var options = JsonSerializerOptions.Web;
        var manifest = JsonSerializer.Deserialize<PluginManifest>(ExampleJson, options)!;
        var process = Assert.IsType<ProcessPluginExecution>(manifest.Execution);
        Assert.Equal("org.gravia.example", manifest.Id);
        Assert.Equal("0.1.0-rc.1+build.001", manifest.Version);
        Assert.Equal("Пример", manifest.Name.Resolve("RU"));
        Assert.Equal("A plugin", manifest.Description!.Resolve("en"));
        Assert.Equal("runtime/python.exe", process.Command);
        Assert.Equal(["folder with spaces/main.py", "", "--name", "a\"b"], process.Arguments);

        var json = JsonSerializer.Serialize(manifest, options);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("process", document.RootElement.GetProperty("execution").GetProperty("type").GetString());
        Assert.Equal("Example", document.RootElement.GetProperty("name").GetProperty("en").GetString());
        var restored = JsonSerializer.Deserialize<PluginManifest>(json, options)!;
        Assert.Equal(process.Arguments, Assert.IsType<ProcessPluginExecution>(restored.Execution).Arguments);
    }

    [Fact]
    public void DefaultSerializerAlsoSupportsPolymorphicRoundTrip()
    {
        var original = CreateManifest();
        var restored = JsonSerializer.Deserialize<PluginManifest>(JsonSerializer.Serialize(original))!;
        Assert.IsType<ProcessPluginExecution>(restored.Execution);
        Assert.Equal("Example", restored.Name.Resolve("ru"));
        Assert.Null(restored.Description);
    }

    [Fact]
    public void ProcessArgumentsCanBeOmitted()
    {
        var manifest = ParseExample();
        manifest["execution"]!.AsObject().Remove("arguments");
        var restored = JsonSerializer.Deserialize<PluginManifest>(manifest.ToJsonString(), JsonSerializerOptions.Web)!;
        Assert.Empty(Assert.IsType<ProcessPluginExecution>(restored.Execution).Arguments);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("version")]
    [InlineData("name")]
    public void MissingRequiredManifestPropertyIsRejected(string property)
    {
        var manifest = ParseExample();
        manifest.Remove(property);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PluginManifest>(manifest.ToJsonString(), JsonSerializerOptions.Web));
    }

    [Theory]
    [InlineData("{\"type\":\"unknown\",\"command\":\"python\"}")]
    public void InvalidExecutionShapeIsRejected(string executionJson)
    {
        var manifest = ParseExample();
        manifest["execution"] = JsonNode.Parse(executionJson);
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<PluginManifest>(manifest.ToJsonString(), JsonSerializerOptions.Web));
    }

    [Fact]
    public void MissingExecutionDiscriminatorIsRejected()
    {
        var manifest = ParseExample();
        manifest["execution"]!.AsObject().Remove("type");
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Deserialize<PluginManifest>(manifest.ToJsonString(), JsonSerializerOptions.Web));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("version")]
    [InlineData("name")]
    [InlineData("execution")]
    public void ExplicitNullRequiredPropertyIsRejected(string property)
    {
        var manifest = ParseExample();
        manifest[property] = null;
        Assert.ThrowsAny<ArgumentException>(() => JsonSerializer.Deserialize<PluginManifest>(manifest.ToJsonString(), JsonSerializerOptions.Web));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("org.example")]
    [InlineData("Org.gravia.example")]
    [InlineData("org..example")]
    [InlineData("org.gravia.example\n")]
    public void InvalidIdIsRejected(string? id)
        => Assert.ThrowsAny<ArgumentException>(() => CreateManifest(id: id!));

    [Theory]
    [InlineData("0.0.0")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3-alpha.1+001")]
    [InlineData("1.2.3-0.a-1")]
    [InlineData("999999999999999999999999.2.3")]
    public void ValidSemanticVersionIsAccepted(string version)
        => Assert.Equal(version, CreateManifest(version: version).Version);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("v1.2.3")]
    [InlineData("01.2.3")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3-alpha..1")]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3\n")]
    [InlineData("1.2.٣")]
    public void InvalidSemanticVersionIsRejected(string? version)
        => Assert.ThrowsAny<ArgumentException>(() => CreateManifest(version: version!));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BlankProcessCommandIsRejected(string? command)
        => Assert.ThrowsAny<ArgumentException>(() => new ProcessPluginExecution { Command = command! });

    [Fact]
    public void NullRequiredValuesAndArgumentsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new PluginManifest
        {
            Id = "org.gravia.example", Version = "1.0.0", Name = null!,
            Execution = new ProcessPluginExecution { Command = "python" }
        });
        Assert.Throws<ArgumentNullException>(() => new ProcessPluginExecution { Command = "python", Arguments = null! });
        Assert.Throws<ArgumentException>(() => new ProcessPluginExecution { Command = "python", Arguments = [null!] });
    }

    private static JsonObject ParseExample() => JsonNode.Parse(ExampleJson)!.AsObject();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualProcessAcceptsMissingOrNullCommand(bool explicitNull)
    {
        var json = ParseExample();
        var execution = json["execution"]!.AsObject();
        if (explicitNull)
            execution["command"] = null;
        else
            execution.Remove("command");

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json.ToJsonString(), JsonSerializerOptions.Web)!;
        Assert.Null(Assert.IsType<ProcessPluginExecution>(manifest.Execution).Command);
        var serialized = JsonSerializer.Serialize(manifest, JsonSerializerOptions.Web);
        using var document = JsonDocument.Parse(serialized);
        var serializedExecution = document.RootElement.GetProperty("execution");
        Assert.Equal("process", serializedExecution.GetProperty("type").GetString());
        Assert.False(serializedExecution.TryGetProperty("command", out _));
        Assert.Null(Assert.IsType<ProcessPluginExecution>(
            JsonSerializer.Deserialize<PluginManifest>(serialized, JsonSerializerOptions.Web)!.Execution).Command);
    }

    [Fact]
    public void ExecutionDefaultsToManualProcess()
    {
        var manifest = new PluginManifest
        {
            Id = "org.gravia.example", Version = "1.0.0", Name = new LocalizedText("Example")
        };
        Assert.Null(Assert.IsType<ProcessPluginExecution>(manifest.Execution).Command);
        var json = JsonSerializer.Serialize(manifest);
        Assert.Null(Assert.IsType<ProcessPluginExecution>(JsonSerializer.Deserialize<PluginManifest>(json)!.Execution).Command);

        var source = ParseExample();
        source.Remove("execution");
        var restored = JsonSerializer.Deserialize<PluginManifest>(source.ToJsonString(), JsonSerializerOptions.Web)!;
        Assert.Null(Assert.IsType<ProcessPluginExecution>(restored.Execution).Command);
    }

    [Fact]
    public void ExecutionCannotBeSetToNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PluginManifest
        {
            Id = "org.gravia.example", Version = "1.0.0", Name = new LocalizedText("Example"), Execution = null!
        });
    }

    [Fact]
    public void DependenciesRoundTripAsIdToVersionRangeObject()
    {
        var json = ParseExample();
        json["dependencies"] = new JsonObject
        {
            ["org.gravia.properties"] = "^1.2.3",
            ["org.gravia.models"] = "1.x.x"
        };
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json.ToJsonString(), JsonSerializerOptions.Web)!;
        Assert.Equal("^1.2.3", manifest.Dependencies["org.gravia.properties"]);
        Assert.Equal("1.x.x", manifest.Dependencies["org.gravia.models"]);
        var restored = JsonSerializer.Deserialize<PluginManifest>(JsonSerializer.Serialize(manifest))!;
        Assert.Equal(manifest.Dependencies.OrderBy(pair => pair.Key), restored.Dependencies.OrderBy(pair => pair.Key));
    }

    [Fact]
    public void OmittedDependenciesDefaultToEmpty()
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(ExampleJson, JsonSerializerOptions.Web)!;
        Assert.Empty(manifest.Dependencies);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("Org.gravia.models")]
    public void InvalidDependencyIdsAreRejected(string? id)
        => Assert.Throws<ArgumentException>(() => new PluginManifest
        {
            Id = "org.gravia.example", Version = "1.0.0", Name = new LocalizedText("Example"),
            Execution = new ProcessPluginExecution { Command = "python" },
            Dependencies = new Dictionary<string, string> { [id!] = "^1.0.0" }
        });

    [Fact]
    public void NullDependenciesAreRejected()
        => Assert.Throws<ArgumentNullException>(() => new PluginManifest
        {
            Id = "org.gravia.example", Version = "1.0.0", Name = new LocalizedText("Example"),
            Execution = new ProcessPluginExecution { Command = "python" }, Dependencies = null!
        });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("^")]
    [InlineData("[1.0.0,2.0.0)")]
    public void InvalidDependencyVersionRangeIsRejected(string? range)
        => Assert.Throws<ArgumentException>(() => CreateManifest(dependencies:
            new Dictionary<string, string> { ["org.gravia.models"] = range! }));

    [Theory]
    [InlineData("^1.2.3", "1.2.2", false)]
    [InlineData("^1.2.3", "1.2.3", true)]
    [InlineData("^1.2.3", "1.9.0", true)]
    [InlineData("^1.2.3", "2.0.0", false)]
    [InlineData("^1.2.3", "1.3.0-beta.1", false)]
    [InlineData("~0.4.2", "0.4.9", true)]
    [InlineData("~0.4.2", "0.5.0", false)]
    [InlineData("^0.4.2", "0.5.0", false)]
    [InlineData("^0.0.2", "0.0.3", false)]
    [InlineData("1.x.x", "1.0.0", true)]
    [InlineData("1.x.x", "2.0.0", false)]
    [InlineData("2.1.0", "2.1.0", true)]
    [InlineData("2.1.0", "2.1.1", false)]
    [InlineData(">=1.2.3 <2.0.0", "1.2.3", true)]
    [InlineData("1.2.3 || 2.1.0", "2.1.0", true)]
    [InlineData("*", "3.0.0", true)]
    [InlineData("1.2.3-beta.1", "1.2.3-beta.1", true)]
    [InlineData("2.1.0", "2.1.0+build.123", true)]
    public void DependencyRangesFollowNpmMatchingRules(string range, string version, bool matches)
    {
        var manifest = CreateManifest(dependencies: new Dictionary<string, string> { ["org.gravia.models"] = range });
        var parsed = SemVersionRange.ParseNpm(manifest.Dependencies["org.gravia.models"]);
        Assert.Equal(matches, parsed.Contains(SemVersion.Parse(version, SemVersionStyles.Strict)));
    }

    [Fact]
    public void DependenciesAreAnImmutableSnapshot()
    {
        var source = new Dictionary<string, string> { ["org.gravia.models"] = "^1.2.3" };
        var manifest = CreateManifest(dependencies: source);
        source["org.gravia.models"] = "2.0.0";
        source.Add("org.gravia.other", "*");
        Assert.Equal("^1.2.3", Assert.Single(manifest.Dependencies).Value);
        var dictionary = Assert.IsAssignableFrom<IDictionary<string, string>>(manifest.Dependencies);
        Assert.Throws<NotSupportedException>(() => dictionary["org.gravia.models"] = "invalid");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[\"org.gravia.models\"]")]
    [InlineData("{\"org.gravia.models\":42}")]
    public void InvalidDependenciesJsonShapeIsRejected(string dependenciesJson)
    {
        var manifest = ParseExample();
        manifest["dependencies"] = JsonNode.Parse(dependenciesJson);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PluginManifest>(manifest.ToJsonString(), JsonSerializerOptions.Web));
    }

    [Theory]
    [InlineData("{\"org.gravia.models\":null}")]
    [InlineData("{\"org.gravia.models\":\"invalid\"}")]
    public void InvalidDependencyRangeInJsonIsRejected(string dependenciesJson)
    {
        var manifest = ParseExample();
        manifest["dependencies"] = JsonNode.Parse(dependenciesJson);
        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<PluginManifest>(manifest.ToJsonString(), JsonSerializerOptions.Web));
    }

    private static PluginManifest CreateManifest(string id = "org.gravia.example", string version = "1.0.0",
        IReadOnlyDictionary<string, string>? dependencies = null) => new()
    {
        Id = id,
        Version = version,
        Name = new LocalizedText("Example"),
        Execution = new ProcessPluginExecution { Command = "python" },
        Dependencies = dependencies ?? new Dictionary<string, string>()
    };
}
