using Gravia.Application.Settings.Services;
using Gravia.Domain.Common.Events;

namespace Gravia.Application.Settings.Events;

/// <summary>A setting definition was registered, replaced, or unregistered.</summary>
public sealed class SettingsRegistrationChanged(SettingsStorage storage, string settingId) : DomainEvent
{
    public SettingsStorage Storage { get; } = storage;
    public string SettingId { get; } = settingId;
}
