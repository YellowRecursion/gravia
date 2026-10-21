using System.Text.Json.Serialization;

namespace Gravia.Domain.Plugins.Entities;

/// <summary>Execution configuration shared by supported plugin loading mechanisms.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ProcessPluginExecution), "process")]
public abstract class PluginExecution
{
}
