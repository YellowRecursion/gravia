using Gravia.Application;
using Gravia.Application.Projects.Services;
using Gravia.Application.Settings.Events;
using Gravia.Domain.Settings.Events;
using Gravia.Infrastructure;
using Gravia.Host.Fusion;
using Gravia.Host.Fusion.Settings;
using MediatR;

namespace Gravia.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddHostServices(this IServiceCollection services)
    {
        // Invalidate before persistence: a disk failure must not leave UI caches stale.
        services.AddTransient<INotificationHandler<SettingChanged>, ApplicationSettingsInvalidation>();
        services.AddTransient<INotificationHandler<SettingsRegistrationChanged>, ApplicationSettingsInvalidation>();
        services.AddApplicationServices();
        services.AddHostedService(provider => provider.GetRequiredService<ProjectService>());
        services.AddInfrastructureServices();
        services.AddFusionServices();

        return services;
    }
}
