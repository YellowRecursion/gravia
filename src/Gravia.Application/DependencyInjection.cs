using System.Reflection;
using FluentValidation;
using Gravia.Application.Common.Behaviors;
using Gravia.Application.Common.Events;
using Gravia.Application.Projects.Services;
using Gravia.Application.Settings.Services;
using Gravia.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gravia.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        params Assembly[] additionalAssemblies)
    {
        services.TryAddSingleton<DomainEventDispatcher>();
        services.AddHostedService(provider => provider.GetRequiredService<DomainEventDispatcher>());
        services.TryAddSingleton<ApplicationSettings>();
        services.TryAddSingleton<ProjectService>();
        services.AddHostedService(provider => provider.GetRequiredService<ApplicationSettings>());
        services.TryAddKeyedSingleton<KeyValueStore>(DataStoreKey.Application,
            (provider, _) => new KeyValueStore(provider.GetRequiredKeyedService<IDataStore>(DataStoreKey.Application)));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblies(
                additionalAssemblies.Prepend(typeof(DependencyInjection).Assembly).Distinct().ToArray());
            configuration.NotificationPublisherType = typeof(DomainEventNotificationPublisher);
            configuration.AddOpenBehavior(typeof(ProjectOperationBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        return services;
    }
}
