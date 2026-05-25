using UrsaFreeAIProxy.RateLimit;
using Xunit;

namespace UrsaFreeAIProxy.Tests.RateLimiter;

public class RateLimiterTests
{
    private static readonly TimeSpan TestWindow = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task WaitForSlotAsync_WithinLimit_DoesNotBlock()
    {
        // Arrange
        var limiter = new global::UrsaFreeAIProxy.RateLimit.RateLimiter(5, TestWindow);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 5; i++)
        {
            await limiter.WaitForSlotAsync();
        }
        stopwatch.Stop();

        // Assert - 5回のリクエストは1秒以内に完了
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Expected < 1000ms, got {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task WaitForSlotAsync_ExceedsLimit_Blocks()
    {
        // Arrange
        var limiter = new global::UrsaFreeAIProxy.RateLimit.RateLimiter(2, TestWindow);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - 最初の2回はすぐ
        await limiter.WaitForSlotAsync();
        await limiter.WaitForSlotAsync();
        // 3回目は待機
        await limiter.WaitForSlotAsync();

        stopwatch.Stop();

        // Assert - 制限に引っかかるので遅延が発生
        Assert.True(stopwatch.Elapsed >= TestWindow,
            $"Expected >= {TestWindow.TotalMilliseconds}ms, got {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetRequestsInLastMinute_ReturnsCorrectCount()
    {
        // Arrange
        var limiter = new global::UrsaFreeAIProxy.RateLimit.RateLimiter(5, TestWindow);

        // Act
        for (int i = 0; i < 3; i++)
        {
            await limiter.WaitForSlotAsync();
        }
        var count = limiter.GetRequestsInLastMinute();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetRequestsInLastMinute_OnlyCountsRecentRequests()
    {
        // Arrange
        var limiter = new global::UrsaFreeAIProxy.RateLimit.RateLimiter(5, TestWindow);

        // Act
        await limiter.WaitForSlotAsync();
        await Task.Delay(TestWindow + TimeSpan.FromMilliseconds(20));
        var countAfter = limiter.GetRequestsInLastMinute();

        // Assert
        Assert.Equal(0, countAfter);
    }
}
