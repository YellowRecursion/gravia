using Gravia.Application.Storage;
using Gravia.Application.Projects.Interfaces;
using Gravia.Infrastructure.Projects;
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
        services.TryAddSingleton<IProjectSessionFactory, ProjectSessionFactory>();
        services.TryAddKeyedSingleton<IDataStore>(DataStoreKey.Application,
            (_, _) => new FileDataStore(ApplicationPaths.StorageDirectory));
        return services;
    }
}
