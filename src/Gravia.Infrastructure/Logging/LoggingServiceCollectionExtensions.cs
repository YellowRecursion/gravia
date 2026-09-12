using Gravia.Application.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Gravia.Infrastructure.Logging;

public static class LoggingServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryLogJournal(
        this IServiceCollection services, Action<InMemoryLogOptions>? configure = null)
    {
        services.AddOptions<InMemoryLogOptions>();
        if (configure is not null) services.Configure(configure);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<ILogJournal, InMemoryLogJournal>();
        services.AddLogging();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, JournalLoggerProvider>());
        return services;
    }
}

