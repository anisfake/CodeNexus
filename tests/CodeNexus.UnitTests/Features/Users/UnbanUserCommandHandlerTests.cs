using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Users.Commands.UnbanUser;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Users;

public class UnbanUserCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly UnbanUserCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public UnbanUserCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _handler = new UnbanUserCommandHandler(_mockContext.Object);
    }

    [Fact]
    public async Task Handle_ValidBannedUser_ShouldUnbanSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        user.Status = "Banned";
        SetupUsersDbSet(new List<User> { user });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UnbanUserCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_UserNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupUsersDbSet(new List<User>());
        var command = new UnbanUserCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        result.ErrorMessage.Should().Be("User not found.");
    }

    [Fact]
    public async Task Handle_UserNotBanned_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        user.Status = "Active";
        SetupUsersDbSet(new List<User> { user });

        var command = new UnbanUserCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_BANNED");
        result.ErrorMessage.Should().Be("User is not banned.");
    }

    [Fact]
    public async Task Handle_UnbanUser_ShouldAllowLoginAgain()
    {
        // Arrange
        var user = CreateTestUser();
        user.Status = "Banned";
        SetupUsersDbSet(new List<User> { user });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UnbanUserCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be("Active");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private User CreateTestUser()
    {
        return new User
        {
            UserId = _userId,
            Username = "testuser",
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            PasswordHash = "hash",
            Status = "Active",
            RoleId = Guid.NewGuid()
        };
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
        _mockContext.Setup(x => x.Users).Returns(dbSetMock.Object);
    }
}
