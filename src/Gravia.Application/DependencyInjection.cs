using FluentValidation;
using Gravia.Application.Common.Behaviors;
using Gravia.Application.Common.Events;
using Gravia.Application.Settings;
using Gravia.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gravia.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHostedService<DomainEventDispatcher>();
        services.TryAddSingleton<ApplicationSettings>();
        services.AddHostedService(provider => provider.GetRequiredService<ApplicationSettings>());
        services.TryAddKeyedSingleton<KeyValueStore>(DataStoreKey.Application,
            (provider, _) => new KeyValueStore(provider.GetRequiredKeyedService<IDataStore>(DataStoreKey.Application)));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        return services;
    }
}
