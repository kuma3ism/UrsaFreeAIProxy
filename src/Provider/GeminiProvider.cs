using System.Net.Http.Json;
using UrsaFreeAIProxy.Config;
using UrsaFreeAIProxy.Logging;
using UrsaFreeAIProxy.RateLimit;

namespace UrsaFreeAIProxy.Provider;

/// <summary>OpenAIメッセージをAI APIに渡す際の中間モデル</summary>
public record GeminiChatMessage(string Role, string Content);

public class GeminiProvider
{
    private readonly GeminiConfig _config;
    private readonly RateLimiter[] _rateLimiters;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    // ラウンドロビン用カウンター
    private int _keyIndex = 0;
    private readonly object _keyLock = new();

    public GeminiProvider(GeminiConfig config)
    {
        config.Validate();
        _config = config;
        _rateLimiters = config.ApiKeys
            .Select(_ => new RateLimiter(config.MaxRequestsPerMinute))
            .ToArray();
        _httpClient = new HttpClient();
        _logger = LoggerProvider.GetLogger(nameof(GeminiProvider));
        _logger.LogInfo($"Loaded {_config.ApiKeys.Count} API key(s)");
    }

    /// <summary>APIキーの下8桁を返す（ログ表示用）</summary>
    private static string MaskKey(string key)
        => key.Length >= 8 ? $"...{key[^8..]}" : "****";

    /// <summary>ラウンドロビンで次のAPIキーを取得する</summary>
    private (string key, int index) GetNextApiKey()
    {
        lock (_keyLock)
        {
            var index = _keyIndex % _config.ApiKeys.Count;
            var key = _config.ApiKeys[index];
            _keyIndex++;
            return (key, index);
        }
    }

    /// <summary>マルチターン会話を送信する</summary>
    public async Task<GeminiResponse> SendMessagesAsync(
        IEnumerable<GeminiChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();

        // systemロールはsystemInstructionに分離（API仕様）
        var systemMessages = messageList.Where(m => m.Role == "system").ToList();
        var chatMessages = messageList.Where(m => m.Role != "system").ToList();

        var contents = chatMessages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new
            {
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

        _logger.LogDebug($"Sending {chatMessages.Count} message(s) to [{_config.Model}] (system: {systemMessages.Count})");
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
        // キーの数だけ試みる（全キー試してもダメなら諦める）
        var totalKeys = _config.ApiKeys.Count;
        for (int attempt = 1; attempt <= totalKeys; attempt++)
        {
            var (apiKey, keyIdx) = GetNextApiKey();
            var rateLimiter = _rateLimiters[keyIdx];
            var currentRequests = rateLimiter.GetRequestsInLastMinute();
            var keyLabel = $"key[{keyIdx}]({MaskKey(apiKey)})";
            var url = $"{BaseUrl}/{_config.Model}:generateContent?key={apiKey}";

            _logger.LogDebug($"Rate limit status for {keyLabel}: {currentRequests}/{_config.MaxRequestsPerMinute}");
            if (currentRequests >= _config.MaxRequestsPerMinute)
            {
                _logger.LogInfo($"⏳ Rate limit reached for {keyLabel} ({currentRequests}/{_config.MaxRequestsPerMinute}). Waiting for slot...");
            }

            await rateLimiter.WaitForSlotAsync(cancellationToken);
            _logger.LogDebug($"Rate limit slot acquired for {keyLabel}");

            try
            {
                var reqNum = rateLimiter.GetRequestsInLastMinute();
                _logger.LogInfo($"📡 [{_config.Model}] [{reqNum}/{_config.MaxRequestsPerMinute} req/min] via {keyLabel}");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
                stopwatch.Stop();

                if ((int)response.StatusCode == 429)
                {
                    _logger.LogInfo($"⚠️  429 on {keyLabel} - switching to next key ({attempt}/{totalKeys})");
                    continue;
                }

                response.EnsureSuccessStatusCode();
                _logger.LogInfo($"✅ [{_config.Model}] response via {keyLabel} ({stopwatch.ElapsedMilliseconds}ms)");

                var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
                return result ?? throw new InvalidOperationException("Failed to parse response");
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Failed to call [{_config.Model}] with {keyLabel}", ex);
                throw new InvalidOperationException($"Failed to call API: {ex.Message}", ex);
            }
        }

        _logger.LogError("All API keys exhausted (all returned 429). Please add more keys or wait.", null);
        throw new InvalidOperationException("All API keys are rate limited. Please try again later.");
    }

    public int GetCurrentRateLimit() => _rateLimiters.Sum(limiter => limiter.GetRequestsInLastMinute());
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
