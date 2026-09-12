using Gravia.Application;
using Gravia.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Gravia.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddHostServices(this IServiceCollection services)
    {
        services.AddApplicationServices();
        services.AddInfrastructureServices();

        return services;
    }
}
