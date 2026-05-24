using UrsaFreeAIProxy.Config;
using Xunit;

namespace UrsaFreeAIProxy.Tests.Config;

public class GeminiConfigTests
{
    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        // Arrange
        var config = new GeminiConfig
        {
            ApiKeys = new List<string> { "test-api-key" },
            Model = "gemini-1.5-flash"
        };

        // Act & Assert
        var exception = Record.Exception(() => config.Validate());
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithoutApiKey_ThrowsException()
    {
        // Arrange
        var config = new GeminiConfig
        {
            ApiKeys = new List<string>(),
            Model = "gemini-1.5-flash"
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("ApiKey (or ApiKeys) is required", exception.Message);
    }

    [Fact]
    public void Validate_WithEmptyModel_ThrowsException()
    {
        // Arrange
        var config = new GeminiConfig
        {
            ApiKeys = new List<string> { "test-key" },
            Model = string.Empty
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("Model is required", exception.Message);
    }

    [Fact]
    public void DefaultValues_AreSet()
    {
        // Arrange & Act
        var config = new GeminiConfig();

        // Assert
        Assert.Equal("gemini-1.5-flash", config.Model);
        Assert.Equal(5, config.MaxRequestsPerMinute);
        Assert.Equal(12000, config.DelayMilliseconds);
    }
}
