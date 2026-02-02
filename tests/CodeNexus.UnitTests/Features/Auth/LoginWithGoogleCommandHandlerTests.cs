using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Auth.Commands.LoginWithGoogle;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class LoginWithGoogleCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IGoogleAuthService> _googleAuthMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly LoginWithGoogleCommandHandler _handler;

    public LoginWithGoogleCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _googleAuthMock = new Mock<IGoogleAuthService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _handler = new LoginWithGoogleCommandHandler(_contextMock.Object, _googleAuthMock.Object, _tokenServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenGoogleTokenInvalid_ReturnsFailure()
    {
        // Arrange
        _googleAuthMock
            .Setup(x => x.ValidateIdTokenAsync("bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GoogleUserInfo?)null);

        var command = new LoginWithGoogleCommand("bad");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_GOOGLE_TOKEN");
    }

    [Fact]
    public async Task Handle_WhenUserExists_ReturnsAccessToken()
    {
        // Arrange
        var user = new User { UserId = Guid.NewGuid(), Email = "user@gmail.com", Username = "user" };
        SetupUsersDbSet(new List<User> { user });

        var refreshTokens = new List<RefreshToken>();
        SetupRefreshTokensDbSet(refreshTokens);

        _googleAuthMock
            .Setup(x => x.ValidateIdTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleUserInfo(user.Email, "A", "LE", "A LE"));

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.Is<User>(u => u.UserId == user.UserId))).Returns("jwt");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("rt");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(new LoginWithGoogleCommand("ok"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be("jwt");
        result.Value.RefreshToken.Should().Be("rt");
        result.Value.Email.Should().Be(user.Email);
        result.Value.Username.Should().Be(user.Username);

        refreshTokens.Should().HaveCount(1);
        refreshTokens[0].UserId.Should().Be(user.UserId);
        refreshTokens[0].Token.Should().Be("rt");
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_CreatesUserAndReturnsAccessToken()
    {
        // Arrange
        var users = new List<User>();
        SetupUsersDbSet(users);

        var refreshTokens = new List<RefreshToken>();
        SetupRefreshTokensDbSet(refreshTokens);

        _googleAuthMock
            .Setup(x => x.ValidateIdTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleUserInfo("new@gmail.com", "New", "User", "New User"));

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>())).Returns("jwt");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("rt");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _contextMock.Setup(x => x.Users.Add(It.IsAny<User>())).Callback<User>(u => users.Add(u));

        // Act
        var result = await _handler.Handle(new LoginWithGoogleCommand("ok"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        users.Should().HaveCount(1);
        users[0].Email.Should().Be("new@gmail.com");
        result.Value.AccessToken.Should().Be("jwt");
        result.Value.RefreshToken.Should().Be("rt");

        refreshTokens.Should().HaveCount(1);
        refreshTokens[0].UserId.Should().Be(users[0].UserId);
        refreshTokens[0].Token.Should().Be("rt");
    }

    private void SetupUsersDbSet(List<User> users)
    {
        var queryable = new TestAsyncEnumerable<User>(users);
        var dbSetMock = new Mock<DbSet<User>>();
        dbSetMock.As<IQueryable<User>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<User>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<User>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());

        dbSetMock.Setup(x => x.Add(It.IsAny<User>()));
        _contextMock.Setup(x => x.Users).Returns(dbSetMock.Object);
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
