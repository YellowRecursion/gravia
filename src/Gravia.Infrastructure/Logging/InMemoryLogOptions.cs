namespace Gravia.Infrastructure.Logging;

public sealed class InMemoryLogOptions
{
    public const string SectionName = "InMemoryLogging";

    public int CapacityBytes { get; set; } = 8 * 1024 * 1024;
    public int MaxEntryBytes { get; set; } = 16 * 1024;

    internal void Validate()
    {
        if (CapacityBytes < 1024 || CapacityBytes > 256 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(CapacityBytes), "Use 1 KiB to 256 MiB.");
        if (MaxEntryBytes < 512 || MaxEntryBytes > CapacityBytes || MaxEntryBytes > 64 * 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxEntryBytes), "Use 512 bytes to min(capacity, 64 KiB).");
    }
}
