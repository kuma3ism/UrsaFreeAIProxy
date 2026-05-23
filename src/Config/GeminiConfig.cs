namespace JeminiLateUse.Config;

public class GeminiConfig
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-1.5-flash";
    public int MaxRequestsPerMinute { get; set; } = 5;
    public int DelayMilliseconds { get; set; } = 12000; // 12 seconds between requests

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new ArgumentException("ApiKey is required");
        if (string.IsNullOrWhiteSpace(Model))
            throw new ArgumentException("Model is required");
    }
}
