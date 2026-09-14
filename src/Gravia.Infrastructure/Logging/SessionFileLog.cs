using System.Text;
using System.Threading.Channels;
using Gravia.Application.Logging;

namespace Gravia.Infrastructure.Logging;

/// <summary>
/// One text file per process run, plus the immediately preceding run.
/// The worker alone owns the files. Queue overflow retains the newest records and reports loss.
/// </summary>
public sealed class SessionFileLog : IAsyncDisposable
{
    public const int FileCapacityBytes = 8 * 1024 * 1024;
    public const int TrimBytes = 1024 * 1024;
    private const int MaxRecordBytes = 64 * 1024;
    private readonly object _gate = new();
    private readonly Queue<byte[]> _queue = new();
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    private readonly int _queueCapacity;
    private readonly TextWriter _errorOutput;
    private readonly Action<LogMessage>? _reportDiagnostic;
    private readonly Task _worker;
    private int _queuedBytes;
    private bool _completed;
    private long _dropped;
    private string? _failure;
    public long DroppedCount { get { lock (_gate) return _dropped; } }
    public string? Failure => Volatile.Read(ref _failure);

    public SessionFileLog(string directory, TextWriter? errorOutput = null,
        int capacityBytes = FileCapacityBytes, int trimBytes = TrimBytes,
        int queueCapacityBytes = 1024 * 1024, Action<LogMessage>? reportDiagnostic = null)
    {
        if (capacityBytes < MaxRecordBytes || trimBytes < 1 || trimBytes >= capacityBytes)
            throw new ArgumentOutOfRangeException(nameof(capacityBytes));
        if (queueCapacityBytes < MaxRecordBytes)
            throw new ArgumentOutOfRangeException(nameof(queueCapacityBytes));
        directory = Path.GetFullPath(directory);
        _queueCapacity = queueCapacityBytes;
        _errorOutput = errorOutput ?? Console.Error;
        _reportDiagnostic = reportDiagnostic;
        _worker = Task.Run(() => Run(directory, capacityBytes, trimBytes));
    }

    public bool TryWrite(LogMessage message)
    {
        var record = Encode(message);
        lock (_gate)
        {
            if (_completed) return false;
            while (_queuedBytes + record.Length > _queueCapacity)
            {
                _queuedBytes -= _queue.Dequeue().Length;
                _dropped++;
            }
            _queue.Enqueue(record);
            _queuedBytes += record.Length;
            // Signal before releasing the gate: Dispose must not complete an empty channel
            // between accepting the last record and publishing its wake-up.
            _wake.Writer.TryWrite(true);
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate) _completed = true;
        _wake.Writer.TryComplete();
        await _worker.ConfigureAwait(false);
    }

    private byte[]? Take()
    {
        lock (_gate)
        {
            if (!_queue.TryDequeue(out var record)) return null;
            _queuedBytes -= record.Length;
            return record;
        }
    }

    private async Task Run(string directory, int capacity, int trim)
    {
        try
        {
            Directory.CreateDirectory(directory);
            // An auxiliary zero-byte lock prevents another process from rotating this session.
            // It is deliberately retained: unlinking a lock file creates a race on Unix.
            using var ownership = new FileStream(Path.Combine(directory, "Gravia.log.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var current = Path.Combine(directory, "Gravia.log");
            var previous = Path.Combine(directory, "Gravia-prev.log");
            var temporary = Path.Combine(directory, "Gravia.log.tmp");
            // Only our fixed scratch file, under exclusive ownership. An interrupted rewrite
            // leaves the last committed current file intact and can leave this disposable copy.
            File.Delete(temporary);
            if (File.Exists(current)) File.Move(current, previous, overwrite: true);
            var output = OpenCurrent(FileMode.CreateNew);
            var lengths = new Queue<int>();
            long reportedDropped = 0;
            try
            {
                await foreach (var _ in _wake.Reader.ReadAllAsync())
                {
                    while (Take() is { } record)
                    {
                        var dropped = DroppedCount;
                        if (dropped != reportedDropped)
                        {
                            var warning = new LogMessage(Microsoft.Extensions.Logging.LogLevel.Warning, "Gravia.Logging",
                                $"File logging queue overflow: {dropped - reportedDropped} records dropped.");
                            ReportDiagnostic(warning);
                            await Append(Encode(warning));
                            reportedDropped = dropped;
                        }
                        await Append(record);
                        // Flush each record to the OS: no artificial timer, and a continuously busy queue
                        // must not postpone flushing until shutdown. This is not a power-loss guarantee.
                        await output.FlushAsync();
                    }
                }
                await output.FlushAsync();
            }
            finally { await output.DisposeAsync(); }

            FileStream OpenCurrent(FileMode mode) => new(current, mode, FileAccess.ReadWrite,
                FileShare.Read, 4096, FileOptions.Asynchronous);

            async Task Append(byte[] record)
            {
                if (output.Length + record.Length > capacity)
                {
                    var remove = 0;
                    do { remove += lengths.Dequeue(); }
                    while (remove < trim || output.Length - remove + record.Length > capacity);
                    var remaining = new byte[checked((int)output.Length - remove)];
                    output.Position = remove;
                    await output.ReadExactlyAsync(remaining);
                    await using (var replacement = new FileStream(temporary, FileMode.Create,
                        FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                    {
                        await replacement.WriteAsync(remaining);
                        // This heavier operation is only for compaction, on the background worker.
                        replacement.Flush(flushToDisk: true);
                    }
                    await output.DisposeAsync();
                    File.Move(temporary, current, overwrite: true);
                    output = OpenCurrent(FileMode.Open);
                }
                output.Position = output.Length;
                await output.WriteAsync(record);
                lengths.Enqueue(record.Length);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Volatile.Write(ref _failure, error.Message);
            lock (_gate)
            {
                _completed = true;
                _dropped += _queue.Count;
                _queue.Clear();
                _queuedBytes = 0;
            }
            // Never ILogger: reporting sink failure must not feed the failing sink.
            ReportDiagnostic(new(Microsoft.Extensions.Logging.LogLevel.Error, "Gravia.Logging",
                $"File logging failed: {error.Message}"));
            try { await _errorOutput.WriteLineAsync($"Gravia file logging failed: {error.Message}"); }
            catch (Exception outputError) when (outputError is not OutOfMemoryException) { }
        }
    }

    private void ReportDiagnostic(LogMessage message)
    {
        // Production sends this directly to memory, never back into the file queue.
        try { _reportDiagnostic?.Invoke(message); }
        catch (Exception error) when (error is not OutOfMemoryException) { }
    }

    private static byte[] Encode(LogMessage message)
    {
        var truncated = message.IsTruncated;
        var text = new StringBuilder();
        text.Append(DateTimeOffset.UtcNow.ToString("O")).Append(' ')
            .Append(message.Level).Append(' ')
            .Append(LogText.Limit(message.Category, 128, ref truncated).Replace('\n', ' ').Replace('\r', ' '))
            .Append(" [").Append(message.EventId.Id).Append("] ")
            .Append(LogText.Limit(message.Message, 4096, ref truncated));
        if (message.EventId.Name is { } name)
            text.Append("\nEvent: ").Append(LogText.Limit(name, 128, ref truncated));
        if (message.Properties is { } properties)
        {
            var count = 0;
            foreach (var (key, value) in properties)
            {
                if (++count > 32) { truncated = true; break; }
                text.Append("\n").Append(LogText.Limit(key, 64, ref truncated)).Append('=')
                    .Append(value is null ? "null" : LogText.Limit(value, 256, ref truncated));
            }
        }
        if (message.Exception is { } exception)
            text.Append("\n").Append(LogText.Limit(exception, 32768, ref truncated));
        // A continuation line is indented; each record ends in a newline.
        var rendered = text.ToString().Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\n  ");
        // UTF-8 uses at most three bytes per UTF-16 code unit (a surrogate pair uses four).
        rendered = LogText.Limit(rendered, (MaxRecordBytes - 32) / 3, ref truncated);
        return Encoding.UTF8.GetBytes(rendered + (truncated ? "\n  [truncated]" : "") + "\n");
    }
}
