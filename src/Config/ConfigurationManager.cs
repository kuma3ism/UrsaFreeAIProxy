using System.Text.Json;
using JeminiLateUse.Logging;

namespace JeminiLateUse.Config;

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
        var geminiSettings = appSettings.Gemini ?? new GeminiSettings();
        var apiKey = geminiSettings.ApiKey ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        return new GeminiConfig
        {
            ApiKey = apiKey,
            Model = geminiSettings.Model,
            MaxRequestsPerMinute = geminiSettings.MaxRequestsPerMinute
        };
    }
}
