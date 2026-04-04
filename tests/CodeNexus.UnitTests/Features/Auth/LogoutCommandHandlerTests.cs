using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Auth.Commands.Logout;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class LogoutCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _tokenServiceMock.Setup(x => x.HashRefreshToken(It.IsAny<string>())).Returns((string s) => s);
        _handler = new LogoutCommandHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object,
            _tokenServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsFailure()
    {
        // Arrange
        _currentUserServiceMock.Setup(x => x.GetUserId()).Returns(Guid.Empty);
        var command = new LogoutCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
        result.ErrorMessage.Should().Be("User not authenticated");
    }

    [Fact]
    public async Task Handle_WithAccessToken_AddsToBlacklist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid().ToString();
        var expiresAt = DateTime.Now.AddHours(1);
        var accessToken = "valid.jwt.token";

        _currentUserServiceMock.Setup(x => x.GetUserId()).Returns(userId);
        _tokenServiceMock.Setup(x => x.ExtractTokenInfo(accessToken))
            .Returns((tokenId, expiresAt));

        SetupTokenBlacklistDbSet(new List<TokenBlacklist>());
        SetupRefreshTokensDbSet(new List<RefreshToken>());

        var blacklistedTokens = new List<TokenBlacklist>();
        _contextMock.Setup(x => x.TokenBlacklist.Add(It.IsAny<TokenBlacklist>()))
            .Callback<TokenBlacklist>(blacklistedTokens.Add);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new LogoutCommand(AccessToken: accessToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        blacklistedTokens.Should().ContainSingle();
        blacklistedTokens[0].TokenId.Should().Be(tokenId);
        blacklistedTokens[0].ExpiresAt.Should().Be(expiresAt);
        blacklistedTokens[0].Reason.Should().Be("User logout");
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAccessToken_WhenAlreadyBlacklisted_DoesNotAddDuplicate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid().ToString();
        var expiresAt = DateTime.Now.AddHours(1);
        var accessToken = "valid.jwt.token";

        _currentUserServiceMock.Setup(x => x.GetUserId()).Returns(userId);
        _tokenServiceMock.Setup(x => x.ExtractTokenInfo(accessToken))
            .Returns((tokenId, expiresAt));

        var existingBlacklist = new TokenBlacklist
        {
            Id = Guid.NewGuid(),
            TokenId = tokenId,
            ExpiresAt = expiresAt,
            BlacklistedAt = DateTime.Now
        };
        SetupTokenBlacklistDbSet(new List<TokenBlacklist> { existingBlacklist });
        SetupRefreshTokensDbSet(new List<RefreshToken>());

        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new LogoutCommand(AccessToken: accessToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _contextMock.Verify(x => x.TokenBlacklist.Add(It.IsAny<TokenBlacklist>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidAccessToken_DoesNotAddToBlacklist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accessToken = "invalid.token";

        _currentUserServiceMock.Setup(x => x.GetUserId()).Returns(userId);
        _tokenServiceMock.Setup(x => x.ExtractTokenInfo(accessToken))
            .Returns(((string?)null, (DateTime?)null));

        SetupTokenBlacklistDbSet(new List<TokenBlacklist>());
        SetupRefreshTokensDbSet(new List<RefreshToken>());

        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new LogoutCommand(AccessToken: accessToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _contextMock.Verify(x => x.TokenBlacklist.Add(It.IsAny<TokenBlacklist>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithRefreshToken_RevokesSpecificToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var refreshTokenValue = "refresh_token_value";
        var refreshToken = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = userId,
            Token = refreshTokenValue,
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddDays(7),
            RevokedAt = null
        };

        _currentUserServiceMock.Setup(x => x.GetUserId()).Returns(userId);
        SetupTokenBlacklistDbSet(new List<TokenBlacklist>());
        SetupRefreshTokensDbSet(new List<RefreshToken> { refreshToken });

        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new LogoutCommand(RefreshToken: refreshTokenValue);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        refreshToken.RevokedAt.Should().NotBeNull();
        refreshToken.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutRefreshToken_RevokesAllUserTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var refreshToken1 = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = userId,
            Token = "token1",
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddDays(7),
            RevokedAt = null
        };
        var refreshToken2 = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = userId,
            Token = "token2",
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddDays(7),
            RevokedAt = null
        };
        var revokedToken = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = userId,
            Token = "token3",
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddDays(7),
            RevokedAt = DateTime.Now.AddHours(-1)
        };

        _currentUserServiceMock.Setup(x => x.GetUserId()).Returns(userId);
        SetupTokenBlacklistDbSet(new List<TokenBlacklist>());
        SetupRefreshTokensDbSet(new List<RefreshToken> { refreshToken1, refreshToken2, revokedToken });

        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new LogoutCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        refreshToken1.RevokedAt.Should().NotBeNull();
        refreshToken2.RevokedAt.Should().NotBeNull();
        revokedToken.RevokedAt.Should().NotBeNull(); // Already revoked
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithBothTokens_BlacklistsAccessTokenAndRevokesRefreshToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid().ToString();
        var expiresAt = DateTime.Now.AddHours(1);
        var accessToken = "valid.jwt.token";
        var refreshTokenValue = "refresh_token_value";

        var refreshToken = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = userId,
            Token = refreshTokenValue,
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddDays(7),
            RevokedAt = null
        };

        _currentUserServiceMock.Setup(x => x.GetUserId()).Returns(userId);
        _tokenServiceMock.Setup(x => x.ExtractTokenInfo(accessToken))
            .Returns((tokenId, expiresAt));

        SetupTokenBlacklistDbSet(new List<TokenBlacklist>());
        SetupRefreshTokensDbSet(new List<RefreshToken> { refreshToken });

        var blacklistedTokens = new List<TokenBlacklist>();
        _contextMock.Setup(x => x.TokenBlacklist.Add(It.IsAny<TokenBlacklist>()))
            .Callback<TokenBlacklist>(blacklistedTokens.Add);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new LogoutCommand(accessToken, refreshTokenValue);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        blacklistedTokens.Should().ContainSingle();
        blacklistedTokens[0].TokenId.Should().Be(tokenId);
        refreshToken.RevokedAt.Should().NotBeNull();
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithRefreshTokenNotBelongingToUser_DoesNotRevoke()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var refreshTokenValue = "refresh_token_value";

        var refreshToken = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = otherUserId, // Different user
            Token = refreshTokenValue,
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddDays(7),
            RevokedAt = null
        };

        _currentUserServiceMock.Setup(x => x.GetUserId()).Returns(userId);
        SetupTokenBlacklistDbSet(new List<TokenBlacklist>());
        SetupRefreshTokensDbSet(new List<RefreshToken> { refreshToken });

        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new LogoutCommand(RefreshToken: refreshTokenValue);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        refreshToken.RevokedAt.Should().BeNull(); // Not revoked
    }

    private void SetupTokenBlacklistDbSet(List<TokenBlacklist> tokens)
    {
        var queryable = new TestAsyncEnumerable<TokenBlacklist>(tokens);
        var dbSetMock = new Mock<DbSet<TokenBlacklist>>();
        dbSetMock.As<IQueryable<TokenBlacklist>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<TokenBlacklist>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<TokenBlacklist>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<TokenBlacklist>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<TokenBlacklist>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());

        _contextMock.Setup(x => x.TokenBlacklist).Returns(dbSetMock.Object);
    }

    private void SetupRefreshTokensDbSet(List<RefreshToken> tokens)
    {
        var queryable = new TestAsyncEnumerable<RefreshToken>(tokens);
        var dbSetMock = new Mock<DbSet<RefreshToken>>();
        dbSetMock.As<IQueryable<RefreshToken>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<RefreshToken>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<RefreshToken>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<RefreshToken>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<RefreshToken>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());

        _contextMock.Setup(x => x.RefreshTokens).Returns(dbSetMock.Object);
    }
}
