using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Users.Commands.BanUser;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Users;

public class BanUserCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IUserCacheService> _mockUserCacheService;
    private readonly BanUserCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public BanUserCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockUserCacheService = new Mock<IUserCacheService>();
        _mockUserCacheService
            .Setup(x => x.InvalidateUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new BanUserCommandHandler(_mockContext.Object, _mockUserCacheService.Object);
    }

    [Fact]
    public async Task Handle_ValidUserId_ShouldBanUserAndRevokeTokens()
    {
        // Arrange
        var user = CreateTestUser();
        var activeToken1 = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = _userId,
            Token = "token1",
            RevokedAt = null
        };
        var activeToken2 = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = _userId,
            Token = "token2",
            RevokedAt = null
        };
        user.RefreshTokens = new List<RefreshToken> { activeToken1, activeToken2 };

        SetupUsersDbSet(new List<User> { user });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new BanUserCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be("Banned");
        activeToken1.RevokedAt.Should().NotBeNull();
        activeToken2.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_UserNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupUsersDbSet(new List<User>());
        var command = new BanUserCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        result.ErrorMessage.Should().Be("User not found.");
    }

    [Fact]
    public async Task Handle_UserAlreadyBanned_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        user.Status = "Banned";
        SetupUsersDbSet(new List<User> { user });

        var command = new BanUserCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_ALREADY_BANNED");
        result.ErrorMessage.Should().Be("User is already banned.");
    }

    [Fact]
    public async Task Handle_UserWithNoActiveTokens_ShouldBanUserSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var revokedToken = new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = _userId,
            Token = "token1",
            RevokedAt = DateTime.Now.AddDays(-1)
        };
        user.RefreshTokens = new List<RefreshToken> { revokedToken };

        SetupUsersDbSet(new List<User> { user });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new BanUserCommand(_userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be("Banned");
        revokedToken.RevokedAt.Should().NotBeNull();
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
            RoleId = Guid.NewGuid(),
            RefreshTokens = new List<RefreshToken>()
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
