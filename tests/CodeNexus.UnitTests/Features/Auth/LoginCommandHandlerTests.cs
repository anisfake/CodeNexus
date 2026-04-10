using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Auth.Commands.Login;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IPasswordService> _passwordServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IDailyReminderTimeInferenceService> _dailyReminderTimeInferenceServiceMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _passwordServiceMock = new Mock<IPasswordService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _dailyReminderTimeInferenceServiceMock = new Mock<IDailyReminderTimeInferenceService>();
        _dailyReminderTimeInferenceServiceMock
            .Setup(x => x.InferDailyReminderTimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimeSpan(20, 0, 0));
        _tokenServiceMock.Setup(x => x.HashRefreshToken(It.IsAny<string>())).Returns((string s) => s);
        _handler = new LoginCommandHandler(
            _contextMock.Object,
            _passwordServiceMock.Object,
            _tokenServiceMock.Object,
            _dailyReminderTimeInferenceServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        SetupUsersDbSet(new List<User>());
        SetupTokenBlacklistDbSet(new List<TokenBlacklist>());
        var command = new LoginCommand("missing@test.com", "Password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenPasswordInvalid_ReturnsFailure()
    {
        // Arrange
        var user = new User { UserId = Guid.NewGuid(), Email = "test@test.com", Username = "test", PasswordHash = "hash" };
        SetupUsersDbSet(new List<User> { user });
        SetupTokenBlacklistDbSet(new List<TokenBlacklist>());
        _passwordServiceMock.Setup(x => x.VerifyPassword("Password123", "hash")).Returns(false);

        var command = new LoginCommand("test@test.com", "Password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenValidCredentials_ReturnsAccessToken()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "test@test.com",
            Username = "test",
            PasswordHash = "hash",
            UserProfile = new UserProfile
            {
                ProfileId = Guid.NewGuid(),
                DailyReminderTime = new TimeSpan(21, 0, 0)
            }
        };
        SetupUsersDbSet(new List<User> { user });
        SetupTokenBlacklistDbSet(new List<TokenBlacklist>());

        var refreshTokens = new List<RefreshToken>();
        SetupRefreshTokensDbSet(refreshTokens);

        _passwordServiceMock.Setup(x => x.VerifyPassword("Password123", "hash")).Returns(true);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.Is<User>(u => u.UserId == user.UserId))).Returns("jwt");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("rt");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("test", "Password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be("jwt");
        result.Value.RefreshToken.Should().Be("rt");
        result.Value.UserId.Should().Be(user.UserId);
        result.Value.Email.Should().Be(user.Email);
        result.Value.Username.Should().Be(user.Username);
        result.Value.ShouldPromptDailyReminderTime.Should().BeFalse();

        refreshTokens.Should().HaveCount(1);
        refreshTokens[0].UserId.Should().Be(user.UserId);
        refreshTokens[0].Token.Should().Be("rt");
    }

    [Fact]
    public async Task Handle_FirstSuccessfulLoginAndMissingReminder_ShouldSetPromptFlagTrue()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "first@test.com",
            Username = "first",
            PasswordHash = "hash",
            LastLogin = null,
            UserProfile = new UserProfile
            {
                ProfileId = Guid.NewGuid(),
                DailyReminderTime = null
            }
        };

        SetupUsersDbSet(new List<User> { user });
        SetupTokenBlacklistDbSet(new List<TokenBlacklist>());
        var refreshTokens = new List<RefreshToken>();
        SetupRefreshTokensDbSet(refreshTokens);

        _passwordServiceMock.Setup(x => x.VerifyPassword("Password123", "hash")).Returns(true);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>())).Returns("jwt");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("rt");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(new LoginCommand("first", "Password123"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ShouldPromptDailyReminderTime.Should().BeTrue();
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
}
