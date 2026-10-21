using System.Text.Json;
using System.Text.Json.Nodes;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Plugins.Entities;

namespace Gravia.Domain.Tests.Plugins;

public sealed class PluginManifestTests
{
    private const string ExampleJson = """
        {
          "id": "org.gravia.example",
          "version": "0.1.0-rc.1+build.001",
          "displayName": { "en": "Example", "ru": "Пример" },
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
        Assert.Equal("Example", document.RootElement.GetProperty("displayName").GetProperty("en").GetString());
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
    [InlineData("displayName")]
    [InlineData("execution")]
    public void MissingRequiredManifestPropertyIsRejected(string property)
    {
        var manifest = ParseExample();
        manifest.Remove(property);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PluginManifest>(manifest.ToJsonString(), JsonSerializerOptions.Web));
    }

    [Theory]
    [InlineData("{\"type\":\"unknown\",\"command\":\"python\"}")]
    [InlineData("{\"type\":\"process\"}")]
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
    [InlineData("displayName")]
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
    [InlineData(null)]
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
        Assert.Throws<ArgumentNullException>(() => new PluginManifest
        {
            Id = "org.gravia.example", Version = "1.0.0", Name = new LocalizedText("Example"),
            Execution = null!
        });
        Assert.Throws<ArgumentNullException>(() => new ProcessPluginExecution { Command = "python", Arguments = null! });
        Assert.Throws<ArgumentException>(() => new ProcessPluginExecution { Command = "python", Arguments = [null!] });
    }

    private static JsonObject ParseExample() => JsonNode.Parse(ExampleJson)!.AsObject();

    private static PluginManifest CreateManifest(string id = "org.gravia.example", string version = "1.0.0") => new()
    {
        Id = id,
        Version = version,
        Name = new LocalizedText("Example"),
        Execution = new ProcessPluginExecution { Command = "python" }
    };
}
