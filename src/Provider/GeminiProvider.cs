using System.Net.Http.Json;
using JeminiLateUse.Config;
using JeminiLateUse.Logging;
using JeminiLateUse.RateLimit;

namespace JeminiLateUse.Provider;

public class GeminiProvider
{
    private readonly GeminiConfig _config;
    private readonly RateLimiter _rateLimiter;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiProvider(GeminiConfig config)
    {
        config.Validate();
        _config = config;
        _rateLimiter = new RateLimiter(config.MaxRequestsPerMinute);
        _httpClient = new HttpClient();
        _logger = LoggerProvider.GetLogger(nameof(GeminiProvider));
    }

    public async Task<GeminiResponse> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug($"Sending message: {message.Substring(0, Math.Min(50, message.Length))}...");
        _logger.LogDebug($"Rate limit status: {_rateLimiter.GetRequestsInLastMinute()}/5");

        // Wait for rate limit slot
        await _rateLimiter.WaitForSlotAsync();
        _logger.LogDebug("Rate limit slot acquired");

        var url = $"{BaseUrl}/{_config.Model}:generateContent?key={_config.ApiKey}";
        
        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = message }
                    }
                }
            }
        };

        try
        {
            _logger.LogDebug($"Calling Gemini API: {_config.Model}");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            stopwatch.Stop();
            
            response.EnsureSuccessStatusCode();
            _logger.LogInfo($"Gemini API call successful ({stopwatch.ElapsedMilliseconds}ms)");

            var result = await response.Content.ReadAsAsync<GeminiResponse>(cancellationToken);
            return result ?? throw new InvalidOperationException("Failed to parse response");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"Failed to call Gemini API", ex);
            throw new InvalidOperationException($"Failed to call Gemini API: {ex.Message}", ex);
        }
    }

    public int GetCurrentRateLimit()
    {
        return _rateLimiter.GetRequestsInLastMinute();
    }

    public string GetModel()
    {
        return _config.Model;
    }
}

public class GeminiResponse
{
    public GeminiContent[]? Candidates { get; set; }
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

public class GeminiContent
{
    public GeminiPart[]? Content { get; set; }
}

public class GeminiPart
{
    public string? Text { get; set; }
}

public class GeminiUsageMetadata
{
    public int PromptTokenCount { get; set; }
    public int CandidatesTokenCount { get; set; }
    public int TotalTokenCount { get; set; }
}
