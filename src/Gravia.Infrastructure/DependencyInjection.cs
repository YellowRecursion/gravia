using Gravia.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Gravia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddInMemoryLogJournal();
        return services;
    }
}
