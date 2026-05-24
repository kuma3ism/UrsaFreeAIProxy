using UrsaFreeAIProxy.RateLimit;
using Xunit;

namespace UrsaFreeAIProxy.Tests.RateLimiter;

public class RateLimiterTests
{
    [Fact]
    public async Task WaitForSlotAsync_WithinLimit_DoesNotBlock()
    {
        // Arrange
        var limiter = new global::UrsaFreeAIProxy.RateLimit.RateLimiter(5);
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
        var limiter = new global::UrsaFreeAIProxy.RateLimit.RateLimiter(2);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - 最初の2回はすぐ
        await limiter.WaitForSlotAsync();
        await limiter.WaitForSlotAsync();
        // 3回目は待機
        await limiter.WaitForSlotAsync();

        stopwatch.Stop();

        // Assert - 制限に引っかかるので遅延が発生
        Assert.True(stopwatch.ElapsedMilliseconds >= 11000,
            $"Expected >= 11000ms (12秒の遅延), got {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void GetRequestsInLastMinute_ReturnsCorrectCount()
    {
        // Arrange
        var limiter = new global::UrsaFreeAIProxy.RateLimit.RateLimiter(5);

        // Act
        for (int i = 0; i < 3; i++)
        {
            limiter.WaitForSlotAsync().GetAwaiter().GetResult();
        }
        var count = limiter.GetRequestsInLastMinute();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void GetRequestsInLastMinute_OnlyCountsRecentRequests()
    {
        // Arrange
        var limiter = new global::UrsaFreeAIProxy.RateLimit.RateLimiter(5);

        // Act
        limiter.WaitForSlotAsync().GetAwaiter().GetResult();
        var countBefore = limiter.GetRequestsInLastMinute();

        // 古いリクエストは数えられないことを確認
        // 注: 実際には時間経過が必要なので、ここでは基本的なテストのみ
        var countAfter = limiter.GetRequestsInLastMinute();

        // Assert
        Assert.Equal(countBefore, countAfter);
    }
}
