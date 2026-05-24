namespace JeminiLateUse.Config;

public class AppSettings
{
    public GeminiSettings? Gemini { get; set; }
    public ServerSettings? Server { get; set; }
    public LoggingSettings? Logging { get; set; }
}

public class GeminiSettings
{
    /// <summary>後方互換用。ApiKeysが空の場合にフォールバックとして使用される。</summary>
    public string? ApiKey { get; set; }

    /// <summary>複数APIキーのリスト。設定するとラウンドロビンでローテーションされる。</summary>
    public List<string>? ApiKeys { get; set; }

    public string Model { get; set; } = "gemini-1.5-flash";
    public int MaxRequestsPerMinute { get; set; } = 5;

    /// <summary>有効なAPIキー一覧を返す。ApiKeysが優先、なければApiKeyを使用。</summary>
    public List<string> GetEffectiveApiKeys()
    {
        if (ApiKeys != null && ApiKeys.Any(k => !string.IsNullOrWhiteSpace(k)))
            return ApiKeys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();

        if (!string.IsNullOrWhiteSpace(ApiKey))
            return new List<string> { ApiKey };

        return new List<string>();
    }
}

public class ServerSettings
{
    public int Port { get; set; } = 8080;
}

public class LoggingSettings
{
    public Dictionary<string, string>? LogLevel { get; set; }
}
