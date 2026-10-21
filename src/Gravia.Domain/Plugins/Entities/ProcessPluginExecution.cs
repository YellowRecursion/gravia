using System.Text.Json.Serialization;

namespace Gravia.Domain.Plugins.Entities;

/// <summary>Describes a process launch. Relative paths are resolved from the plugin folder.</summary>
public sealed class ProcessPluginExecution : PluginExecution
{
    private string? _command;
    private string[] _arguments = [];

    /// <summary>An executable path or command name. When absent, the process is started manually.</summary>
    /// <example>runtime/python.exe — relative to the folder containing plugin.json.</example>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Command
    {
        get => _command;
        init
        {
            if (value is not null)
                ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Command));
            _command = value;
        }
    }

    /// <summary>Individual arguments, without shell quoting. The default is an empty list.</summary>
    public string[] Arguments
    {
        get => _arguments;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Arguments));
            if (value.Any(argument => argument is null))
                throw new ArgumentException("Process arguments must not contain null entries.", nameof(Arguments));
            _arguments = value;
        }
    }
}
