using System.Collections.ObjectModel;
using Gravia.Application.Logging;
using Gravia.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gravia.Infrastructure.Tests.Logging;

public sealed class InMemoryLogJournalTests
{
    private static InMemoryLogJournal Create(int capacity = 4096, int maxEntry = 1024,
        bool collapse = false, int readBytes = 4096, int readEntries = 500, int scanEntries = 2048) =>
        new(Options.Create(new InMemoryLogOptions
        {
            CapacityBytes = capacity, MaxEntryBytes = maxEntry, CollapseConsecutive = collapse,
            MaxReadBytes = readBytes, MaxReadEntries = readEntries, MaxScanEntries = scanEntries
        }), new FixedTime());

    private static LogMessage Message(string text, LogLevel level = LogLevel.Information,
        IReadOnlyDictionary<string, string?>? properties = null) =>
        new(level, "Test.Model", text, new EventId(12, "TestEvent"), Properties: properties);

    [Fact]
    public void EmptyJournalHasStableIdentityAndZeroCounters()
    {
        var journal = Create();
        var page = journal.Read();
        Assert.Empty(page.Entries);
        Assert.Equal(journal.GetStatistics().JournalId, page.JournalId);
        Assert.False(page.HasMore);
        Assert.False(page.HasGap);
        Assert.Equal(0, page.NextSequence);
        Assert.Equal(0, journal.GetStatistics().UsedBytes);
    }

    [Fact]
    public void RoundTripsUnicodeContextAndException()
    {
        var journal = Create();
        var properties = new Dictionary<string, string?> { ["NodeId"] = "node-1", ["Empty"] = null };
        Assert.True(journal.TryWrite(Message("Нефть 🛢️: 12.5", properties: properties) with { Exception = "trace\nline 2" }));
        var entry = Assert.Single(journal.Read().Entries);
        Assert.Equal("Нефть 🛢️: 12.5", entry.Message);
        Assert.Equal("trace\nline 2", entry.Exception);
        Assert.Equal("node-1", entry.Properties["NodeId"]);
        Assert.Null(entry.Properties["Empty"]);
        Assert.Equal(new EventId(12, "TestEvent"), entry.EventId);
        Assert.Equal(FixedTime.Now, entry.Timestamp);
        Assert.Equal(1, entry.Sequence);
        Assert.Equal(1, entry.RepeatCount);
        Assert.False(entry.IsTruncated);
    }

    [Fact]
    public void SnapshotsAndInputsDoNotShareMutableState()
    {
        var journal = Create();
        var properties = new Dictionary<string, string?> { ["NodeId"] = "old" };
        journal.TryWrite(Message("one", properties: properties));
        var snapshot = journal.Read();
        properties["NodeId"] = "new";
        journal.Clear();
        journal.TryWrite(Message("two"));
        Assert.Equal("old", snapshot.Entries[0].Properties["NodeId"]);
        Assert.Equal("one", snapshot.Entries[0].Message);
        Assert.IsType<ReadOnlyDictionary<string, string?>>(snapshot.Entries[0].Properties);
    }

    [Fact]
    public void RingWrapsAndEvictsOldestWholeRecords()
    {
        var journal = Create();
        for (var i = 0; i < 1000; i++)
        {
            Assert.True(journal.TryWrite(Message($"{i:D4} — " + new string('я', i % 30))));
            Assert.InRange(journal.GetStatistics().UsedBytes, 1, 4096);
        }

        var page = journal.Read(new LogQuery { Limit = 500 });
        Assert.True(page.HasGap);
        Assert.False(page.HasMore);
        Assert.Equal(1000, page.NextSequence);
        Assert.Equal("0999 — " + new string('я', 9), page.Entries[^1].Message);
        Assert.Equal(1000, journal.GetStatistics().EvictedCount + page.Entries.Count);
    }

    [Fact]
    public void ExactCapacityFitsBeforeNextRecordEvicts()
    {
        var measuring = Create();
        var message = Message(new string('a', 600));
        measuring.TryWrite(message);
        var recordBytes = measuring.GetStatistics().UsedBytes;
        var journal = Create(capacity: recordBytes * 2);
        journal.TryWrite(message);
        journal.TryWrite(message);
        Assert.Equal(recordBytes * 2, journal.GetStatistics().UsedBytes);
        Assert.Equal(0, journal.GetStatistics().EvictedCount);
        journal.TryWrite(message);
        Assert.Equal(recordBytes * 2, journal.GetStatistics().UsedBytes);
        Assert.Equal(1, journal.GetStatistics().EvictedCount);
        Assert.Equal(new long[] { 2, 3 }, journal.Read().Entries.Select(entry => entry.Sequence));
    }

    [Fact]
    public void HugeRecordIsBoundedAndMarkedTruncated()
    {
        var journal = Create(capacity: 1024, maxEntry: 512);
        var properties = Enumerable.Range(0, 100).ToDictionary(i => $"Property{i}", _ => (string?)new string('語', 2000));
        journal.TryWrite(Message(string.Concat(Enumerable.Repeat("🛢️Нефть", 10000)), properties: properties)
            with { Category = new string('c', 10000), Exception = new string('e', 100000) });
        var entry = Assert.Single(journal.Read().Entries);
        Assert.True(entry.IsTruncated);
        Assert.InRange(journal.GetStatistics().UsedBytes, 1, 512);
        Assert.Equal(1, journal.GetStatistics().TruncatedCount);
        Assert.DoesNotContain("\uFFFD", entry.Message);
    }

    [Fact]
    public void ConsecutiveCollapseUpdatesExistingRowAndCursor()
    {
        var journal = Create(collapse: true);
        journal.TryWrite(Message("repeat"));
        var first = journal.Read();
        var bytes = journal.GetStatistics().UsedBytes;
        for (var i = 0; i < 100000; i++) journal.TryWrite(Message("repeat"));
        var update = journal.Read(new LogQuery { AfterSequence = first.NextSequence });
        var entry = Assert.Single(update.Entries);
        Assert.Equal(first.Entries[0].Sequence, entry.Sequence);
        Assert.Equal(100001, entry.RepeatCount);
        Assert.Equal(100001, entry.LastSequence);
        Assert.Equal(bytes, journal.GetStatistics().UsedBytes);
        Assert.False(update.HasGap);
    }

    [Fact]
    public void DifferentContextLevelEventAndTruncationDoNotCollapse()
    {
        var journal = Create(capacity: 16384, collapse: true);
        journal.TryWrite(Message("same"));
        journal.TryWrite(Message("same", LogLevel.Error));
        journal.TryWrite(Message("same", LogLevel.Error) with { EventId = new EventId(13) });
        journal.TryWrite(Message("same", LogLevel.Error, new Dictionary<string, string?> { ["NodeId"] = "B" }));
        journal.TryWrite(Message("same") with { IsTruncated = true });
        journal.TryWrite(Message("same") with { IsTruncated = true });
        Assert.Equal(6, journal.GetStatistics().EntryCount);
    }

    [Fact]
    public void CollapseNormalizesPropertyOrder()
    {
        var journal = Create(collapse: true);
        journal.TryWrite(Message("same", properties: new Dictionary<string, string?> { ["A"] = "1", ["B"] = "2" }));
        journal.TryWrite(Message("same", properties: new Dictionary<string, string?> { ["B"] = "2", ["A"] = "1" }));
        Assert.Equal(2, Assert.Single(journal.Read().Entries).RepeatCount);
    }

    [Fact]
    public void ClearKeepsMonotonicCursorAndReportsGap()
    {
        var journal = Create(collapse: true);
        journal.TryWrite(Message("first"));
        journal.TryWrite(Message("first"));
        journal.Clear();
        Assert.Empty(journal.Read().Entries);
        Assert.Equal(2, journal.Read().NextSequence);
        Assert.True(journal.Read().HasGap);
        journal.TryWrite(Message("new"));
        var page = journal.Read(new LogQuery { AfterSequence = 1 });
        Assert.Equal(3, Assert.Single(page.Entries).Sequence);
        Assert.True(page.HasGap);
        Assert.Equal(2, journal.GetStatistics().ClearedCount);
    }

    [Fact]
    public void ReadFiltersAndAdvancesPastNonMatchingEntries()
    {
        var journal = Create();
        journal.TryWrite(Message("other"));
        journal.TryWrite(Message("TARGET", LogLevel.Warning, new Dictionary<string, string?> { ["RunId"] = "r1" }));
        journal.TryWrite(Message("target", LogLevel.Error, new Dictionary<string, string?> { ["RunId"] = "r2" }));
        var page = journal.Read(new LogQuery
        {
            Category = "Test.Model", Text = "target", MinimumLevel = LogLevel.Warning,
            Properties = new Dictionary<string, string?> { ["RunId"] = "r1" }
        });
        Assert.Equal(2, Assert.Single(page.Entries).Sequence);
        Assert.Equal(3, page.NextSequence);
        Assert.False(page.HasMore);
        var empty = journal.Read(new LogQuery { Category = "missing" });
        Assert.Empty(empty.Entries);
        Assert.Equal(3, empty.NextSequence);
    }

    [Fact]
    public void PagesHaveBoundedSizeAndNoMissingRows()
    {
        var journal = Create(capacity: 65536, readBytes: 1024);
        for (var i = 0; i < 50; i++) journal.TryWrite(Message(i + new string('x', 300)));
        var sequences = new List<long>();
        long cursor = 0;
        LogPage page;
        do
        {
            page = journal.Read(new LogQuery { AfterSequence = cursor, Limit = 2 });
            Assert.InRange(page.Entries.Count, 1, 2);
            Assert.True(page.NextSequence > cursor);
            sequences.AddRange(page.Entries.Select(entry => entry.Sequence));
            cursor = page.NextSequence;
        } while (page.HasMore);
        Assert.Equal(Enumerable.Range(1, 50).Select(i => (long)i), sequences);
    }

    [Fact]
    public void ScanLimitReturnsEmptyPageWithProgressWhenFilterMatchesNothing()
    {
        var journal = Create(readEntries: 2, scanEntries: 2);
        for (var i = 0; i < 5; i++) journal.TryWrite(Message(i.ToString()));
        var page = journal.Read(new LogQuery { Category = "absent", Limit = 2 });
        Assert.Empty(page.Entries);
        Assert.Equal(2, page.NextSequence);
        Assert.True(page.HasMore);
        Assert.Equal(2, journal.Read().Entries.Count);
    }

    [Fact]
    public void ParallelWritersAndReadersRemainWithinBudget()
    {
        var journal = Create(capacity: 65536);
        Parallel.For(0, 100000, i =>
        {
            journal.TryWrite(Message($"source-{i % 20}: event-{i}"));
            if (i % 1000 == 0)
            {
                var page = journal.Read();
                Assert.Equal(page.Entries.OrderBy(entry => entry.LastSequence), page.Entries);
                Assert.InRange(journal.GetStatistics().UsedBytes, 0, 65536);
            }
        });
        var stats = journal.GetStatistics();
        Assert.Equal(100000, stats.WrittenCount);
        Assert.Equal(100000, stats.EvictedCount + stats.EntryCount);
        Assert.InRange(stats.UsedBytes, 1, 65536);
    }

    [Fact]
    public void CountersAccountForCollapsedEvictionsAndClear()
    {
        var journal = Create(capacity: 1024, collapse: true);
        for (var i = 0; i < 50; i++) journal.TryWrite(Message("same"));
        for (var i = 0; i < 50; i++) journal.TryWrite(Message(i + new string('x', 300)));
        journal.Clear();
        Assert.Equal(100, journal.GetStatistics().EvictedCount + journal.GetStatistics().ClearedCount);
    }

    [Theory]
    [InlineData(LogLevel.None)]
    [InlineData((LogLevel)99)]
    [InlineData((LogLevel)(-1))]
    public void InvalidLevelIsRejectedWithoutConsumingCursor(LogLevel level)
    {
        var journal = Create();
        Assert.False(journal.TryWrite(Message("invalid", level)));
        Assert.Equal(1, journal.GetStatistics().RejectedCount);
        Assert.Equal(0, journal.GetStatistics().LastSequence);
    }

    [Fact]
    public void InvalidQueriesFailExplicitly()
    {
        var journal = Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.Read(new LogQuery { Limit = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.Read(new LogQuery { Limit = 501 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.Read(new LogQuery { AfterSequence = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.Read(new LogQuery { AfterSequence = 1 }));
    }

    [Fact]
    public void InvalidCapacityAndPageSettingsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(capacity: 1023));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(maxEntry: 511));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(capacity: 1024, maxEntry: 2048));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(readBytes: 512));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(readEntries: 10, scanEntries: 9));
    }

    [Fact]
    public void NewJournalHasNoHistoryAndDifferentIdentity()
    {
        var first = Create();
        first.TryWrite(Message("old"));
        var second = Create();
        Assert.NotEqual(first.Read().JournalId, second.Read().JournalId);
        Assert.Empty(second.Read().Entries);
    }

    private sealed class FixedTime : TimeProvider
    {
        public static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

