namespace JeminiLateUse.Config;

public class AppSettings
{
    public GeminiSettings? Gemini { get; set; }
    public ServerSettings? Server { get; set; }
    public LoggingSettings? Logging { get; set; }
}

public class GeminiSettings
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-1.5-flash";
    public int MaxRequestsPerMinute { get; set; } = 5;
}

public class ServerSettings
{
    public int Port { get; set; } = 8080;
}

public class LoggingSettings
{
    public Dictionary<string, string>? LogLevel { get; set; }
}
