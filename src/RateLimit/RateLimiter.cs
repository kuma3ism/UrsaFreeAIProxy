namespace JeminiLateUse.RateLimit;

public class RateLimiter
{
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly int _maxRequestsPerMinute;
    private readonly object _lock = new();

    public RateLimiter(int maxRequestsPerMinute = 5)
    {
        _maxRequestsPerMinute = maxRequestsPerMinute;
    }

    public async Task WaitForSlotAsync()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddMinutes(-1);

            // Remove timestamps older than 1 minute
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < oneMinuteAgo)
            {
                _requestTimestamps.Dequeue();
            }

            // If we've hit the limit, wait
            if (_requestTimestamps.Count >= _maxRequestsPerMinute)
            {
                var oldestRequest = _requestTimestamps.Peek();
                var waitTime = oldestRequest.AddMinutes(1) - now;
                if (waitTime.TotalMilliseconds > 0)
                {
                    Thread.Sleep(waitTime);
                }
                _requestTimestamps.Enqueue(DateTime.UtcNow);
            }
            else
            {
                _requestTimestamps.Enqueue(now);
            }
        }

        await Task.CompletedTask;
    }

    public int GetRequestsInLastMinute()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddMinutes(-1);
            return _requestTimestamps.Count(t => t >= oneMinuteAgo);
        }
    }
}
