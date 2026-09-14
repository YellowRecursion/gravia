using Gravia.Application;
using Gravia.Application.Settings.Events;
using Gravia.Domain.Settings.Events;
using Gravia.Infrastructure;
using Gravia.Host.Errors;
using Gravia.Host.Fusion;
using Gravia.Host.Fusion.Settings;
using MediatR;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gravia.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddHostServices(this IServiceCollection services)
    {
        // Invalidate before persistence: a disk failure must not leave UI caches stale.
        services.AddTransient<INotificationHandler<SettingChanged>, ApplicationSettingsInvalidation>();
        services.AddTransient<INotificationHandler<SettingsRegistrationChanged>, ApplicationSettingsInvalidation>();
        services.AddApplicationServices();
        services.AddInfrastructureServices();
        services.AddFusionServices();
        services.TryAddSingleton<ExceptionHandler>();

        return services;
    }
}
