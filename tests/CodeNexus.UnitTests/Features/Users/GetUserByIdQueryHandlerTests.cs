using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Users.Queries.GetUserById;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.Users;

public class GetUserByIdQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly GetUserByIdQueryHandler _handler;

    public GetUserByIdQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _handler = new GetUserByIdQueryHandler(_mockContext.Object);
    }

    [Fact]
    public async Task Handle_WithValidUserId_ShouldReturnUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserByIdQuery(userId);
        var user = CreateTestUser(userId);

        SetupUsersDbSet(new List<User> { user });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal("john", result.Value.Username);
        Assert.Equal("john@example.com", result.Value.Email);
        Assert.Equal("John", result.Value.FirstName);
        Assert.Equal("Doe", result.Value.LastName);
        Assert.Equal("Mentor", result.Value.RoleName);
    }

    [Fact]
    public async Task Handle_WithNonExistentUserId_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserByIdQuery(userId);

        SetupUsersDbSet(new List<User>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("USER_NOT_FOUND", result.ErrorCode);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithUserHavingProfile_ShouldReturnCompleteData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserByIdQuery(userId);
        var user = CreateTestUser(userId);

        SetupUsersDbSet(new List<User> { user });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Experienced mentor", result.Value.Bio);
        Assert.Equal("avatar.jpg", result.Value.AvatarUrl);
        Assert.Equal("123456789", result.Value.Phone);
        Assert.Equal("123 Main St", result.Value.Address);
    }

    [Fact]
    public async Task Handle_WithUserWithoutProfile_ShouldReturnUserWithNullProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserByIdQuery(userId);
        var mentorRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var user = new User
        {
            UserId = userId,
            Username = "alice",
            Email = "alice@example.com",
            FirstName = "Alice",
            LastName = "Smith",
            RoleId = mentorRole.RoleId,
            Role = mentorRole,
            CreatedAt = DateTime.UtcNow,
            UserProfile = null
        };

        SetupUsersDbSet(new List<User> { user });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Null(result.Value.Bio);
        Assert.Null(result.Value.AvatarUrl);
        Assert.Null(result.Value.Phone);
        Assert.Null(result.Value.Address);
    }

    [Fact]
    public async Task Handle_WithUserWithoutRole_ShouldReturnUserWithNullRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserByIdQuery(userId);
        var user = new User
        {
            UserId = userId,
            Username = "bob",
            Email = "bob@example.com",
            FirstName = "Bob",
            LastName = "Johnson",
            RoleId = null,
            Role = null,
            CreatedAt = DateTime.UtcNow,
            UserProfile = null
        };

        SetupUsersDbSet(new List<User> { user });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Null(result.Value.RoleName);
    }

    private User CreateTestUser(Guid userId)
    {
        var mentorRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Mentor" };

        return new User
        {
            UserId = userId,
            Username = "john",
            Email = "john@example.com",
            FirstName = "John",
            LastName = "Doe",
            RoleId = mentorRole.RoleId,
            Role = mentorRole,
            CreatedAt = DateTime.UtcNow,
            UserProfile = new UserProfile
            {
                ProfileId = Guid.NewGuid(),
                UserId = userId,
                Bio = "Experienced mentor",
                AvatarUrl = "avatar.jpg",
                Phone = "123456789",
                Address = "123 Main St"
            }
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
