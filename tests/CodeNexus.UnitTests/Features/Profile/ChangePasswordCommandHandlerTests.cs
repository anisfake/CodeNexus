using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Profile.Commands.ChangePassword;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Profile;

public class ChangePasswordCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IPasswordService> _passwordServiceMock;
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _passwordServiceMock = new Mock<IPasswordService>();
        _handler = new ChangePasswordCommandHandler(_contextMock.Object, _passwordServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        SetupUsersDbSet(new List<User>());
        var userId = Guid.NewGuid();
        var command = new ChangePasswordCommand(userId, "CurrentPass123", "NewPass456");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        result.ErrorMessage.Should().Be("User not found");
    }

    [Fact]
    public async Task Handle_WhenCurrentPasswordIncorrect_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Email = "test@test.com",
            Username = "testuser",
            PasswordHash = "hashedOldPassword"
        };
        SetupUsersDbSet(new List<User> { user });

        _passwordServiceMock.Setup(x => x.VerifyPassword("WrongPassword", "hashedOldPassword"))
            .Returns(false);

        var command = new ChangePasswordCommand(userId, "WrongPassword", "NewPass456");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CURRENT_PASSWORD");
        result.ErrorMessage.Should().Be("Current password is incorrect");
        _passwordServiceMock.Verify(x => x.HashPassword(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValidPasswordChange_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Email = "test@test.com",
            Username = "testuser",
            PasswordHash = "hashedOldPassword"
        };
        SetupUsersDbSet(new List<User> { user });

        _passwordServiceMock.Setup(x => x.VerifyPassword("CurrentPass123", "hashedOldPassword"))
            .Returns(true);
        _passwordServiceMock.Setup(x => x.HashPassword("NewPass456"))
            .Returns("hashedNewPassword");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new ChangePasswordCommand(userId, "CurrentPass123", "NewPass456");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("hashedNewPassword");
        _passwordServiceMock.Verify(x => x.VerifyPassword("CurrentPass123", "hashedOldPassword"), Times.Once);
        _passwordServiceMock.Verify(x => x.HashPassword("NewPass456"), Times.Once);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPasswordChanged_ShouldUpdateDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var originalPasswordHash = "hashedOldPassword";
        var newPasswordHash = "hashedNewPassword";
        var user = new User
        {
            UserId = userId,
            Email = "test@test.com",
            Username = "testuser",
            PasswordHash = originalPasswordHash
        };
        SetupUsersDbSet(new List<User> { user });

        _passwordServiceMock.Setup(x => x.VerifyPassword("CurrentPass123", originalPasswordHash))
            .Returns(true);
        _passwordServiceMock.Setup(x => x.HashPassword("NewPass456"))
            .Returns(newPasswordHash);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new ChangePasswordCommand(userId, "CurrentPass123", "NewPass456");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(newPasswordHash);
        user.PasswordHash.Should().NotBe(originalPasswordHash);
    }

    [Fact]
    public async Task Handle_WhenUserExistsButPasswordServiceFails_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Email = "test@test.com",
            Username = "testuser",
            PasswordHash = "hashedOldPassword"
        };
        SetupUsersDbSet(new List<User> { user });

        _passwordServiceMock.Setup(x => x.VerifyPassword("CurrentPass123", "hashedOldPassword"))
            .Returns(false);

        var command = new ChangePasswordCommand(userId, "CurrentPass123", "NewPass456");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CURRENT_PASSWORD");
        user.PasswordHash.Should().Be("hashedOldPassword"); // Password should not change
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
