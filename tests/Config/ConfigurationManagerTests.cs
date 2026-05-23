using JeminiLateUse.Config;
using Xunit;

namespace JeminiLateUse.Tests.Config;

public class ConfigurationManagerTests
{
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
        var appSettings = new AppSettings
        {
            Gemini = new GeminiSettings
            {
                Model = "gemini-pro"
            }
        };

        // Act
        var config = ConfigurationManager.ToGeminiConfig(appSettings);

        // Assert
        Assert.Equal("env-api-key", config.ApiKey);
        Assert.Equal("gemini-pro", config.Model);
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
}
