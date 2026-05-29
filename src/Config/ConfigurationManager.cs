using System.IO;
using System.Linq;
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
                var created = new AppSettings();

                // If apikeys.txt exists even when appsettings.json does not, merge and create appsettings.json
                var txtPathWhenMissing = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, "apikeys.txt");
                if (File.Exists(txtPathWhenMissing))
                {
                    var added = MergeApiKeysFromTxt(created, txtPathWhenMissing);
                    try
                    {
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        File.WriteAllText(filePath, JsonSerializer.Serialize(created, options));
                        Logger.LogInfo($"Created {filePath} and added {added} api key(s) from {txtPathWhenMissing}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to write created configuration: {ex.Message}", ex);
                    }
                }

                return created;
            }

            var json = File.ReadAllText(filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            Logger.LogInfo($"Loaded configuration from {filePath}");
            var appSettings = settings ?? new AppSettings();

            // If apikeys.txt is present, merge its keys into the loaded settings (ignore duplicates).
            var txtPath = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, "apikeys.txt");
            if (File.Exists(txtPath))
            {
                var added = MergeApiKeysFromTxt(appSettings, txtPath);
                if (added > 0)
                {
                    try
                    {
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        File.WriteAllText(filePath, JsonSerializer.Serialize(appSettings, options));
                        Logger.LogInfo($"Updated {filePath} with {added} api key(s) from {txtPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to update configuration: {ex.Message}", ex);
                    }
                }
            }

            return appSettings;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load configuration: {ex.Message}", ex);
            return new AppSettings();
        }
    }

    private static int MergeApiKeysFromTxt(AppSettings appSettings, string txtPath)
    {
        try
        {
            var lines = File.ReadAllLines(txtPath)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
                .ToList();

            if (!lines.Any())
                return 0;

            if (appSettings.Ai == null)
                appSettings.Ai = new AiSettings();

            var existing = appSettings.Ai.GetEffectiveApiKeys();
            var set = new HashSet<string>(existing);
            var added = 0;
            foreach (var k in lines)
            {
                if (set.Add(k))
                {
                    existing.Add(k);
                    added++;
                }
            }

            appSettings.Ai.ApiKeys = existing;
            appSettings.Ai.ApiKey = null; // prefer ApiKeys list going forward
            return added;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to merge API keys from {txtPath}: {ex.Message}", ex);
            return 0;
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
