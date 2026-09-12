namespace Gravia.Application.Logging;

public sealed record LogStatistics(
    Guid JournalId,
    int CapacityBytes,
    int UsedBytes,
    int EntryCount,
    long WrittenCount,
    long EvictedCount,
    long ClearedCount,
    long RejectedCount,
    long TruncatedCount,
    long LastSequence);

