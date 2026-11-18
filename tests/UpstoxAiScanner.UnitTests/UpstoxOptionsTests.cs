using FluentAssertions;
using UpstoxAiScanner.Application.Configuration;
using Xunit;

namespace UpstoxAiScanner.UnitTests;

public class UpstoxOptionsTests
{
    [Fact]
    public void Options_ShouldHaveDefaultValues()
    {
        var options = new UpstoxOptions();

        options.BaseUrl.Should().Be("https://api.upstox.com");
        options.ApiVersion.Should().Be("v3");
        options.TimeoutSeconds.Should().Be(30);
        options.MaxRetryAttempts.Should().Be(3);
        options.ThrottleRequestsPerSecond.Should().Be(10);
        options.UseMockFallbackWhenUnconfigured.Should().BeTrue();
        options.ForceMockMode.Should().BeFalse();
        options.AccessToken.Should().BeNull();
        options.IsLiveMode.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData("", false, false)]
    [InlineData("   ", false, false)]
    [InlineData("valid_token_123", false, true)]
    [InlineData("valid_token_123", true, false)]
    public void IsLiveMode_ShouldReflectTokenAndForceMockStatus(string? token, bool forceMock, bool expectedIsLive)
    {
        var options = new UpstoxOptions
        {
            AccessToken = token,
            ForceMockMode = forceMock
        };

        options.IsLiveMode.Should().Be(expectedIsLive);
    }
}
