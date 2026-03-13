using CodeNexus.Domain.Entities;
using CodeNexus.Infrastructure.Services;
using CodeNexus.Infrastructure.Settings;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CodeNexus.UnitTests.Services;

public class TokenServiceExtractTokenInfoTests
{
    private readonly TokenService _tokenService;
    private readonly JwtSettings _jwtSettings;

    public TokenServiceExtractTokenInfoTests()
    {
        _jwtSettings = new JwtSettings
        {
            SecretKey = "YourSuperSecretKeyAtLeast32CharactersLongForTesting!@#$",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7,
            ResetPasswordTokenExpirationMinutes = 15
        };

        var optionsMock = new Mock<IOptions<JwtSettings>>();
        optionsMock.Setup(x => x.Value).Returns(_jwtSettings);
        _tokenService = new TokenService(optionsMock.Object);
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeJtiClaim()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hashedpassword"
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);

        jtiClaim.Should().NotBeNull();
        jtiClaim!.Value.Should().NotBeNullOrEmpty();
        Guid.TryParse(jtiClaim.Value, out _).Should().BeTrue("JTI should be a valid GUID");
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeAllUserClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hashedpassword",
            RoleId = roleId,
            Role = new Role { RoleId = roleId, RoleName = "Admin" }
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        jwtToken.Claims.Should().Contain(c => c.Type == "roleId" && c.Value == roleId.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void ExtractTokenInfo_WithValidToken_ReturnsTokenIdAndExpiresAt()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hashedpassword"
        };

        var token = _tokenService.GenerateAccessToken(user);

        // Act
        var (tokenId, expiresAt) = _tokenService.ExtractTokenInfo(token);

        // Assert
        tokenId.Should().NotBeNullOrEmpty();
        Guid.TryParse(tokenId, out _).Should().BeTrue("TokenId should be a valid GUID");
        expiresAt.Should().NotBeNull();
        expiresAt.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes), 
            TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void ExtractTokenInfo_WithInvalidToken_ReturnsNull()
    {
        // Arrange
        var invalidToken = "this.is.not.a.valid.jwt.token";

        // Act
        var (tokenId, expiresAt) = _tokenService.ExtractTokenInfo(invalidToken);

        // Assert
        tokenId.Should().BeNull();
        expiresAt.Should().BeNull();
    }

    [Fact]
    public void ExtractTokenInfo_WithEmptyToken_ReturnsNull()
    {
        // Arrange
        var emptyToken = string.Empty;

        // Act
        var (tokenId, expiresAt) = _tokenService.ExtractTokenInfo(emptyToken);

        // Assert
        tokenId.Should().BeNull();
        expiresAt.Should().BeNull();
    }

    [Fact]
    public void ExtractTokenInfo_WithNullToken_ReturnsNull()
    {
        // Arrange
        string? nullToken = null;

        // Act
        var (tokenId, expiresAt) = _tokenService.ExtractTokenInfo(nullToken!);

        // Assert
        tokenId.Should().BeNull();
        expiresAt.Should().BeNull();
    }

    [Fact]
    public void ExtractTokenInfo_WithMalformedToken_ReturnsNull()
    {
        // Arrange
        var malformedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid.signature";

        // Act
        var (tokenId, expiresAt) = _tokenService.ExtractTokenInfo(malformedToken);

        // Assert
        tokenId.Should().BeNull();
        expiresAt.Should().BeNull();
    }

    [Fact]
    public void ExtractTokenInfo_WithTruncatedToken_ReturnsNull()
    {
        // Arrange
        var truncatedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";

        // Act
        var (tokenId, expiresAt) = _tokenService.ExtractTokenInfo(truncatedToken);

        // Assert
        tokenId.Should().BeNull();
        expiresAt.Should().BeNull();
    }

    [Fact]
    public void GenerateAccessToken_MultipleTimes_GeneratesUniqueJti()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hashedpassword"
        };

        // Act
        var token1 = _tokenService.GenerateAccessToken(user);
        var token2 = _tokenService.GenerateAccessToken(user);

        var (tokenId1, _) = _tokenService.ExtractTokenInfo(token1);
        var (tokenId2, _) = _tokenService.ExtractTokenInfo(token2);

        // Assert
        tokenId1.Should().NotBe(tokenId2, "Each token should have a unique JTI");
        tokenId1.Should().NotBeNullOrEmpty();
        tokenId2.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ExtractTokenInfo_ExtractedJtiMatchesOriginal()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hashedpassword"
        };

        var token = _tokenService.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var originalJti = jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        // Act
        var (extractedTokenId, _) = _tokenService.ExtractTokenInfo(token);

        // Assert
        extractedTokenId.Should().Be(originalJti);
    }

    [Fact]
    public void ExtractTokenInfo_ExpiresAtMatchesTokenExpiration()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hashedpassword"
        };

        var token = _tokenService.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var originalExpiry = jwtToken.ValidTo;

        // Act
        var (_, extractedExpiresAt) = _tokenService.ExtractTokenInfo(token);

        // Assert
        extractedExpiresAt.Should().BeCloseTo(originalExpiry, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GenerateAccessToken_TokenExpirationShouldMatchConfiguration()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hashedpassword"
        };

        var beforeGeneration = DateTime.UtcNow;

        // Act
        var token = _tokenService.GenerateAccessToken(user);
        var (_, expiresAt) = _tokenService.ExtractTokenInfo(token);

        var afterGeneration = DateTime.UtcNow;

        // Assert
        var expectedExpiration = beforeGeneration.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
        expiresAt.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));
        expiresAt.Should().BeOnOrAfter(beforeGeneration.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes - 1));
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnBase64String()
    {
        // Act
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Assert
        refreshToken.Should().NotBeNullOrEmpty();
        
        var action = () => Convert.FromBase64String(refreshToken);
        action.Should().NotThrow();
    }

    [Fact]
    public void GenerateRefreshToken_MultipleTimes_GeneratesUniqueTokens()
    {
        // Act
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();
        var token3 = _tokenService.GenerateRefreshToken();

        // Assert
        token1.Should().NotBe(token2);
        token1.Should().NotBe(token3);
        token2.Should().NotBe(token3);
    }
}
