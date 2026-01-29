using CodeNexus.Infrastructure.Services;
using CodeNexus.Infrastructure.Settings;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace CodeNexus.UnitTests.Services;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;
    private readonly JwtSettings _settings;

    public TokenServiceTests()
    {
        _settings = new JwtSettings
        {
            SecretKey = "ThisIsAVeryLongSecretKeyForTestingPurposes123456789",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ResetPasswordTokenExpirationMinutes = 15
        };
        var optionsMock = new Mock<IOptions<JwtSettings>>();
        optionsMock.Setup(x => x.Value).Returns(_settings);
        _tokenService = new TokenService(optionsMock.Object);
    }

    [Fact]
    public void GenerateResetPasswordToken_ShouldReturnNonEmptyToken()
    {
        // Arrange
        var email = "test@test.com";

        // Act
        var token = _tokenService.GenerateResetPasswordToken(email);

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateResetPasswordToken_ShouldReturnValidJwtFormat()
    {
        // Arrange
        var email = "test@test.com";

        // Act
        var token = _tokenService.GenerateResetPasswordToken(email);

        // Assert - JWT has 3 parts separated by dots
        token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public void ValidateResetPasswordToken_WhenValidToken_ShouldReturnEmail()
    {
        // Arrange
        var email = "test@test.com";
        var token = _tokenService.GenerateResetPasswordToken(email);

        // Act
        var result = _tokenService.ValidateResetPasswordToken(token);

        // Assert
        result.Should().Be(email);
    }

    [Fact]
    public void ValidateResetPasswordToken_WhenInvalidToken_ShouldReturnNull()
    {
        // Arrange
        var invalidToken = "invalid.token.here";

        // Act
        var result = _tokenService.ValidateResetPasswordToken(invalidToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateResetPasswordToken_WhenMalformedToken_ShouldReturnNull()
    {
        // Arrange
        var malformedToken = "not-a-jwt-token";

        // Act
        var result = _tokenService.ValidateResetPasswordToken(malformedToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateResetPasswordToken_WhenTokenFromDifferentKey_ShouldReturnNull()
    {
        // Arrange - Create token with different settings
        var differentSettings = new JwtSettings
        {
            SecretKey = "DifferentSecretKeyThatIsAlsoVeryLong123456789",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ResetPasswordTokenExpirationMinutes = 15
        };
        var differentOptionsMock = new Mock<IOptions<JwtSettings>>();
        differentOptionsMock.Setup(x => x.Value).Returns(differentSettings);
        var differentService = new TokenService(differentOptionsMock.Object);
        var token = differentService.GenerateResetPasswordToken("test@test.com");

        // Act
        var result = _tokenService.ValidateResetPasswordToken(token);

        // Assert
        result.Should().BeNull();
    }
}
