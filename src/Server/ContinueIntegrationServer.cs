using System.Net;
using System.Text.Json;
using JeminiLateUse.Config;
using JeminiLateUse.Provider;

namespace JeminiLateUse.Server;

public class ContinueIntegrationServer
{
    private readonly GeminiProvider _provider;
    private readonly int _port;
    private HttpListener? _listener;

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
        Console.WriteLine($"Server started on http://localhost:{_port}");

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
            if (context.Request.HttpMethod == "POST" && context.Request.Url?.PathAndQuery == "/chat")
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
            else if (context.Request.Url?.PathAndQuery == "/health")
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await WriteResponseAsync(context, new { status = "ok" });
            }
            else
            {
                context.Response.StatusCode = 404;
                await WriteResponseAsync(context, new { error = "Not found" });
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await WriteResponseAsync(context, new { error = ex.Message });
        }
        finally
        {
            context.Response.Close();
        }
    }

    private async Task WriteResponseAsync(HttpListenerContext context, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var buffer = System.Text.Encoding.UTF8.GetBytes(json);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    public void Stop()
    {
        _listener?.Stop();
    }
}

public class ChatRequest
{
    public string? Message { get; set; }
}
