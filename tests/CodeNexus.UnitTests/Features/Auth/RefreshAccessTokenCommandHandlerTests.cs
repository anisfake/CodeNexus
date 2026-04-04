using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Auth.Commands.RefreshAccessToken;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class RefreshAccessTokenCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly RefreshAccessTokenCommandHandler _handler;

    public RefreshAccessTokenCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _tokenServiceMock = new Mock<ITokenService>();
        _tokenServiceMock.Setup(x => x.HashRefreshToken(It.IsAny<string>())).Returns((string s) => s);
        _tokenServiceMock.Setup(x => x.RefreshTokenExpirationDays).Returns(7);
        _handler = new RefreshAccessTokenCommandHandler(_contextMock.Object, _tokenServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenNotFound_ReturnsFailure()
    {
        // Arrange
        SetupRefreshTokensDbSet(new List<RefreshToken>());

        var command = new RefreshAccessTokenCommand("nonexistent-token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenRevoked_RevokesAllUserTokensAndReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId, Email = "test@test.com", Username = "test" };

        var revokedToken = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = userId,
            User = user,
            Token = "revoked-token",
            ExpiresAt = DateTime.Now.AddDays(7),
            RevokedAt = DateTime.Now.AddMinutes(-5)
        };

        // This is the token the hacker got — should be revoked by reuse detection
        var activeToken = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = userId,
            User = user,
            Token = "hacker-active-token",
            ExpiresAt = DateTime.Now.AddDays(7),
            RevokedAt = null
        };

        SetupRefreshTokensDbSet(new List<RefreshToken> { revokedToken, activeToken });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new RefreshAccessTokenCommand("revoked-token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_REUSE_DETECTED");
        activeToken.RevokedAt.Should().NotBeNull("all active tokens should be revoked on reuse detection");
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenExpired_ReturnsFailure()
    {
        // Arrange
        var user = new User { UserId = Guid.NewGuid(), Email = "test@test.com", Username = "test" };

        var expiredToken = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            Token = "expired-token",
            ExpiresAt = DateTime.Now.AddDays(-1),
            RevokedAt = null
        };

        SetupRefreshTokensDbSet(new List<RefreshToken> { expiredToken });

        var command = new RefreshAccessTokenCommand("expired-token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_EXPIRED");
    }

    [Fact]
    public async Task Handle_WhenValidRefreshToken_RevokesOldAndReturnsNewTokens()
    {
        // Arrange
        var role = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "test@test.com",
            Username = "test",
            RoleId = role.RoleId,
            Role = role
        };

        var validToken = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            Token = "valid-refresh-token",
            ExpiresAt = DateTime.Now.AddDays(5),
            RevokedAt = null
        };

        var refreshTokens = new List<RefreshToken> { validToken };
        SetupRefreshTokensDbSet(refreshTokens);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.Is<User>(u => u.UserId == user.UserId))).Returns("new-jwt");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("new-refresh-token");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new RefreshAccessTokenCommand("valid-refresh-token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be("new-jwt");
        result.Value.RefreshToken.Should().Be("new-refresh-token");
        result.Value.UserId.Should().Be(user.UserId);
        result.Value.Email.Should().Be(user.Email);
        result.Value.RoleName.Should().Be("Student");

        // Old token should be revoked
        validToken.RevokedAt.Should().NotBeNull("old refresh token should be revoked");

        // New refresh token should be added
        refreshTokens.Should().HaveCount(2);
        refreshTokens[1].Token.Should().Be("new-refresh-token");
        refreshTokens[1].UserId.Should().Be(user.UserId);
    }

    [Fact]
    public async Task Handle_WhenRevokedTokenUsed_OtherUsersTokensAreNotAffected()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var user = new User { UserId = userId, Email = "test@test.com", Username = "test" };

        var revokedToken = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = userId,
            User = user,
            Token = "reused-token",
            ExpiresAt = DateTime.Now.AddDays(7),
            RevokedAt = DateTime.Now.AddMinutes(-5)
        };

        var otherUserToken = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = otherUserId,
            Token = "other-user-token",
            ExpiresAt = DateTime.Now.AddDays(7),
            RevokedAt = null
        };

        SetupRefreshTokensDbSet(new List<RefreshToken> { revokedToken, otherUserToken });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new RefreshAccessTokenCommand("reused-token");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        otherUserToken.RevokedAt.Should().BeNull("other users' tokens should not be affected");
    }

    private void SetupRefreshTokensDbSet(List<RefreshToken> refreshTokens)
    {
        var queryable = new TestAsyncEnumerable<RefreshToken>(refreshTokens);
        var dbSetMock = new Mock<DbSet<RefreshToken>>();
        dbSetMock.As<IQueryable<RefreshToken>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<RefreshToken>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<RefreshToken>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<RefreshToken>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<RefreshToken>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());

        dbSetMock.Setup(x => x.Add(It.IsAny<RefreshToken>())).Callback<RefreshToken>(rt => refreshTokens.Add(rt));
        _contextMock.Setup(x => x.RefreshTokens).Returns(dbSetMock.Object);
    }
}
