using Gravia.Application.Storage;
using Gravia.Infrastructure.Logging;
using Gravia.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gravia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddInMemoryLogJournal();
        services.TryAddKeyedSingleton<IDataStore>(DataStoreKey.Application,
            (_, _) => new FileDataStore(ApplicationPaths.StorageDirectory));
        return services;
    }
}
