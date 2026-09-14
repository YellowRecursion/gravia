using System.Collections.ObjectModel;
using Gravia.Application.Logging;
using Gravia.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gravia.Infrastructure.Tests.Logging;

public sealed class InMemoryLogJournalTests
{
    private static InMemoryLogJournal Create(int capacity = 4096, int maxEntry = 1024) =>
        new(Options.Create(new InMemoryLogOptions { CapacityBytes = capacity, MaxEntryBytes = maxEntry }), TimeProvider.System);

    private static LogMessage Message(string text) => new(LogLevel.Information, "Test.Model", text, new EventId(12, "Test"));

    [Fact]
    public void EmptyJournalAndClearHaveAuthoritativeBounds()
    {
        var journal = Create();
        var empty = journal.Read();
        Assert.True(empty.Reset);
        Assert.Empty(empty.Entries);
        Assert.Equal(1, empty.State.FirstSequence);
        Assert.Equal(0, empty.State.LastSequence);
        journal.TryWrite(Message("one"));
        var before = journal.Read();
        journal.Clear();
        var cleared = journal.Read(before.State.JournalId, before.State.LastSequence);
        Assert.False(cleared.Reset);
        Assert.Empty(cleared.Entries);
        Assert.True(cleared.State.Revision > before.State.Revision);
        Assert.Equal(2, cleared.State.FirstSequence);
        journal.TryWrite(Message("two"));
        Assert.Equal(2, Assert.Single(journal.Read(before.State.JournalId, 1).Entries).Sequence);
    }

    [Fact]
    public void UnicodeContextAndExceptionAreDetached()
    {
        var journal = Create();
        var properties = new Dictionary<string, string?> { ["NodeId"] = "old", ["Empty"] = null };
        journal.TryWrite(Message("Нефть 🛢️") with { Properties = properties, Exception = "trace\nline 2" });
        var entry = Assert.Single(journal.Read().Entries);
        properties["NodeId"] = "new";
        journal.Clear();
        Assert.Equal("Нефть 🛢️", entry.Message);
        Assert.Equal("old", entry.Properties["NodeId"]);
        Assert.Null(entry.Properties["Empty"]);
        Assert.Equal("trace\nline 2", entry.Exception);
        Assert.Equal(new EventId(12, "Test"), entry.EventId);
        Assert.IsType<ReadOnlyDictionary<string, string?>>(entry.Properties);
    }

    [Fact]
    public void EveryRepeatedMessageRemainsAnIndependentRecord()
    {
        var journal = Create(capacity: 65536);
        for (var i = 0; i < 600; i++) journal.TryWrite(Message("same"));
        var batch = journal.Read();
        Assert.Equal(600, batch.Entries.Count); // No implicit page of 100/500.
        Assert.Equal(Enumerable.Range(1, 600).Select(x => (long)x), batch.Entries.Select(x => x.Sequence));
    }

    [Fact]
    public void RingWrapAndOffsetIndexReturnExactlyTheUnreadSuffix()
    {
        var journal = Create();
        var expected = new Dictionary<long, string>();
        var id = journal.GetState().JournalId;
        var random = new Random(125);
        for (var i = 1; i <= 5000; i++)
        {
            var text = $"{i}: " + new string('я', random.Next(100));
            journal.TryWrite(Message(text));
            expected[i] = text;
            var state = journal.GetState();
            var after = random.Next(Math.Max(1, i - 100), i + 1);
            var batch = journal.Read(id, after);
            Assert.False(batch.Reset);
            var first = Math.Max(after + 1, state.FirstSequence);
            Assert.Equal(expected.Where(x => x.Key >= first).Select(x => x.Key), batch.Entries.Select(x => x.Sequence));
            Assert.All(batch.Entries, entry => Assert.Equal(expected[entry.Sequence], entry.Message));
            Assert.InRange(state.UsedBytes, 0, 4096);
        }
    }

    [Fact]
    public void ExactCapacityAndWrappedFullSnapshotAreReadable()
    {
        var message = Message(new string('a', 600));
        var measuring = Create();
        measuring.TryWrite(message);
        var size = measuring.GetState().UsedBytes;
        var journal = Create(size * 2);
        for (var i = 0; i < 5; i++) journal.TryWrite(message);
        var batch = journal.Read();
        Assert.Equal(size * 2, batch.State.UsedBytes);
        Assert.Equal(new long[] { 4, 5 }, batch.Entries.Select(x => x.Sequence));
        Assert.Equal(3, journal.GetStatistics().EvictedCount);
    }

    [Fact]
    public void DifferentProcessOrFutureCursorRequestsAFullReset()
    {
        var journal = Create();
        journal.TryWrite(Message("new"));
        Assert.True(journal.Read(Guid.NewGuid(), long.MaxValue).Reset);
        Assert.Single(journal.Read(journal.GetState().JournalId, long.MaxValue).Entries);
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.Read(afterSequence: -1));
    }

    [Fact]
    public void HugeRecordIsBoundedWithoutBreakingSurrogates()
    {
        var journal = Create(1024, 512);
        journal.TryWrite(Message(string.Concat(Enumerable.Repeat("🛢️Нефть", 10000)))
            with { Exception = new string('e', 100000), Properties = Enumerable.Range(0, 100)
                .ToDictionary(x => x.ToString(), _ => (string?)new string('語', 1000)) });
        var entry = Assert.Single(journal.Read().Entries);
        Assert.True(entry.IsTruncated);
        Assert.DoesNotContain("\uFFFD", entry.Message);
        Assert.InRange(journal.GetState().UsedBytes, 1, 512);
    }

    [Fact]
    public void ConcurrentReadersWritersAndClearProduceConsistentSnapshots()
    {
        var journal = Create(65536);
        Parallel.For(0, 20000, i =>
        {
            journal.TryWrite(Message($"message-{i}"));
            if (i % 100 == 0)
            {
                var batch = journal.Read();
                Assert.Equal(batch.State.EntryCount, batch.Entries.Count);
                Assert.All(batch.Entries, entry => Assert.InRange(entry.Sequence,
                    batch.State.FirstSequence, batch.State.LastSequence));
                Assert.Equal(batch.Entries.OrderBy(x => x.Sequence), batch.Entries);
            }
            if (i % 997 == 0) journal.Clear();
        });
        var stats = journal.GetStatistics();
        Assert.Equal(20000, stats.WrittenCount);
        Assert.Equal(stats.WrittenCount, stats.EntryCount + stats.EvictedCount + stats.ClearedCount);
    }

    [Fact]
    public void ChangeListenersRunOutsideTheLockAndCannotBreakLogging()
    {
        var journal = Create();
        var callbacks = 0;
        var outsideLock = true;
        journal.Changed += () => throw new InvalidOperationException();
        journal.Changed += () =>
        {
            callbacks++;
            outsideLock &= Task.Run(() => journal.GetState()).Wait(TimeSpan.FromSeconds(2));
        };
        Assert.True(journal.TryWrite(Message("one")));
        journal.Clear();
        // Assert outside the callback: the journal deliberately catches subscriber exceptions.
        Assert.Equal(2, callbacks);
        Assert.True(outsideLock);
    }

    [Theory]
    [InlineData(LogLevel.None)]
    [InlineData((LogLevel)99)]
    [InlineData((LogLevel)(-1))]
    public void InvalidLevelDoesNotConsumeSequence(LogLevel level)
    {
        var journal = Create();
        Assert.False(journal.TryWrite(Message("invalid") with { Level = level }));
        Assert.Equal(1, journal.GetStatistics().RejectedCount);
        Assert.Equal(0, journal.GetState().Revision);
    }

    [Fact]
    public void InvalidOptionsFailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(1023));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(maxEntry: 511));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(1024, 2048));
    }
}
