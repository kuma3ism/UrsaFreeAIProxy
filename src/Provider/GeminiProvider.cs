using System.Net.Http.Json;
using JeminiLateUse.Config;
using JeminiLateUse.Logging;
using JeminiLateUse.RateLimit;

namespace JeminiLateUse.Provider;

/// <summary>OpenAIメッセージをGeminiに渡す際の中間モデル</summary>
public record GeminiChatMessage(string Role, string Content);

public class GeminiProvider
{
    private readonly GeminiConfig _config;
    private readonly RateLimiter _rateLimiter;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private const int RetryWaitSeconds = 60;
    private const int MaxRetries = 3;

    // ラウンドロビン用カウンター
    private int _keyIndex = 0;
    private readonly object _keyLock = new();

    public GeminiProvider(GeminiConfig config)
    {
        config.Validate();
        _config = config;
        _rateLimiter = new RateLimiter(config.MaxRequestsPerMinute);
        _httpClient = new HttpClient();
        _logger = LoggerProvider.GetLogger(nameof(GeminiProvider));
        _logger.LogInfo($"Loaded {_config.ApiKeys.Count} API key(s)");
    }

    /// <summary>ラウンドロビンで次のAPIキーを取得する</summary>
    private string GetNextApiKey()
    {
        lock (_keyLock)
        {
            var key = _config.ApiKeys[_keyIndex % _config.ApiKeys.Count];
            _keyIndex++;
            return key;
        }
    }

    /// <summary>マルチターン会話を正式なGemini形式で送信する</summary>
    public async Task<GeminiResponse> SendMessagesAsync(
        IEnumerable<GeminiChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();

        // systemロールはsystemInstructionに分離（Gemini APIの仕様）
        var systemMessages = messageList.Where(m => m.Role == "system").ToList();
        var chatMessages = messageList.Where(m => m.Role != "system").ToList();

        var contents = chatMessages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new
            {
                // Geminiはassistantではなくmodelというロール名を使う
                role = m.Role == "assistant" ? "model" : "user",
                parts = new[] { new { text = m.Content } }
            }).ToArray();

        object request;
        if (systemMessages.Any())
        {
            var systemText = string.Join("\n", systemMessages.Select(m => m.Content));
            request = new
            {
                system_instruction = new { parts = new[] { new { text = systemText } } },
                contents
            };
        }
        else
        {
            request = new { contents };
        }

        _logger.LogDebug($"Sending {chatMessages.Count} message(s) to Gemini (system: {systemMessages.Count})");
        return await SendRequestAsync(request, cancellationToken);
    }

    /// <summary>単一メッセージを送信する（/chatエンドポイント後方互換用）</summary>
    public async Task<GeminiResponse> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug($"Sending message: {message.Substring(0, Math.Min(50, message.Length))}...");

        var request = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = message } } }
            }
        };

        return await SendRequestAsync(request, cancellationToken);
    }

    private async Task<GeminiResponse> SendRequestAsync(object request, CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Rate limit status: {_rateLimiter.GetRequestsInLastMinute()}/{_config.MaxRequestsPerMinute}");
        await _rateLimiter.WaitForSlotAsync(cancellationToken);
        _logger.LogDebug("Rate limit slot acquired");

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var apiKey = GetNextApiKey();
            var keyLabel = $"key[{(_keyIndex - 1) % _config.ApiKeys.Count}]";
            var url = $"{BaseUrl}/{_config.Model}:generateContent?key={apiKey}";

            try
            {
                _logger.LogDebug($"Calling Gemini API: {_config.Model} with {keyLabel} (attempt {attempt}/{MaxRetries})");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
                stopwatch.Stop();

                if ((int)response.StatusCode == 429)
                {
                    if (attempt >= MaxRetries)
                    {
                        _logger.LogError($"429 Too Many Requests on {keyLabel} - max retries reached", null);
                        throw new InvalidOperationException("Gemini API rate limit exceeded. Please wait and try again.");
                    }
                    _logger.LogInfo($"429 Too Many Requests on {keyLabel} - waiting {RetryWaitSeconds}s before retry ({attempt}/{MaxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(RetryWaitSeconds), cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                _logger.LogInfo($"Gemini API call successful with {keyLabel} ({stopwatch.ElapsedMilliseconds}ms)");

                var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
                return result ?? throw new InvalidOperationException("Failed to parse response");
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Failed to call Gemini API with {keyLabel}", ex);
                throw new InvalidOperationException($"Failed to call Gemini API: {ex.Message}", ex);
            }
        }

        throw new InvalidOperationException("Failed to call Gemini API after max retries.");
    }

    public int GetCurrentRateLimit() => _rateLimiter.GetRequestsInLastMinute();
    public string GetModel() => _config.Model;
}

public class GeminiResponse
{
    public GeminiContent[]? Candidates { get; set; }
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

public class GeminiContent
{
    public GeminiContentBody? Content { get; set; }
}

public class GeminiContentBody
{
    public GeminiPart[]? Parts { get; set; }
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
