using System.Net.Http.Json;
using UrsaFreeAIProxy.Config;
using UrsaFreeAIProxy.Logging;
using UrsaFreeAIProxy.Provider;
using UrsaFreeAIProxy.Server;

// Parse command-line arguments
var isDebug = args.Contains("--debug");
var isTest  = args.Contains("--test");

// Configure logging level
var isDevelopment = isDebug || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
LoggerProvider.SetMinimumLogLevel(isDevelopment ? LogLevel.Debug : LogLevel.Information);
var logger = LoggerProvider.GetLogger("Startup");

logger.LogInfo("🚀 UrsaFreeAIProxy starting...");
if (isDebug) logger.LogInfo("🐛 Debug mode enabled");

// Load configuration
var appSettings = ConfigurationManager.LoadFromFile("appsettings.json");
if (isDevelopment)
{
    var devSettings = ConfigurationManager.LoadFromFile("appsettings.Development.json");
    if (devSettings.Logging?.LogLevel?.Count > 0)
    {
        appSettings.Logging = devSettings.Logging;
    }
}

// Build Gemini config
var config = ConfigurationManager.ToGeminiConfig(appSettings);
var serverPort = ConfigurationManager.GetServerPort(appSettings);
var cliPort = GetPortFromArgs(args);
if (cliPort.HasValue)
{
    serverPort = cliPort.Value;
}

// Validate API keys
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
    logger.LogInfo($"   key[{i}]: {MaskKey(key)}");
}
logger.LogInfo($"   Rate Limit: {config.MaxRequestsPerMinute} requests/minute/key");
logger.LogInfo($"   Effective Limit: {config.ApiKeys.Count * config.MaxRequestsPerMinute} requests/minute");

// --test mode
if (isTest)
{
    await RunRateLimitTestAsync(config, logger);
    return;
}

logger.LogInfo($"   Server Port: {serverPort}");

// Start the server
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
    logger.LogInfo("🧪 ===== API Key Check Test =====");
    logger.LogInfo($"   Model: {config.Model}");
    logger.LogInfo($"   Keys: {config.ApiKeys.Count}");
    logger.LogInfo($"   Limit per key: {config.MaxRequestsPerMinute} requests/minute");
    logger.LogInfo($"   Effective limit: {config.ApiKeys.Count} × {config.MaxRequestsPerMinute} = {config.ApiKeys.Count * config.MaxRequestsPerMinute} requests/minute");
    logger.LogInfo($"   Sending 1 request per key");
    logger.LogInfo("");

    const string testMessage = "Reply with only the word OK.";
    int succeeded = 0;
    int rateLimited = 0;
    int failed = 0;
    var keyReports = new List<KeyReport>();

    using var httpClient = new HttpClient();
    for (int i = 0; i < config.ApiKeys.Count; i++)
    {
        var apiKey = config.ApiKeys[i];
        var keyLabel = $"key[{i}]({MaskKey(apiKey)})";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await SendSingleKeyTestAsync(httpClient, config.Model, apiKey, testMessage);
            sw.Stop();

            if (response.StatusCode == 429)
            {
                rateLimited++;
                logger.LogWarning($"   {keyLabel} ⚠️  {response.Reason} ({sw.ElapsedMilliseconds}ms) → {response.ErrorMessage}");
                keyReports.Add(new KeyReport(i, MaskKey(apiKey), response.Reason, 0, sw.ElapsedMilliseconds, response.Description, response.ErrorMessage));
                continue;
            }

            if (!response.IsSuccess)
            {
                failed++;
                logger.LogError($"   {keyLabel} ❌ HTTP {response.StatusCode} ({sw.ElapsedMilliseconds}ms) → {response.ErrorMessage}");
                keyReports.Add(new KeyReport(i, MaskKey(apiKey), $"HTTP {response.StatusCode}", 0, sw.ElapsedMilliseconds, response.Description, response.ErrorMessage));
                continue;
            }

            logger.LogInfo($"   {keyLabel} ✅ {sw.ElapsedMilliseconds}ms → \"{response.Text.Trim()}\"");
            succeeded++;
            keyReports.Add(new KeyReport(i, MaskKey(apiKey), "OK", config.MaxRequestsPerMinute, sw.ElapsedMilliseconds, "Available", response.Text.Trim()));
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError($"   {keyLabel} ❌ {sw.ElapsedMilliseconds}ms → {ex.Message}");
            failed++;
            keyReports.Add(new KeyReport(i, MaskKey(apiKey), "ERROR", 0, sw.ElapsedMilliseconds, "Communication or execution error", ex.Message));
        }
    }

    logger.LogInfo("");
    logger.LogInfo($"🧪 ===== Result =====");
    logger.LogInfo($"   Per-key report:");
    foreach (var report in keyReports)
    {
        logger.LogInfo($"   key[{report.Index}] {report.MaskedKey} | {report.Status,-16} | {report.Capacity,2} req/min | {report.ElapsedMilliseconds,5}ms | {report.Description}");
        if (!string.IsNullOrWhiteSpace(report.Detail) && report.Status != "OK")
        {
            logger.LogInfo($"      detail: {report.Detail}");
        }
    }
    logger.LogInfo("");
    logger.LogInfo($"   ✅ Succeeded : {succeeded}");
    logger.LogInfo($"   ⚠️  429       : {rateLimited}");
    logger.LogInfo($"   ❌ Failed    : {failed}");
    logger.LogInfo($"   Current usable capacity: {succeeded} × {config.MaxRequestsPerMinute} = {succeeded * config.MaxRequestsPerMinute} requests/minute");
    logger.LogInfo($"   Configured capacity    : {config.ApiKeys.Count} × {config.MaxRequestsPerMinute} = {config.ApiKeys.Count * config.MaxRequestsPerMinute} requests/minute");
    if (failed == 0 && rateLimited == 0)
        logger.LogInfo($"   🎉 All keys are usable. Estimated capacity: {config.ApiKeys.Count * config.MaxRequestsPerMinute} requests/minute");
    else if (keyReports.All(report => report.Status == "DAILY_FREE_TIER"))
        logger.LogWarning("   ⚠️  Free Tier daily quota appears exhausted for all tested keys. Free projects under the same account may still share practical limits.");
    else
        logger.LogWarning("   ⚠️  Some keys are unavailable or currently rate limited.");
    logger.LogInfo("");
}

static async Task<KeyTestResult> SendSingleKeyTestAsync(
    HttpClient httpClient,
    string model,
    string apiKey,
    string message)
{
    const string baseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    var url = $"{baseUrl}/{model}:generateContent?key={apiKey}";
    var request = new
    {
        contents = new[]
        {
            new { parts = new[] { new { text = message } } }
        }
    };

    using var response = await httpClient.PostAsJsonAsync(url, request);
    if ((int)response.StatusCode == 429)
    {
        var error = await response.Content.ReadAsStringAsync();
        var geminiError = ParseGeminiError(error);
        return new KeyTestResult(false, 429, "", geminiError.Reason, geminiError.Description, geminiError.Detail);
    }

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        var geminiError = ParseGeminiError(error);
        return new KeyTestResult(false, (int)response.StatusCode, "", $"HTTP {(int)response.StatusCode}", geminiError.Description, geminiError.Detail);
    }

    var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
    var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "(empty)";
    return new KeyTestResult(true, (int)response.StatusCode, text, "OK", "Available", "");
}

static string MaskKey(string key)
    => key.Length >= 8 ? $"...{key[^8..]}" : "****";

static string Truncate(string? text, int maxLen)
{
    if (string.IsNullOrEmpty(text))
    {
        return "";
    }

    var trimmed = text.Replace("\n", " ").Replace("\r", "");
    return trimmed.Length <= maxLen ? trimmed : trimmed[..maxLen] + "...";
}

static GeminiErrorInfo ParseGeminiError(string errorJson)
{
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(errorJson);
        if (!doc.RootElement.TryGetProperty("error", out var error))
        {
            return new GeminiErrorInfo("ERROR", "Failed to parse Gemini error body", Truncate(errorJson, 220));
        }

        var parts = new List<string>();
        var quotaIds = new List<string>();
        var quotaMetrics = new List<string>();
        var retryDelay = "";
        AddProperty(parts, error, "status");
        AddProperty(parts, error, "message");

        if (error.TryGetProperty("details", out var details) && details.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var detail in details.EnumerateArray())
            {
                AddProperty(parts, detail, "reason");
                AddProperty(parts, detail, "domain");
                AddProperty(parts, detail, "retryDelay");
                retryDelay = GetProperty(detail, "retryDelay") ?? retryDelay;

                if (detail.TryGetProperty("metadata", out var metadata) &&
                    metadata.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    AddProperty(parts, metadata, "quota_metric", "quotaMetric");
                    AddProperty(parts, metadata, "quota_id", "quotaId");
                    AddProperty(parts, metadata, "quota_location", "quotaLocation");
                    AddProperty(parts, metadata, "service");
                    AddProperty(parts, metadata, "consumer");
                    AddIfNotBlank(quotaMetrics, GetProperty(metadata, "quota_metric"));
                    AddIfNotBlank(quotaIds, GetProperty(metadata, "quota_id"));
                }

                if (detail.TryGetProperty("violations", out var violations) &&
                    violations.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var violation in violations.EnumerateArray())
                    {
                        AddProperty(parts, violation, "quotaMetric");
                        AddProperty(parts, violation, "quotaId");
                        AddProperty(parts, violation, "subject");
                        AddProperty(parts, violation, "description");
                        AddIfNotBlank(quotaMetrics, GetProperty(violation, "quotaMetric"));
                        AddIfNotBlank(quotaIds, GetProperty(violation, "quotaId"));
                    }
                }
            }
        }

        var detailText = parts.Count > 0 ? string.Join(" | ", parts.Distinct()) : Truncate(errorJson, 220);
        return ClassifyGeminiError(quotaIds, quotaMetrics, retryDelay, detailText);
    }
    catch (System.Text.Json.JsonException)
    {
        return new GeminiErrorInfo("ERROR", "Failed to parse Gemini error body", Truncate(errorJson, 220));
    }
}

static GeminiErrorInfo ClassifyGeminiError(
    List<string> quotaIds,
    List<string> quotaMetrics,
    string retryDelay,
    string detailText)
{
    var quotaIdText = string.Join(",", quotaIds);
    var quotaMetricText = string.Join(",", quotaMetrics);
    var retryNote = string.IsNullOrWhiteSpace(retryDelay) ? "" : $" retry={retryDelay}";

    if (quotaIdText.Contains("PerDay", StringComparison.OrdinalIgnoreCase) &&
        quotaIdText.Contains("FreeTier", StringComparison.OrdinalIgnoreCase))
    {
        return new GeminiErrorInfo(
            "DAILY_FREE_TIER",
            $"Daily free tier quota exhausted. This key can be treated as unusable for today.{retryNote}",
            detailText);
    }

    if (quotaIdText.Contains("PerMinute", StringComparison.OrdinalIgnoreCase))
    {
        return new GeminiErrorInfo(
            "RPM_LIMIT",
            $"Per-minute rate limit reached. May recover after a short wait.{retryNote}",
            detailText);
    }

    if (quotaMetricText.Contains("input_token", StringComparison.OrdinalIgnoreCase) ||
        quotaIdText.Contains("Token", StringComparison.OrdinalIgnoreCase))
    {
        return new GeminiErrorInfo(
            "TOKEN_QUOTA",
            $"Token quota limit. Use shorter input or wait before retrying.{retryNote}",
            detailText);
    }

    return new GeminiErrorInfo(
        "QUOTA",
        $"Quota limit reached. Please check the details.{retryNote}",
        detailText);
}

static string? GetProperty(System.Text.Json.JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out var value))
    {
        return null;
    }

    return value.ValueKind == System.Text.Json.JsonValueKind.String
        ? value.GetString()
        : value.ToString();
}

static void AddIfNotBlank(List<string> values, string? value)
{
    if (!string.IsNullOrWhiteSpace(value))
    {
        values.Add(value);
    }
}

static void AddProperty(
    List<string> parts,
    System.Text.Json.JsonElement element,
    string propertyName,
    string? label = null)
{
    if (!element.TryGetProperty(propertyName, out var value))
    {
        return;
    }

    var text = value.ValueKind == System.Text.Json.JsonValueKind.String
        ? value.GetString()
        : value.ToString();

    if (string.IsNullOrWhiteSpace(text))
    {
        return;
    }

    parts.Add($"{label ?? propertyName}={Truncate(text, 120)}");
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

record KeyTestResult(bool IsSuccess, int StatusCode, string Text, string Reason, string Description, string ErrorMessage);
record KeyReport(int Index, string MaskedKey, string Status, int Capacity, long ElapsedMilliseconds, string Description, string Detail);
record GeminiErrorInfo(string Reason, string Description, string Detail);
