namespace Gravia.Application.Logging;

/// <summary>Authoritative bounds of the process-local journal, including clear and eviction.</summary>
public sealed record LogState(Guid JournalId, long Revision, long FirstSequence, long LastSequence,
    int EntryCount, int UsedBytes, int CapacityBytes);

/// <summary>Entries and bounds captured atomically. Reset replaces the client's entire history.</summary>
public sealed record LogBatch(LogState State, IReadOnlyList<LogEntry> Entries, bool Reset);
