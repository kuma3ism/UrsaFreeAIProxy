using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using JeminiLateUse.Config;
using JeminiLateUse.Logging;
using JeminiLateUse.Provider;

namespace JeminiLateUse.Server;

public class ContinueIntegrationServer
{
    private const int MaxConcurrentRequests = 8;

    private readonly GeminiProvider _provider;
    private readonly int _port;
    private HttpListener? _listener;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _requestSemaphore = new(MaxConcurrentRequests, MaxConcurrentRequests);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ContinueIntegrationServer(GeminiConfig config, int port = 8080)
    {
        _provider = new GeminiProvider(config);
        _port = port;
        _logger = LoggerProvider.GetLogger(nameof(ContinueIntegrationServer));
    }

    public async Task StartAsync()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{_port}/");
        _listener.Start();
        Console.WriteLine($"🚀 Server started on http://localhost:{_port}");
        Console.WriteLine($"📝 Model: {_provider.GetModel()}");
        Console.WriteLine($"✅ Ready to serve CONTINUE agent requests");

        while (_listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequestWithConcurrencyLimitAsync(context);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestWithConcurrencyLimitAsync(HttpListenerContext context)
    {
        if (!await _requestSemaphore.WaitAsync(TimeSpan.Zero))
        {
            try
            {
                context.Response.StatusCode = 503;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Retry-After", "5");
                await WriteResponseAsync(context, new
                {
                    error = new { message = "Server is busy. Please retry shortly.", type = "server_busy" }
                });
            }
            finally
            {
                context.Response.Close();
            }

            return;
        }

        try
        {
            await HandleRequestAsync(context);
        }
        finally
        {
            _requestSemaphore.Release();
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.PathAndQuery ?? "";
        var method = context.Request.HttpMethod;

        try
        {
            if (method == "POST" && path == "/v1/chat/completions")
            {
                _logger.LogInfo($"→→→ Continue: {method} {path}");
                await HandleChatCompletionsAsync(context);
            }
            else if (method == "POST" && path == "/chat")
            {
                _logger.LogInfo($"→→→ Continue: {method} {path}");
                await HandleChatAsync(context);
            }
            else if (path == "/health")
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await WriteResponseAsync(context, new { status = "ok", model = _provider.GetModel() });
            }
            else if (path == "/v1/models")
            {
                _logger.LogDebug($"→→→ Continue: {method} {path}");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var response = new
                {
                    @object = "list",
                    data = new[]
                    {
                        new
                        {
                            id = _provider.GetModel(),
                            @object = "model",
                            owned_by = "google",
                            permission = new object[] { }
                        }
                    }
                };
                await WriteResponseAsync(context, response);
            }
            else
            {
                context.Response.StatusCode = 404;
                await WriteResponseAsync(context, new { error = new { message = "Not found", type = "invalid_request_error" } });
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await WriteResponseAsync(context, new { error = new { message = ex.Message, type = "server_error" } });
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static string Truncate(string? text, int maxLen = 80)
    {
        if (string.IsNullOrEmpty(text)) return "(empty)";
        var trimmed = text.Replace("\n", " ").Replace("\r", "");
        return trimmed.Length <= maxLen ? trimmed : trimmed[..maxLen] + "...";
    }

    private async Task HandleChatCompletionsAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        var request = JsonSerializer.Deserialize<OpenAIChatRequest>(body);

        if (request?.Messages == null || request.Messages.Length == 0)
        {
            context.Response.StatusCode = 400;
            await WriteResponseAsync(context, new
            {
                error = new { message = "Messages are required", type = "invalid_request_error" }
            });
            return;
        }

        var geminiMessages = request.Messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new GeminiChatMessage(m.Role ?? "user", m.Content!));

        _logger.LogInfo($"=>=>=> Gemini: {request.Messages.Length} messages (stream={request.Stream})");

        // ユーザーの最後のメッセージ冒頭を表示
        var lastUserMsg = request.Messages.LastOrDefault(m => m.Role == "user")?.Content;
        _logger.LogInfo($"💬 User: \"{Truncate(lastUserMsg)}\"");

        try
        {
            var geminiResponse = await _provider.SendMessagesAsync(geminiMessages);
            var candidate = geminiResponse.Candidates?.FirstOrDefault();
            var part = candidate?.Content?.Parts?.FirstOrDefault();
            var assistantText = part?.Text ?? "No response";
            var tokens = geminiResponse.UsageMetadata?.TotalTokenCount ?? 0;

            _logger.LogInfo($"<=<=<= Gemini: {tokens} tokens");

            // レスポンス冒頭を表示
            _logger.LogInfo($"🤖 Reply: \"{Truncate(assistantText)}\"");

            var isStream = request.Stream == true;

            if (isStream)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/event-stream";
                context.Response.Headers.Add("Cache-Control", "no-cache");
                context.Response.Headers.Add("X-Accel-Buffering", "no");

                var id = Guid.NewGuid().ToString("N")[..24];
                var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var chunk = JsonSerializer.Serialize(new
                {
                    id,
                    @object = "chat.completion.chunk",
                    created,
                    model = _provider.GetModel(),
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            delta = new { role = "assistant", content = assistantText },
                            finish_reason = (string?)null
                        }
                    }
                }, _jsonOptions);

                var doneChunk = JsonSerializer.Serialize(new
                {
                    id,
                    @object = "chat.completion.chunk",
                    created,
                    model = _provider.GetModel(),
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            delta = new { },
                            finish_reason = "stop"
                        }
                    }
                }, _jsonOptions);

                var sseBody = $"data: {chunk}\n\ndata: {doneChunk}\n\ndata: [DONE]\n\n";
                var buffer = System.Text.Encoding.UTF8.GetBytes(sseBody);
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer);
                _logger.LogInfo($"←←← Continue: stream sent ({assistantText.Length} chars)");
            }
            else
            {
                var response = new OpenAIChatResponse
                {
                    Id = Guid.NewGuid().ToString("N")[..24],
                    Object = "chat.completion",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = _provider.GetModel(),
                    Choices = new[]
                    {
                        new OpenAIChoice
                        {
                            Index = 0,
                            Message = new OpenAIMessage { Role = "assistant", Content = assistantText },
                            FinishReason = "stop"
                        }
                    },
                    Usage = new OpenAIUsage
                    {
                        PromptTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0,
                        CompletionTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0,
                        TotalTokens = geminiResponse.UsageMetadata?.TotalTokenCount ?? 0
                    }
                };

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await WriteResponseAsync(context, response);
                _logger.LogInfo($"←←← Continue: response sent ({assistantText.Length} chars)");
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await WriteResponseAsync(context, new
            {
                error = new { message = $"Failed to get response: {ex.Message}", type = "server_error" }
            });
            _logger.LogError($"←←← Continue: 500 error", ex);
        }
    }

    private async Task HandleChatAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        var request = JsonSerializer.Deserialize<ChatRequest>(body);

        if (request?.Message == null)
        {
            context.Response.StatusCode = 400;
            await WriteResponseAsync(context, new { error = "Message is required" });
            return;
        }

        _logger.LogInfo($"=>=>=> Gemini: {request.Message.Substring(0, Math.Min(30, request.Message.Length))}...");
        var response = await _provider.SendMessageAsync(request.Message);
        var text = response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "No response";

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        await WriteResponseAsync(context, new { response = text, rateLimit = _provider.GetCurrentRateLimit() });
        _logger.LogInfo($"←←← Continue: response sent ({text.Length} chars)");
    }

    private async Task WriteResponseAsync(HttpListenerContext context, object data)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        var buffer = System.Text.Encoding.UTF8.GetBytes(json);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    public void Stop()
    {
        _listener?.Stop();
    }
}

public class OpenAIChatRequest
{
    [JsonPropertyName("messages")]
    public OpenAIMessage[]? Messages { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }
}

public class OpenAIChatResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public OpenAIChoice[]? Choices { get; set; }

    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}

public class OpenAIChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public OpenAIMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class OpenAIMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public class OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class ChatRequest
{
    public string? Message { get; set; }
}
