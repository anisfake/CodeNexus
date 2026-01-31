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
    private readonly Mock<IOTPService> _otpServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _otpServiceMock = new Mock<IOTPService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _handler = new LoginCommandHandler(_contextMock.Object, _otpServiceMock.Object, _tokenServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        SetupUsersDbSet(new List<User>());
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
        _otpServiceMock.Setup(x => x.VerifyOtp("Password123", "hash")).Returns(false);

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
        var user = new User { UserId = Guid.NewGuid(), Email = "test@test.com", Username = "test", PasswordHash = "hash" };
        SetupUsersDbSet(new List<User> { user });

        _otpServiceMock.Setup(x => x.VerifyOtp("Password123", "hash")).Returns(true);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.Is<User>(u => u.UserId == user.UserId))).Returns("jwt");

        var command = new LoginCommand("test", "Password123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be("jwt");
        result.Value.UserId.Should().Be(user.UserId);
        result.Value.Email.Should().Be(user.Email);
        result.Value.Username.Should().Be(user.Username);
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
}
