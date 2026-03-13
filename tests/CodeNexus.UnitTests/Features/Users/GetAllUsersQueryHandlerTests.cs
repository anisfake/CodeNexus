using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Users.Queries.GetAllUsers;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.Users;

public class GetAllUsersQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly GetAllUsersQueryHandler _handler;

    public GetAllUsersQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _handler = new GetAllUsersQueryHandler(_mockContext.Object);
    }

    [Fact]
    public async Task Handle_WithDefaultParameters_ShouldReturnPaginatedUsers()
    {
        // Arrange
        var query = new GetAllUsersQuery();
        var users = CreateTestUsers();

        SetupUsersDbSet(users);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task Handle_WithRoleFilter_ShouldReturnFilteredUsers()
    {
        // Arrange
        var query = new GetAllUsersQuery { Role = "Mentor" };
        var users = CreateTestUsers();

        SetupUsersDbSet(users);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Mentor", result.Items[0].RoleName);
    }

    [Fact]
    public async Task Handle_WithSearchTerm_ShouldReturnMatchingUsers()
    {
        // Arrange
        var query = new GetAllUsersQuery { SearchTerm = "alice" };
        var users = CreateTestUsers();

        SetupUsersDbSet(users);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Contains("alice", result.Items[0].Username.ToLower());
    }

    [Fact]
    public async Task Handle_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var query = new GetAllUsersQuery { PageNumber = 1, PageSize = 2 };
        var users = CreateTestUsers();

        SetupUsersDbSet(users);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public async Task Handle_WithSortByUsername_ShouldReturnSortedUsers()
    {
        // Arrange
        var query = new GetAllUsersQuery { SortBy = UserSortBy.Username, SortDescending = false };
        var users = CreateTestUsers();

        SetupUsersDbSet(users);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("alice", result.Items[0].Username);
        Assert.Equal("bob", result.Items[1].Username);
        Assert.Equal("john", result.Items[2].Username);
    }

    [Fact]
    public async Task Handle_WithEmptyDatabase_ShouldReturnEmptyResult()
    {
        // Arrange
        var query = new GetAllUsersQuery();
        SetupUsersDbSet(new List<User>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    private List<User> CreateTestUsers()
    {
        var mentorRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Mentor" };
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };

        return new List<User>
        {
            new User
            {
                UserId = Guid.NewGuid(),
                Username = "john",
                Email = "john@example.com",
                FirstName = "John",
                LastName = "Doe",
                RoleId = mentorRole.RoleId,
                Role = mentorRole,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UserProfile = new UserProfile
                {
                    ProfileId = Guid.NewGuid(),
                    Bio = "Experienced mentor",
                    AvatarUrl = "avatar1.jpg",
                    Phone = "123456789",
                    Address = "123 Main St"
                }
            },
            new User
            {
                UserId = Guid.NewGuid(),
                Username = "alice",
                Email = "alice@example.com",
                FirstName = "Alice",
                LastName = "Smith",
                RoleId = studentRole.RoleId,
                Role = studentRole,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UserProfile = new UserProfile
                {
                    ProfileId = Guid.NewGuid(),
                    Bio = "Eager learner",
                    AvatarUrl = "avatar2.jpg"
                }
            },
            new User
            {
                UserId = Guid.NewGuid(),
                Username = "bob",
                Email = "bob@example.com",
                FirstName = "Bob",
                LastName = "Johnson",
                RoleId = studentRole.RoleId,
                Role = studentRole,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UserProfile = null
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
