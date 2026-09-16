using Gravia.Application;
using Gravia.Application.Projects.Services;
using Gravia.Infrastructure;
using Gravia.Host.Fusion;

namespace Gravia.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddHostServices(this IServiceCollection services)
    {
        services.AddSingleton<Desktop.LogFileActions>();
        services.AddApplicationServices(typeof(DependencyInjection).Assembly);
        services.AddHostedService(provider => provider.GetRequiredService<ProjectService>());
        services.AddInfrastructureServices();
        services.AddFusionServices();

        return services;
    }
}
