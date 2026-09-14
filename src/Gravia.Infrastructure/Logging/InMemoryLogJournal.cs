using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;
using Gravia.Application.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gravia.Infrastructure.Logging;

/// <summary>
/// Fixed byte ring with a bounded offset table for O(1) lookup of the first unread record.
/// Capacity covers serialized records; index, scratch and detached snapshots are additional.
/// </summary>
public sealed class InMemoryLogJournal : ILogJournal
{
    private const int HeaderSize = 20;
    private readonly object _gate = new();
    private readonly byte[] _buffer;
    private readonly int[] _offsets;
    private readonly byte[] _scratch;
    private readonly BoundedWriteStream _stream;
    private readonly BinaryWriter _writer;
    private readonly TimeProvider _time;
    private readonly Guid _id = Guid.NewGuid();
    private int _head, _used, _count;
    private long _sequence, _revision, _evicted, _cleared, _rejected, _truncated;
    public event Action? Changed;

    public InMemoryLogJournal(IOptions<InMemoryLogOptions> options, TimeProvider time)
    {
        var value = options.Value;
        value.Validate();
        _buffer = new byte[value.CapacityBytes];
        // Live entries cannot collide: every record is larger than its header.
        _offsets = new int[value.CapacityBytes / HeaderSize];
        _scratch = new byte[value.MaxEntryBytes - HeaderSize];
        _stream = new BoundedWriteStream(_scratch);
        _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
        _time = time;
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
            var exception = LogText.Limit(message.Exception ?? "", 32768, ref truncated);
            var properties = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (message.Properties is not null)
            {
                var visited = 0;
                foreach (var pair in message.Properties)
                {
                    if (++visited > 32) { truncated = true; break; }
                    properties[LogText.Limit(pair.Key, 64, ref truncated)] =
                        pair.Value is null ? null : LogText.Limit(pair.Value, 256, ref truncated);
                }
            }
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
                    if (properties.Count > 0) properties.Remove(properties.Keys.Last());
                    else if (exception.Length > 0) exception = LogText.Limit(exception, exception.Length / 2, ref truncated);
                    else if (text.Length > 0) text = LogText.Limit(text, text.Length / 2, ref truncated);
                    else if (name.Length > 0) name = LogText.Limit(name, name.Length / 2, ref truncated);
                    else category = LogText.Limit(category, category.Length / 2, ref truncated);
                }
            }
            var length = HeaderSize + _stream.Written;
            while (_buffer.Length - _used < length)
            {
                var oldestLength = ReadLength(_head);
                _head = (_head + oldestLength) % _buffer.Length;
                _used -= oldestLength;
                _count--;
                _evicted++;
            }
            var sequence = ++_sequence;
            var offset = (_head + _used) % _buffer.Length;
            Span<byte> header = stackalloc byte[HeaderSize];
            BinaryPrimitives.WriteInt32LittleEndian(header, length);
            BinaryPrimitives.WriteInt64LittleEndian(header[4..], sequence);
            BinaryPrimitives.WriteInt64LittleEndian(header[12..], _time.GetUtcNow().UtcTicks);
            CopyIn(offset, header);
            CopyIn(offset + HeaderSize, _scratch.AsSpan(0, _stream.Written));
            _offsets[(int)(sequence % _offsets.Length)] = offset;
            _used += length;
            _count++;
            _revision++;
            if (truncated) _truncated++;
        }
        NotifyChanged();
        return true;
    }

    public LogBatch Read(Guid? journalId = null, long afterSequence = 0)
    {
        if (afterSequence < 0) throw new ArgumentOutOfRangeException(nameof(afterSequence));
        byte[] bytes;
        LogState state;
        bool reset;
        lock (_gate)
        {
            state = State();
            reset = journalId != _id || afterSequence > _sequence;
            var after = reset ? 0 : afterSequence;
            if (_count == 0 || after == _sequence)
                bytes = [];
            else
            {
                var first = Math.Max(state.FirstSequence, after + 1);
                var offset = _offsets[(int)(first % _offsets.Length)];
                var skippedBytes = (offset - _head + _buffer.Length) % _buffer.Length;
                bytes = new byte[_used - skippedBytes];
                CopyOut(offset, bytes);
            }
        }
        // Decode outside the writer lock.
        var entries = new List<LogEntry>();
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        while (stream.Position < stream.Length)
        {
            _ = reader.ReadInt32();
            var sequence = reader.ReadInt64();
            var timestamp = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
            var level = (LogLevel)reader.ReadByte();
            var eventId = reader.ReadInt32();
            var truncated = reader.ReadBoolean();
            var category = reader.ReadString();
            var name = reader.ReadString();
            var text = reader.ReadString();
            var exception = reader.ReadString();
            var count = reader.ReadByte();
            var properties = new Dictionary<string, string?>(count, StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
                properties.Add(reader.ReadString(), reader.ReadBoolean() ? reader.ReadString() : null);
            entries.Add(new(sequence, timestamp, level, category, text,
                new EventId(eventId, name.Length == 0 ? null : name),
                exception.Length == 0 ? null : exception,
                new ReadOnlyDictionary<string, string?>(properties), truncated));
        }
        return new(state, entries.AsReadOnly(), reset);
    }

    public LogState GetState() { lock (_gate) return State(); }
    private LogState State() => new(_id, _revision, _sequence - _count + 1, _sequence,
        _count, _used, _buffer.Length);

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
            _cleared += _count;
            _head = _used = _count = 0;
            _revision++;
            Array.Clear(_buffer);
            Array.Clear(_scratch);
        }
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        var handlers = Changed;
        if (handlers is null) return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception error) when (error is not OutOfMemoryException) { }
        }
    }

    private int ReadLength(int offset)
    {
        Span<byte> bytes = stackalloc byte[4];
        CopyOut(offset, bytes);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
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
}
