namespace Gravia.Infrastructure.Logging;

/// <summary>Reusable serialization scratch space; never expands its supplied array.</summary>
internal sealed class BoundedWriteStream(byte[] buffer) : Stream
{
    public int Written { get; private set; }
    public void Reset() => Written = 0;
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => Written;
    public override long Position { get => Written; set => throw new NotSupportedException(); }
    public override void Flush() { }

    public override void Write(byte[] source, int offset, int count) => Write(source.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> source)
    {
        if (source.Length > buffer.Length - Written)
            throw new LogEntryTooLargeException();
        source.CopyTo(buffer.AsSpan(Written));
        Written += source.Length;
    }

    public override void WriteByte(byte value)
    {
        if (Written == buffer.Length)
            throw new LogEntryTooLargeException();
        buffer[Written++] = value;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}

internal sealed class LogEntryTooLargeException : Exception;

