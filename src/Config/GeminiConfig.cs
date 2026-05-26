namespace UrsaFreeAIProxy.Config;

public class GeminiConfig
{
    /// <summary>ラウンドロビンで使用するAPIキーリスト</summary>
    public List<string> ApiKeys { get; set; } = new();

    public string Model { get; set; } = "gemini-1.5-flash";
    public int MaxRequestsPerMinute { get; set; } = 5;

    /// <summary>後方互換用: 単一キーとして取得</summary>
    public string? ApiKey => ApiKeys.FirstOrDefault();

    public void Validate()
    {
        if (ApiKeys == null || !ApiKeys.Any())
            throw new ArgumentException("ApiKey (or ApiKeys) is required");
        if (string.IsNullOrWhiteSpace(Model))
            throw new ArgumentException("Model is required");
    }
}
