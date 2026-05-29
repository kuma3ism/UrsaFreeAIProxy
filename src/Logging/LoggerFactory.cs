using System.Text;

namespace UrsaFreeAIProxy.Logging;

public interface ILogger
{
    void LogInfo(string message);
    void LogError(string message, Exception? ex = null);
    void LogDebug(string message);
    void LogWarning(string message, Exception? ex = null);
}

public class ConsoleLogger : ILogger
{
    private readonly string _name;
    private readonly LogLevel _minimumLogLevel;

    public ConsoleLogger(string name, LogLevel minimumLogLevel = LogLevel.Information)
    {
        _name = name;
        _minimumLogLevel = minimumLogLevel;
    }

    public void LogInfo(string message)
    {
        Log(LogLevel.Information, "", message);
    }

    public void LogError(string message, Exception? ex = null)
    {
        var fullMessage = ex != null ? $"{message}\n{ex}" : message;
        Log(LogLevel.Error, "❌", fullMessage);
    }

    public void LogDebug(string message)
    {
        Log(LogLevel.Debug, "🔍", message);
    }

    public void LogWarning(string message, Exception? ex = null)
    {
        var fullMessage = ex != null ? $"{message}\n{ex}" : message;
        Log(LogLevel.Warning, "⚠️", fullMessage);
    }

    private void Log(LogLevel level, string emoji, string message)
    {
        if (level < _minimumLogLevel)
            return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var emojiPart = string.IsNullOrEmpty(emoji) ? "" : $"{emoji} ";
        var formattedMessage = $"[{timestamp}] {emojiPart}{message}";

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = level switch
        {
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Information => ConsoleColor.Green,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            _ => ConsoleColor.White
        };

        Console.WriteLine(formattedMessage);
        Console.ForegroundColor = originalColor;
    }
}

public class LoggerProvider
{
    private static LogLevel _minimumLogLevel = LogLevel.Information;

    public static void SetMinimumLogLevel(LogLevel level)
    {
        _minimumLogLevel = level;
    }

    public static ILogger GetLogger(string name)
    {
        return new ConsoleLogger(name, _minimumLogLevel);
    }
}

public enum LogLevel
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3
}
