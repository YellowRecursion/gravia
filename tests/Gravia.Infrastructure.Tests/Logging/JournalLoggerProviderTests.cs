using Gravia.Application.Logging;
using Gravia.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gravia.Infrastructure.Tests.Logging;

public sealed class JournalLoggerProviderTests
{
    private static ServiceProvider CreateServices(Action<ILoggingBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddInMemoryLogJournal(options =>
        {
            options.CapacityBytes = 65536;
            options.MaxEntryBytes = 4096;
        });
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            configure?.Invoke(logging);
        });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void ILoggerCapturesStructuredArgumentsAndNestedScopes()
    {
        using var services = CreateServices();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Example.Model");
        using (logger.BeginScope(new Dictionary<string, object?> { ["RunId"] = "run", ["NodeId"] = "outer" }))
        using (logger.BeginScope(new Dictionary<string, object?> { ["NodeId"] = "inner" }))
        {
            logger.LogWarning(new EventId(42, "OutsideRange"), "Temperature is {Temperature}", 123.45);
        }
        logger.LogInformation("outside scope");
        var entries = services.GetRequiredService<ILogJournal>().Read().Entries;
        Assert.Equal("run", entries[0].Properties["RunId"]);
        Assert.Equal("inner", entries[0].Properties["NodeId"]);
        Assert.Equal("123.45", entries[0].Properties["Temperature"]);
        Assert.Equal("OutsideRange", entries[0].EventId.Name);
        Assert.Empty(entries[1].Properties);
    }

    [Fact]
    public void FiltersPreventFormatterInvocation()
    {
        using var services = CreateServices(logging => logging.AddFilter<JournalLoggerProvider>(null, LogLevel.Warning));
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Example.Model");
        Assert.False(logger.IsEnabled(LogLevel.Information));
        logger.Log(LogLevel.Information, default, "ignored", null,
            (_, _) => throw new InvalidOperationException("Must never format"));
        logger.LogWarning("visible");
        Assert.Equal("visible", Assert.Single(services.GetRequiredService<ILogJournal>().Read().Entries).Message);
    }

    [Fact]
    public async Task AsyncConcurrentScopesStayIsolated()
    {
        using var services = CreateServices();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Example.Model");
        await Task.WhenAll(Enumerable.Range(0, 20).Select(async index =>
        {
            using var scope = logger.BeginScope(new Dictionary<string, object?> { ["RunId"] = index });
            await Task.Yield();
            logger.LogInformation("Run {Index}", index);
        }));
        var entries = services.GetRequiredService<ILogJournal>().Read().Entries;
        Assert.Equal(20, entries.Count);
        Assert.All(entries, entry => Assert.Equal(entry.Properties["Index"], entry.Properties["RunId"]));
    }

    [Fact]
    public void ExceptionIsCapturedWithoutChangingCallerOutcome()
    {
        using var services = CreateServices();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Example.Model");
        try { throw new InvalidOperationException("test failure"); }
        catch (Exception error) { logger.LogError(error, "Could not finish"); }
        var entry = Assert.Single(services.GetRequiredService<ILogJournal>().Read().Entries);
        Assert.Equal("Could not finish", entry.Message);
        Assert.Contains("InvalidOperationException: test failure", entry.Exception);
        Assert.Contains(nameof(ExceptionIsCapturedWithoutChangingCallerOutcome), entry.Exception);
    }

    [Fact]
    public void BrokenFormatterBecomesSafeFallback()
    {
        using var services = CreateServices();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Example.Model");
        logger.Log(LogLevel.Error, default, "input", null, (_, _) => throw new Exception("secret"));
        var entry = Assert.Single(services.GetRequiredService<ILogJournal>().Read().Entries);
        Assert.Equal("LoggingFailure", entry.EventId.Name);
        Assert.DoesNotContain("secret", entry.Message);
    }

    [Fact]
    public void ScopeValuesAreCopiedNotRetained()
    {
        using var services = CreateServices();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Example.Model");
        var properties = new Dictionary<string, object?> { ["NodeId"] = "original" };
        using (logger.BeginScope(properties)) logger.LogInformation("message");
        properties["NodeId"] = "changed";
        Assert.Equal("original", Assert.Single(services.GetRequiredService<ILogJournal>().Read().Entries).Properties["NodeId"]);
    }

    [Fact]
    public void OverlongScopePropertiesAreMarkedAsTruncated()
    {
        using var services = CreateServices();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Example.Model");
        using (logger.BeginScope(new Dictionary<string, object?> { ["Large"] = new string('x', 10000) }))
            logger.LogInformation("message");
        var entry = Assert.Single(services.GetRequiredService<ILogJournal>().Read().Entries);
        Assert.True(entry.IsTruncated);
        Assert.Equal(256, entry.Properties["Large"]!.Length);
    }

    [Fact]
    public void RegistrationIsIdempotentAndJournalCanBeReplaced()
    {
        var services = new ServiceCollection();
        var replacement = new ReplacementJournal();
        services.AddSingleton<ILogJournal>(replacement);
        services.AddInfrastructureServices();
        services.AddInfrastructureServices();
        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<ILoggerProvider>().OfType<JournalLoggerProvider>());
        provider.GetRequiredService<ILoggerFactory>().CreateLogger("Replacement").LogWarning("one");
        Assert.Same(replacement, provider.GetRequiredService<ILogJournal>());
        Assert.Equal(1, replacement.Writes);
    }

    private sealed class ReplacementJournal : ILogJournal
    {
        public event Action? Changed { add { } remove { } }
        public int Writes { get; private set; }
        public bool TryWrite(LogMessage message) { Writes++; return true; }
        public LogBatch Read(Guid? journalId = null, long afterSequence = 0) => throw new NotSupportedException();
        public LogState GetState() => throw new NotSupportedException();
        public LogStatistics GetStatistics() => throw new NotSupportedException();
        public void Clear() { }
    }
}
