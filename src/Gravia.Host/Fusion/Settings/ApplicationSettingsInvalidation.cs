using ActualLab.Fusion;
using Gravia.Application.Settings.Events;
using Gravia.Application.Settings.Services;
using Gravia.Domain.Settings.Events;
using MediatR;

namespace Gravia.Host.Fusion.Settings;

public sealed class ApplicationSettingsInvalidation(ApplicationSettings settings, ApplicationSettingsApi api)
    : INotificationHandler<SettingChanged>, INotificationHandler<SettingsRegistrationChanged>
{
    public Task Handle(SettingChanged notification, CancellationToken cancellationToken)
    {
        if (settings.Contains(notification.Setting))
        {
            using var invalidation = Invalidation.Begin();
            _ = api.Get(notification.Setting.Id, CancellationToken.None);
        }
        return Task.CompletedTask;
    }

    public Task Handle(SettingsRegistrationChanged notification, CancellationToken cancellationToken)
    {
        if (notification.Storage == settings)
        {
            using var invalidation = Invalidation.Begin();
            _ = api.GetAll(CancellationToken.None);
            _ = api.Get(notification.SettingId, CancellationToken.None);
        }
        return Task.CompletedTask;
    }
}
