using UrsaFreeAIProxy.Config;
using UrsaFreeAIProxy.Logging;
using UrsaFreeAIProxy.Provider;
using UrsaFreeAIProxy.Server;

// コマンドライン引数チェック
var isDebug = args.Contains("--debug");
var isTest  = args.Contains("--test");

// ロギングレベルを設定
var isDevelopment = isDebug || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
LoggerProvider.SetMinimumLogLevel(isDevelopment ? LogLevel.Debug : LogLevel.Information);
var logger = LoggerProvider.GetLogger("Startup");

logger.LogInfo("🚀 UrsaFreeAIProxy starting...");
if (isDebug) logger.LogInfo("🐛 Debug mode enabled");

// 設定を読み込む
var appSettings = ConfigurationManager.LoadFromFile("appsettings.json");
if (isDevelopment)
{
    var devSettings = ConfigurationManager.LoadFromFile("appsettings.Development.json");
    if (devSettings.Logging?.LogLevel?.Count > 0)
    {
        appSettings.Logging = devSettings.Logging;
    }
}

// Gemini設定を作成
var config = ConfigurationManager.ToGeminiConfig(appSettings);
var serverPort = ConfigurationManager.GetServerPort(appSettings);
var cliPort = GetPortFromArgs(args);
if (cliPort.HasValue)
{
    serverPort = cliPort.Value;
}

// APIキーの検証
if (config.ApiKeys == null || !config.ApiKeys.Any())
{
    logger.LogError("❌ Error: GEMINI_API_KEY is not set. Please set it via environment variable or appsettings.json");
    Environment.Exit(1);
}

logger.LogInfo($"✅ Configuration loaded");
logger.LogInfo($"   Model: {config.Model}");
logger.LogInfo($"   API Keys: {config.ApiKeys.Count} key(s) loaded");
for (int i = 0; i < config.ApiKeys.Count; i++)
{
    var key = config.ApiKeys[i];
    var masked = key.Length >= 8 ? $"...{key[^8..]}" : "****";
    logger.LogInfo($"   key[{i}]: {masked}");
}
logger.LogInfo($"   Rate Limit: {config.MaxRequestsPerMinute} requests/minute");

// --test モード
if (isTest)
{
    await RunRateLimitTestAsync(config, logger);
    return;
}

logger.LogInfo($"   Server Port: {serverPort}");

// サーバーを起動
try
{
    var server = new ContinueIntegrationServer(config, serverPort);
    logger.LogInfo($"📡 Starting HTTP server on http://localhost:{serverPort}");
    await server.StartAsync();
}
catch (Exception ex)
{
    logger.LogError($"Failed to start server", ex);
    Environment.Exit(1);
}

static async Task RunRateLimitTestAsync(GeminiConfig config, ILogger logger)
{
    logger.LogInfo("");
    logger.LogInfo("🧪 ===== Rate Limit Test =====");
    logger.LogInfo($"   Sending {config.MaxRequestsPerMinute + 1} requests (limit={config.MaxRequestsPerMinute})");
    logger.LogInfo($"   Keys: {config.ApiKeys.Count}");
    logger.LogInfo("");

    var provider = new GeminiProvider(config);
    const string testMessage = "Reply with only the word OK.";
    int total = config.MaxRequestsPerMinute + 1;
    int succeeded = 0;
    int failed = 0;

    for (int i = 1; i <= total; i++)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await provider.SendMessageAsync(testMessage);
            sw.Stop();
            var text = response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "(empty)";
            var waitNote = sw.ElapsedMilliseconds > 5000 ? $" (waited {sw.ElapsedMilliseconds / 1000}s)" : "";
            logger.LogInfo($"   [{i}/{total}] ✅ {sw.ElapsedMilliseconds}ms{waitNote} → \"{text.Trim()}\"");
            succeeded++;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError($"   [{i}/{total}] ❌ {sw.ElapsedMilliseconds}ms → {ex.Message}");
            failed++;
        }
    }

    logger.LogInfo("");
    logger.LogInfo($"🧪 ===== Result =====");
    logger.LogInfo($"   ✅ Succeeded : {succeeded}");
    logger.LogInfo($"   ❌ Failed    : {failed}");
    if (failed == 0)
        logger.LogInfo("   🎉 All requests completed successfully!");
    else
        logger.LogWarning("   ⚠️  Some requests failed. Check your API keys.");
    logger.LogInfo("");
}

static int? GetPortFromArgs(string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        string? portValue = null;

        if (arg == "--port" || arg == "-p")
        {
            portValue = i + 1 < args.Length ? args[i + 1] : "";
        }
        else if (arg.StartsWith("--port=", StringComparison.Ordinal))
        {
            portValue = arg["--port=".Length..];
        }

        if (portValue == null)
        {
            continue;
        }

        if (int.TryParse(portValue, out var port) && port > 0 && port <= 65535)
        {
            return port;
        }

        Console.Error.WriteLine($"Invalid port: {portValue}");
        Environment.Exit(1);
    }

    return null;
}
