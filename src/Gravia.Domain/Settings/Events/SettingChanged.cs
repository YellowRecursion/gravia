using Gravia.Domain.Common.Events;
using Gravia.Domain.Settings.Interfaces;

namespace Gravia.Domain.Settings.Events;

/// <summary>Signals a value change and provides access to the setting's current state.</summary>
public sealed class SettingChanged(ISetting setting) : DomainEvent
{
    public ISetting Setting { get; } = setting;
}
