using System.Net.Http.Json;
using JeminiLateUse.Config;
using JeminiLateUse.RateLimit;

namespace JeminiLateUse.Provider;

public class GeminiProvider
{
    private readonly GeminiConfig _config;
    private readonly RateLimiter _rateLimiter;
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiProvider(GeminiConfig config)
    {
        config.Validate();
        _config = config;
        _rateLimiter = new RateLimiter(config.MaxRequestsPerMinute);
        _httpClient = new HttpClient();
    }

    public async Task<GeminiResponse> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        // Wait for rate limit slot
        await _rateLimiter.WaitForSlotAsync();

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
            var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsAsync<GeminiResponse>(cancellationToken);
            return result ?? throw new InvalidOperationException("Failed to parse response");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to call Gemini API: {ex.Message}", ex);
        }
    }

    public int GetCurrentRateLimit()
    {
        return _rateLimiter.GetRequestsInLastMinute();
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
