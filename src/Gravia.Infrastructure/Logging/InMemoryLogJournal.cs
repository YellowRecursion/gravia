using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;
using Gravia.Application.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gravia.Infrastructure.Logging;

/// <summary>
/// Fixed byte ring. Capacity includes framing and all stored text/context, not caller allocations
/// or detached read snapshots. No unbounded index, background queue, file, or subscriber collection.
/// </summary>
public sealed class InMemoryLogJournal : ILogJournal
{
    private const int HeaderSize = 44;
    private readonly object _gate = new();
    private readonly byte[] _buffer;
    private readonly byte[] _scratch;
    private readonly BoundedWriteStream _stream;
    private readonly BinaryWriter _writer;
    private readonly TimeProvider _time;
    private readonly int _maxReadBytes;
    private readonly int _maxReadEntries;
    private readonly int _maxScanEntries;
    private readonly bool _collapse;
    private readonly Guid _id = Guid.NewGuid();
    private int _head, _used, _count, _lastOffset, _lastLength;
    private long _sequence, _evicted, _cleared, _rejected, _truncated, _discardedThrough;

    public InMemoryLogJournal(IOptions<InMemoryLogOptions> options, TimeProvider time)
    {
        var value = options.Value;
        value.Validate();
        _buffer = new byte[value.CapacityBytes];
        _scratch = new byte[value.MaxEntryBytes - HeaderSize];
        _stream = new BoundedWriteStream(_scratch);
        _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
        _time = time;
        _maxReadBytes = value.MaxReadBytes;
        _maxReadEntries = value.MaxReadEntries;
        _maxScanEntries = value.MaxScanEntries;
        _collapse = value.CollapseConsecutive;
    }

    public bool TryWrite(LogMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Category);
        ArgumentNullException.ThrowIfNull(message.Message);
        if (message.Level is < LogLevel.Trace or >= LogLevel.None)
        {
            lock (_gate) _rejected++;
            return false;
        }

        lock (_gate)
        {
            var truncated = message.IsTruncated;
            var category = LogText.Limit(message.Category, 128, ref truncated);
            var name = LogText.Limit(message.EventId.Name ?? "", 128, ref truncated);
            var text = LogText.Limit(message.Message, 4096, ref truncated);
            var exception = LogText.Limit(message.Exception ?? "", 4096, ref truncated);
            var properties = new SortedDictionary<string, string?>(StringComparer.Ordinal);
            if (message.Properties is not null)
            {
                var visited = 0;
                foreach (var pair in message.Properties)
                {
                    if (++visited > 32) { truncated = true; break; }
                    var key = LogText.Limit(pair.Key, 64, ref truncated);
                    properties[key] = pair.Value is null ? null : LogText.Limit(pair.Value, 256, ref truncated);
                }
            }

            // Drop details progressively, preserving a bounded readable record even for huge input.
            while (true)
            {
                try
                {
                    _stream.Reset();
                    _writer.Write((byte)message.Level);
                    _writer.Write(message.EventId.Id);
                    _writer.Write(truncated);
                    _writer.Write(category);
                    _writer.Write(name);
                    _writer.Write(text);
                    _writer.Write(exception);
                    _writer.Write((byte)properties.Count);
                    foreach (var pair in properties)
                    {
                        _writer.Write(pair.Key);
                        _writer.Write(pair.Value is not null);
                        if (pair.Value is not null) _writer.Write(pair.Value);
                    }
                    _writer.Flush();
                    break;
                }
                catch (LogEntryTooLargeException)
                {
                    truncated = true;
                    if (exception.Length > 0)
                        exception = LogText.Limit(exception, exception.Length / 2, ref truncated);
                    else if (properties.Count > 0)
                        properties.Remove(properties.Keys.Last());
                    else if (text.Length > 0)
                        text = LogText.Limit(text, text.Length / 2, ref truncated);
                    else if (name.Length > 0)
                        name = LogText.Limit(name, name.Length / 2, ref truncated);
                    else
                        category = LogText.Limit(category, category.Length / 2, ref truncated);
                }
            }

            var length = HeaderSize + _stream.Written;
            var sequence = ++_sequence;
            var now = _time.GetUtcNow().UtcTicks;
            if (truncated) _truncated++;

            if (_collapse && _count > 0 && _lastLength == length && !truncated
                && PayloadEquals(_lastOffset + HeaderSize, _scratch.AsSpan(0, _stream.Written)))
            {
                var previous = ReadHeader(_lastOffset);
                WriteHeader(_lastOffset, previous with
                {
                    LastSequence = sequence, LastTicks = now, Repeats = previous.Repeats + 1
                });
                return true;
            }

            while (_buffer.Length - _used < length)
            {
                var oldest = ReadHeader(_head);
                _head = (_head + oldest.Length) % _buffer.Length;
                _used -= oldest.Length;
                _count--;
                _evicted += oldest.Repeats;
                _discardedThrough = oldest.LastSequence;
            }

            var offset = (_head + _used) % _buffer.Length;
            WriteHeader(offset, new Header(length, sequence, sequence, now, now, 1));
            CopyIn(offset + HeaderSize, _scratch.AsSpan(0, _stream.Written));
            _lastOffset = offset;
            _lastLength = length;
            _used += length;
            _count++;
            return true;
        }
    }

    public LogPage Read(LogQuery? query = null)
    {
        query ??= new LogQuery { Limit = Math.Min(100, _maxReadEntries) };
        if (query.AfterSequence < 0) throw new ArgumentOutOfRangeException(nameof(query.AfterSequence));
        if (query.Limit < 1 || query.Limit > _maxReadEntries) throw new ArgumentOutOfRangeException(nameof(query.Limit));
        if (query.MinimumLevel is < LogLevel.Trace or > LogLevel.None) throw new ArgumentOutOfRangeException(nameof(query.MinimumLevel));
        if (query.Properties?.Count > 32) throw new ArgumentException("At most 32 property filters.", nameof(query));

        lock (_gate)
        {
            if (query.AfterSequence > _sequence)
                throw new ArgumentOutOfRangeException(nameof(query.AfterSequence), "Cursor belongs to a different journal or is in the future.");

            var entries = new List<LogEntry>();
            var next = Math.Max(query.AfterSequence, _discardedThrough);
            var offset = _head;
            var returnedBytes = 0;
            var scannedBytes = 0;
            var scanned = 0;
            for (var index = 0; index < _count; index++)
            {
                var header = ReadHeader(offset);
                var payloadOffset = offset + HeaderSize;
                offset = (offset + header.Length) % _buffer.Length;
                if (header.LastSequence <= query.AfterSequence) continue;
                if (scanned >= _maxScanEntries || entries.Count >= query.Limit
                    || returnedBytes + header.Length > _maxReadBytes
                    || scannedBytes + header.Length > _maxReadBytes) break;
                scanned++;
                scannedBytes += header.Length;
                CopyOut(payloadOffset, _scratch.AsSpan(0, header.Length - HeaderSize));
                var entry = Decode(header);
                next = header.LastSequence;
                if (!Matches(entry, query)) continue;
                entries.Add(entry);
                returnedBytes += header.Length;
            }

            return new LogPage(_id, entries.AsReadOnly(), next, next < _sequence, query.AfterSequence < _discardedThrough);
        }
    }

    public LogStatistics GetStatistics()
    {
        lock (_gate)
            return new(_id, _buffer.Length, _used, _count, _sequence, _evicted, _cleared,
                _rejected, _truncated, _sequence);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _cleared += _sequence - _discardedThrough;
            _discardedThrough = _sequence;
            _head = _used = _count = _lastLength = 0;
            Array.Clear(_buffer);
            Array.Clear(_scratch);
        }
    }

    private LogEntry Decode(Header header)
    {
        using var stream = new MemoryStream(_scratch, 0, header.Length - HeaderSize, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        var level = (LogLevel)reader.ReadByte();
        var eventId = reader.ReadInt32();
        var truncated = reader.ReadBoolean();
        var category = reader.ReadString();
        var name = reader.ReadString();
        var message = reader.ReadString();
        var exception = reader.ReadString();
        var count = reader.ReadByte();
        var properties = new Dictionary<string, string?>(count, StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var key = reader.ReadString();
            properties.Add(key, reader.ReadBoolean() ? reader.ReadString() : null);
        }

        return new(header.FirstSequence, header.LastSequence,
            new DateTimeOffset(header.FirstTicks, TimeSpan.Zero), new DateTimeOffset(header.LastTicks, TimeSpan.Zero),
            header.Repeats, level, category, message, new EventId(eventId, name.Length == 0 ? null : name),
            exception.Length == 0 ? null : exception, new ReadOnlyDictionary<string, string?>(properties), truncated);
    }

    private static bool Matches(LogEntry entry, LogQuery query) =>
        entry.Level >= query.MinimumLevel
        && (query.Category is null || entry.Category == query.Category)
        && (query.Text is null || entry.Message.Contains(query.Text, StringComparison.OrdinalIgnoreCase)
            || (entry.Exception?.Contains(query.Text, StringComparison.OrdinalIgnoreCase) ?? false))
        && (query.Properties is null || query.Properties.All(pair =>
            entry.Properties.TryGetValue(pair.Key, out var value) && value == pair.Value));

    private bool PayloadEquals(int offset, ReadOnlySpan<byte> data)
    {
        offset %= _buffer.Length;
        var first = Math.Min(data.Length, _buffer.Length - offset);
        return data[..first].SequenceEqual(_buffer.AsSpan(offset, first))
            && data[first..].SequenceEqual(_buffer.AsSpan(0, data.Length - first));
    }

    private void CopyIn(int offset, ReadOnlySpan<byte> data)
    {
        offset %= _buffer.Length;
        var first = Math.Min(data.Length, _buffer.Length - offset);
        data[..first].CopyTo(_buffer.AsSpan(offset));
        data[first..].CopyTo(_buffer);
    }

    private void CopyOut(int offset, Span<byte> destination)
    {
        offset %= _buffer.Length;
        var first = Math.Min(destination.Length, _buffer.Length - offset);
        _buffer.AsSpan(offset, first).CopyTo(destination);
        _buffer.AsSpan(0, destination.Length - first).CopyTo(destination[first..]);
    }

    private Header ReadHeader(int offset)
    {
        Span<byte> bytes = stackalloc byte[HeaderSize];
        CopyOut(offset, bytes);
        return new(BinaryPrimitives.ReadInt32LittleEndian(bytes),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[4..]), BinaryPrimitives.ReadInt64LittleEndian(bytes[12..]),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[20..]), BinaryPrimitives.ReadInt64LittleEndian(bytes[28..]),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[36..]));
    }

    private void WriteHeader(int offset, Header value)
    {
        Span<byte> bytes = stackalloc byte[HeaderSize];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value.Length);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[4..], value.FirstSequence);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[12..], value.LastSequence);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[20..], value.FirstTicks);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[28..], value.LastTicks);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[36..], value.Repeats);
        CopyIn(offset, bytes);
    }

    private readonly record struct Header(int Length, long FirstSequence, long LastSequence,
        long FirstTicks, long LastTicks, long Repeats);
}
