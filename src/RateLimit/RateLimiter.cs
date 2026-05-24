namespace UrsaFreeAIProxy.RateLimit;

public class RateLimiter
{
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly int _maxRequestsPerMinute;
    private readonly object _lock = new();

    public RateLimiter(int maxRequestsPerMinute = 5)
    {
        if (maxRequestsPerMinute <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRequestsPerMinute), "Rate limit must be greater than zero");

        _maxRequestsPerMinute = maxRequestsPerMinute;
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
                waitTime = oldestRequest.AddMinutes(1) - now;
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
        var oneMinuteAgo = now.AddMinutes(-1);
        while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < oneMinuteAgo)
        {
            _requestTimestamps.Dequeue();
        }
    }
}
