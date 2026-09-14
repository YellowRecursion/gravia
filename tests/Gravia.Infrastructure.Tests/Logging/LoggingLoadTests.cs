using System.Diagnostics;
using Gravia.Application.Logging;
using Gravia.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Gravia.Infrastructure.Tests.Logging;

public sealed class LoggingLoadTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Load")]
    public void TwoMillionMessagesStayInsideEightMiB()
    {
        const int capacity = 8 * 1024 * 1024;
        var journal = new InMemoryLogJournal(Options.Create(new InMemoryLogOptions
        {
            CapacityBytes = capacity
        }), TimeProvider.System);
        var contexts = Enumerable.Range(0, 20).Select(index =>
            (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
            {
                ["NodeId"] = $"node-{index}", ["RunId"] = "load-test"
            }).ToArray();

        var retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var timer = Stopwatch.StartNew();
        for (var i = 0; i < 2_000_000; i++)
        {
            Assert.True(journal.TryWrite(new LogMessage(LogLevel.Information, "Example.Plugin",
                $"Сообщение {i}: нефтяная модель 🛢️", Properties: contexts[i % 20])));
            if (i % 1000 == 0)
                Assert.InRange(journal.GetStatistics().UsedBytes, 1, capacity);
        }
        timer.Stop();
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        var retainedAfter = GC.GetTotalMemory(forceFullCollection: true);
        var stats = journal.GetStatistics();
        Assert.Equal(2_000_000, stats.WrittenCount);
        Assert.Equal(stats.WrittenCount, stats.EvictedCount + stats.EntryCount);
        Assert.InRange(stats.UsedBytes, 1, capacity);
        Assert.True(journal.Read().State.FirstSequence > 1);
        output.WriteLine($"Messages=2000000; CapacityBytes={capacity}; UsedBytes={stats.UsedBytes}; RetainedEntries={stats.EntryCount}; Evicted={stats.EvictedCount}");
        output.WriteLine($"ElapsedMs={timer.Elapsed.TotalMilliseconds:F1}; AllocatedBytes={allocated}; ManagedRetainedDeltaBytes={retainedAfter - retainedBefore}");
        output.WriteLine("Single diagnostic run, not a performance SLA. Allocation measurements are process-wide; the ring was allocated before the baseline.");
        GC.KeepAlive(journal);
    }
}
