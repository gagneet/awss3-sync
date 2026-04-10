using System.Diagnostics;

namespace FileSyncApp.Core.Services;

/// <summary>
/// Stream wrapper that limits bandwidth using a token bucket algorithm
/// </summary>
public class ThrottledStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _maxBytesPerSecond;
    private readonly Stopwatch _stopwatch;
    private long _totalBytesTransferred;
    private readonly object _lock = new();

    /// <summary>
    /// Create a throttled stream
    /// </summary>
    /// <param name="baseStream">Underlying stream to wrap</param>
    /// <param name="maxBytesPerSecond">Maximum bytes per second (0 = unlimited)</param>
    public ThrottledStream(Stream baseStream, long maxBytesPerSecond = 0)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _maxBytesPerSecond = maxBytesPerSecond;
        _stopwatch = Stopwatch.StartNew();
        _totalBytesTransferred = 0;
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => _baseStream.CanWrite;
    public override long Length => _baseStream.Length;

    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    public override void Flush() => _baseStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        Throttle(count);
        var bytesRead = _baseStream.Read(buffer, offset, count);
        RecordBytes(bytesRead);
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await ThrottleAsync(count, cancellationToken);
        var bytesRead = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        RecordBytes(bytesRead);
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await ThrottleAsync(buffer.Length, cancellationToken);
        var bytesRead = await _baseStream.ReadAsync(buffer, cancellationToken);
        RecordBytes(bytesRead);
        return bytesRead;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Throttle(count);
        _baseStream.Write(buffer, offset, count);
        RecordBytes(count);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await ThrottleAsync(count, cancellationToken);
        await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
        RecordBytes(count);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await ThrottleAsync(buffer.Length, cancellationToken);
        await _baseStream.WriteAsync(buffer, cancellationToken);
        RecordBytes(buffer.Length);
    }

    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

    public override void SetLength(long value) => _baseStream.SetLength(value);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _baseStream.Dispose();
        }
        base.Dispose(disposing);
    }

    private void Throttle(int bytes)
    {
        if (_maxBytesPerSecond <= 0) return;

        lock (_lock)
        {
            var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            if (elapsedSeconds <= 0) return;

            var currentRate = _totalBytesTransferred / elapsedSeconds;

            if (currentRate > _maxBytesPerSecond)
            {
                // Calculate how long to wait
                var expectedSeconds = _totalBytesTransferred / (double)_maxBytesPerSecond;
                var delaySeconds = expectedSeconds - elapsedSeconds;

                if (delaySeconds > 0)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(Math.Min(delaySeconds, 1.0)));
                }
            }
        }
    }

    private async Task ThrottleAsync(int bytes, CancellationToken cancellationToken)
    {
        if (_maxBytesPerSecond <= 0) return;

        double delaySeconds;

        lock (_lock)
        {
            var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            if (elapsedSeconds <= 0) return;

            var currentRate = _totalBytesTransferred / elapsedSeconds;

            if (currentRate > _maxBytesPerSecond)
            {
                var expectedSeconds = _totalBytesTransferred / (double)_maxBytesPerSecond;
                delaySeconds = expectedSeconds - elapsedSeconds;
            }
            else
            {
                return;
            }
        }

        if (delaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(delaySeconds, 1.0)), cancellationToken);
        }
    }

    private void RecordBytes(int bytes)
    {
        lock (_lock)
        {
            _totalBytesTransferred += bytes;
        }
    }

    /// <summary>
    /// Get current transfer rate in bytes per second
    /// </summary>
    public double CurrentRate
    {
        get
        {
            lock (_lock)
            {
                var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
                return elapsedSeconds > 0 ? _totalBytesTransferred / elapsedSeconds : 0;
            }
        }
    }

    /// <summary>
    /// Get total bytes transferred
    /// </summary>
    public long TotalBytesTransferred
    {
        get
        {
            lock (_lock)
            {
                return _totalBytesTransferred;
            }
        }
    }
}
