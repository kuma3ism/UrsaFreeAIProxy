using JeminiLateUse.Config;
using Xunit;

namespace JeminiLateUse.Tests.Config;

public class ConfigurationManagerTests : IDisposable
{
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
        Environment.SetEnvironmentVariable("GEMINI_MODEL", null);
        Environment.SetEnvironmentVariable("GEMINI_MAX_REQUESTS_PER_MINUTE", null);
        Environment.SetEnvironmentVariable("SERVER_PORT", null);
    }

    [Fact]
    public void LoadFromFile_WithMissingFile_ReturnsDefaultAppSettings()
    {
        // Act
        var settings = ConfigurationManager.LoadFromFile("non-existent-file.json");

        // Assert
        Assert.NotNull(settings);
    }

    [Fact]
    public void ToGeminiConfig_WithEnvironmentVariable_UsesEnvVar()
    {
        // Arrange
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "env-api-key");
        Environment.SetEnvironmentVariable("GEMINI_MODEL", "gemini-env-model");
        Environment.SetEnvironmentVariable("GEMINI_MAX_REQUESTS_PER_MINUTE", "7");
        var appSettings = new AppSettings
        {
            Gemini = new GeminiSettings
            {
                ApiKey = "file-api-key",
                Model = "gemini-pro",
                MaxRequestsPerMinute = 10
            }
        };

        // Act
        var config = ConfigurationManager.ToGeminiConfig(appSettings);

        // Assert
        Assert.Equal("env-api-key", config.ApiKey);
        Assert.Equal("gemini-env-model", config.Model);
        Assert.Equal(7, config.MaxRequestsPerMinute);
    }

    [Fact]
    public void ToGeminiConfig_WithFileConfig_UsesFileValue()
    {
        // Arrange
        var appSettings = new AppSettings
        {
            Gemini = new GeminiSettings
            {
                ApiKey = "file-api-key",
                Model = "gemini-1.5-flash",
                MaxRequestsPerMinute = 10
            }
        };

        // Act
        var config = ConfigurationManager.ToGeminiConfig(appSettings);

        // Assert
        Assert.Equal("file-api-key", config.ApiKey);
        Assert.Equal("gemini-1.5-flash", config.Model);
        Assert.Equal(10, config.MaxRequestsPerMinute);
    }

    [Fact]
    public void ToGeminiConfig_WithBlankFileApiKey_UsesEnvironmentVariable()
    {
        // Arrange
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "env-api-key");
        var appSettings = new AppSettings
        {
            Gemini = new GeminiSettings
            {
                ApiKey = "",
                Model = "gemini-1.5-flash"
            }
        };

        // Act
        var config = ConfigurationManager.ToGeminiConfig(appSettings);

        // Assert
        Assert.Equal("env-api-key", config.ApiKey);
    }

    [Fact]
    public void GetServerPort_WithEnvironmentVariable_UsesEnvVar()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SERVER_PORT", "9090");
        var appSettings = new AppSettings
        {
            Server = new ServerSettings { Port = 8080 }
        };

        // Act
        var port = ConfigurationManager.GetServerPort(appSettings);

        // Assert
        Assert.Equal(9090, port);
    }
}
