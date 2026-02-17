using System.Diagnostics;

namespace FileSyncApp.Core.Services;

public class ThrottledStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _maxBytesPerSecond;
    private long _totalBytesRead;
    private readonly Stopwatch _stopwatch;

    public ThrottledStream(Stream baseStream, long maxBytesPerSecond)
    {
        _baseStream = baseStream;
        _maxBytesPerSecond = maxBytesPerSecond;
        _stopwatch = Stopwatch.StartNew();
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => _baseStream.CanWrite;
    public override long Length => _baseStream.Length;
    public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }

    public override void Flush() => _baseStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        Throttle(count);
        int read = _baseStream.Read(buffer, offset, count);
        _totalBytesRead += read;
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Throttle(count);
        int read = await _baseStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        _totalBytesRead += read;
        return read;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Throttle(count);
        _baseStream.Write(buffer, offset, count);
        _totalBytesRead += count;
    }

    private void Throttle(int count)
    {
        if (_maxBytesPerSecond <= 0) return;

        long expectedTimeMs = (_totalBytesRead + count) * 1000 / _maxBytesPerSecond;
        long actualTimeMs = _stopwatch.ElapsedMilliseconds;

        if (expectedTimeMs > actualTimeMs)
        {
            Thread.Sleep((int)(expectedTimeMs - actualTimeMs));
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
    public override void SetLength(long value) => _baseStream.SetLength(value);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _baseStream.Dispose();
        base.Dispose(disposing);
    }
}
