using JeminiLateUse.Config;
using JeminiLateUse.Logging;
using JeminiLateUse.Server;

// ロギングレベルを設定
var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
LoggerProvider.SetMinimumLogLevel(isDevelopment ? LogLevel.Debug : LogLevel.Information);
var logger = LoggerProvider.GetLogger("Startup");

logger.LogInfo("🚀 JeminiLateUse starting...");

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

// APIキーの検証
if (string.IsNullOrWhiteSpace(config.ApiKey))
{
    logger.LogError("❌ Error: GEMINI_API_KEY is not set. Please set it via environment variable or appsettings.json");
    Environment.Exit(1);
}

logger.LogInfo($"✅ Configuration loaded");
logger.LogInfo($"   Model: {config.Model}");
logger.LogInfo($"   Rate Limit: {config.MaxRequestsPerMinute} requests/minute");
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
