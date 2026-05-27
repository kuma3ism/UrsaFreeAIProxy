using System.Text.Json;
using UrsaFreeAIProxy.Logging;

namespace UrsaFreeAIProxy.Config;

public class ConfigurationManager
{
    private static readonly ILogger Logger = LoggerProvider.GetLogger(nameof(ConfigurationManager));

    public static AppSettings LoadFromFile(string filePath = "appsettings.json")
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Logger.LogWarning($"Configuration file not found: {filePath}");
                return new AppSettings();
            }

            var json = File.ReadAllText(filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            Logger.LogInfo($"Loaded configuration from {filePath}");
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load configuration: {ex.Message}", ex);
            return new AppSettings();
        }
    }

    public static GeminiConfig ToGeminiConfig(AppSettings appSettings)
    {
        var aiSettings = appSettings.Ai ?? new AiSettings();

        // Collect valid API keys from config file
        var apiKeys = aiSettings.GetEffectiveApiKeys();

        // Prepend GEMINI_API_KEY environment variable if set
        var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey) && !apiKeys.Contains(envKey))
            apiKeys.Insert(0, envKey);

        return new GeminiConfig
        {
            ApiKeys = apiKeys,
            Model = GetEnvironmentValueOrDefault("GEMINI_MODEL", aiSettings.Model) ?? new AiSettings().Model,
            MaxRequestsPerMinute = GetPositiveIntEnvironmentValueOrDefault(
                "GEMINI_MAX_REQUESTS_PER_MINUTE",
                aiSettings.MaxRequestsPerMinute)
        };
    }

    public static int GetServerPort(AppSettings appSettings)
    {
        return GetPositiveIntEnvironmentValueOrDefault("SERVER_PORT", appSettings.Server?.Port ?? 8080);
    }

    private static string? GetEnvironmentValueOrDefault(string variableName, string? defaultValue)
    {
        var environmentValue = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(environmentValue) ? defaultValue : environmentValue;
    }

    private static int GetPositiveIntEnvironmentValueOrDefault(string variableName, int defaultValue)
    {
        var environmentValue = Environment.GetEnvironmentVariable(variableName);
        if (int.TryParse(environmentValue, out var parsedValue) && parsedValue > 0)
        {
            return parsedValue;
        }

        return defaultValue;
    }
}
