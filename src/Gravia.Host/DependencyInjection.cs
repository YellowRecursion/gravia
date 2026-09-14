using ActualLab.Fusion;
using ActualLab.Fusion.Server;
using ActualLab.Rpc;
using Gravia.Application;
using Gravia.Infrastructure;
using Gravia.Host.Errors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gravia.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddHostServices(this IServiceCollection services)
    {
        services.AddApplicationServices();
        services.AddInfrastructureServices();
        services.AddFusion(RpcServiceMode.Server).AddWebServer();
        services.TryAddSingleton<ExceptionHandler>();

        return services;
    }
}
