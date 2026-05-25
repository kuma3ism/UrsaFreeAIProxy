namespace UrsaFreeAIProxy.RateLimit;

public class RateLimiter
{
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly int _maxRequestsPerMinute;
    private readonly TimeSpan _window;
    private readonly object _lock = new();

    public RateLimiter(int maxRequestsPerMinute = 5, TimeSpan? window = null)
    {
        if (maxRequestsPerMinute <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRequestsPerMinute), "Rate limit must be greater than zero");

        _maxRequestsPerMinute = maxRequestsPerMinute;
        _window = window ?? TimeSpan.FromMinutes(1);
    }

    public async Task WaitForSlotAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            TimeSpan waitTime;

            lock (_lock)
            {
                var now = DateTime.UtcNow;
                RemoveExpiredRequests(now);

                if (_requestTimestamps.Count < _maxRequestsPerMinute)
                {
                    _requestTimestamps.Enqueue(now);
                    return;
                }

                var oldestRequest = _requestTimestamps.Peek();
                waitTime = oldestRequest.Add(_window) - now;
            }

            if (waitTime > TimeSpan.Zero)
            {
                await Task.Delay(waitTime, cancellationToken);
            }
            else
            {
                await Task.Yield();
            }
        }
    }

    public int GetRequestsInLastMinute()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            RemoveExpiredRequests(now);
            return _requestTimestamps.Count;
        }
    }

    private void RemoveExpiredRequests(DateTime now)
    {
        var windowStart = now - _window;
        while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() <= windowStart)
        {
            _requestTimestamps.Dequeue();
        }
    }
}
