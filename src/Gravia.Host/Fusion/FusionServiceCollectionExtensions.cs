using ActualLab.Fusion;
using ActualLab.Fusion.Server;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Gravia.Host.Fusion;

public static class FusionServiceCollectionExtensions
{
    public static IServiceCollection AddFusionServices(this IServiceCollection services)
    {
        var fusion = services.AddFusion(RpcServiceMode.Server);
        fusion.Rpc.AddMiddleware<RpcExceptionMiddleware>();
        fusion.AddWebServer();
        services.AddHostedService<Logging.LogJournalInvalidation>();

        var serviceTypes = typeof(FusionServiceCollectionExtensions).Assembly.DefinedTypes
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false })
            .Where(DirectlyImplementsComputeService)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var serviceType in serviceTypes)
            fusion.AddService(serviceType);

        services.AddSingleton(RpcWebSocketServerOptions.Default with
        { OriginValidator = RpcWebSocketServerOriginValidators.SameOrigin });

        return services;
    }

    private static bool DirectlyImplementsComputeService(Type type)
    {
        // Generated Fusion proxies inherit the marker; only source API classes declare it themselves.
        var inheritedInterfaces = type.BaseType?.GetInterfaces() ?? [];
        return type.GetInterfaces().Except(inheritedInterfaces).Contains(typeof(IComputeService));
    }
}
