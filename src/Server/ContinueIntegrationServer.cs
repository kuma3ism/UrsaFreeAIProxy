using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using JeminiLateUse.Config;
using JeminiLateUse.Provider;

namespace JeminiLateUse.Server;

public class ContinueIntegrationServer
{
    private readonly GeminiProvider _provider;
    private readonly int _port;
    private HttpListener? _listener;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ContinueIntegrationServer(GeminiConfig config, int port = 8080)
    {
        _provider = new GeminiProvider(config);
        _port = port;
    }

    public async Task StartAsync()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        _listener.Start();
        Console.WriteLine($"🚀 Server started on http://localhost:{_port}");
        Console.WriteLine($"📝 Model: {_provider.GetModel()}");
        Console.WriteLine($"✅ Ready to serve CONTINUE agent requests");

        while (_listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequestAsync(context);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            // OpenAI互換エンドポイント
            if (context.Request.HttpMethod == "POST" && context.Request.Url?.PathAndQuery == "/v1/chat/completions")
            {
                await HandleChatCompletionsAsync(context);
            }
            // 独自エンドポイント（後方互換性）
            else if (context.Request.HttpMethod == "POST" && context.Request.Url?.PathAndQuery == "/chat")
            {
                await HandleChatAsync(context);
            }
            // ヘルスチェック
            else if (context.Request.Url?.PathAndQuery == "/health")
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await WriteResponseAsync(context, new { status = "ok", model = _provider.GetModel() });
            }
            // モデル情報
            else if (context.Request.Url?.PathAndQuery == "/v1/models")
            {
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
                error = new
                {
                    message = "Messages are required",
                    type = "invalid_request_error"
                }
            });
            return;
        }

        // 最後のメッセージを取得
        var lastMessage = request.Messages[^1];
        var userMessage = lastMessage.Content ?? string.Empty;

        try
        {
            var geminiResponse = await _provider.SendMessageAsync(userMessage);
            var assistantText = geminiResponse.Candidates?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text ?? "No response";

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
                        Message = new OpenAIMessage
                        {
                            Role = "assistant",
                            Content = assistantText
                        },
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
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await WriteResponseAsync(context, new
            {
                error = new
                {
                    message = $"Failed to get response: {ex.Message}",
                    type = "server_error"
                }
            });
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

        var response = await _provider.SendMessageAsync(request.Message);
        var text = response.Candidates?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text ?? "No response";

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        await WriteResponseAsync(context, new { response = text, rateLimit = _provider.GetCurrentRateLimit() });
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

// OpenAI互換リクエスト
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
}

// OpenAI互換レスポンス
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

// OpenAI互換チョイス
public class OpenAIChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public OpenAIMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

// OpenAI互換メッセージ
public class OpenAIMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

// OpenAI互換使用量情報
public class OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

// 独自フォーマット（後方互換性）
public class ChatRequest
{
    public string? Message { get; set; }
}
