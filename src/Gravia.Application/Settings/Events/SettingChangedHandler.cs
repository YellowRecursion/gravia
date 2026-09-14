using Gravia.Domain.Settings.Events;
using MediatR;

namespace Gravia.Application.Settings.Events;

/// <summary>Saves the setting through the storage that registered it.</summary>
public sealed class SettingChangedHandler : INotificationHandler<SettingChanged>
{
    public Task Handle(SettingChanged notification, CancellationToken cancellationToken)
        => SettingsStorage.FindOwner(notification.Setting)?.SaveAsync(notification.Setting, cancellationToken)
           ?? Task.CompletedTask;
}
