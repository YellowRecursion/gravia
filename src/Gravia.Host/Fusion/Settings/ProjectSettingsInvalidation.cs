using ActualLab.Fusion;
using Gravia.Application.Projects.Events;
using Gravia.Application.Projects.Services;
using Gravia.Application.Settings.Events;
using Gravia.Domain.Settings.Events;
using MediatR;

namespace Gravia.Host.Fusion.Settings;

public sealed class ProjectSettingsInvalidation(ProjectSettingsApi api)
    : INotificationHandler<SettingChanged>, INotificationHandler<SettingsRegistrationChanged>,
        INotificationHandler<CurrentProjectChanged>
{
    public Task Handle(SettingChanged notification, CancellationToken cancellationToken)
    {
        if (ProjectSession.Current?.Settings.Contains(notification.Setting) == true)
        {
            using var invalidation = Invalidation.Begin();
            _ = api.Get(notification.Setting.Id, CancellationToken.None);
        }
        return Task.CompletedTask;
    }

    public Task Handle(SettingsRegistrationChanged notification, CancellationToken cancellationToken)
    {
        if (ProjectSession.Current?.Settings == notification.Storage)
        {
            using var invalidation = Invalidation.Begin();
            _ = api.GetAll(CancellationToken.None);
            _ = api.Get(notification.SettingId, CancellationToken.None);
        }
        return Task.CompletedTask;
    }

    public Task Handle(CurrentProjectChanged notification, CancellationToken cancellationToken)
    {
        using var invalidation = Invalidation.Begin();
        _ = api.GetAll(CancellationToken.None);
        return Task.CompletedTask;
    }
}
