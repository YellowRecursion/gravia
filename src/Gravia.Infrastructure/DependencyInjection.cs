using Gravia.Application.Projects;
using Gravia.Infrastructure.Logging;
using Gravia.Infrastructure.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gravia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddInMemoryLogJournal();
        services.TryAddSingleton<IProjectStore, FileProjectStore>();
        return services;
    }
}
