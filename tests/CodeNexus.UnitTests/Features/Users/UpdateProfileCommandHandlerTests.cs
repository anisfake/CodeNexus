using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Users.Commands.UpdateProfile;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Users;

public class UpdateProfileCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IAchievementService> _mockAchievementService;
    private readonly UpdateProfileCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateProfileCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockAchievementService = new Mock<IAchievementService>();
        _mockContext.Setup(x => x.UserProfiles).Returns(new Mock<DbSet<UserProfile>>().Object);
        _handler = new UpdateProfileCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockAchievementService.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldUpdateProfileSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_userId);
        SetupUsersDbSet(new List<User> { user });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateProfileCommand(
            "UpdatedFirstName",
            "UpdatedLastName",
            "Updated bio",
            new DateTime(1990, 1, 1),
            "0123456789",
            "Updated address",
            new TimeSpan(20, 0, 0)
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.FirstName.Should().Be("UpdatedFirstName");
        result.Value.LastName.Should().Be("UpdatedLastName");
        result.Value.Bio.Should().Be("Updated bio");
        result.Value.Phone.Should().Be("0123456789");
        result.Value.Address.Should().Be("Updated address");
        result.Value.DailyReminderTime.Should().Be(new TimeSpan(20, 0, 0));
    }

    [Fact]
    public async Task Handle_UserNotFound_ShouldReturnFailure()
    {
        // Arrange
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_userId);
        SetupUsersDbSet(new List<User>());

        var command = new UpdateProfileCommand("John", "Doe", null, null, null, null, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_PartialUpdate_ShouldUpdateOnlyProvidedFields()
    {
        // Arrange
        var user = CreateTestUser();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_userId);
        SetupUsersDbSet(new List<User> { user });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateProfileCommand(
            "NewFirstName",
            null,
            null,
            null,
            null,
            null,
            null
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.FirstName.Should().Be("NewFirstName");
        result.Value.LastName.Should().Be("Doe");
        result.Value.Bio.Should().Be("Original bio");
        result.Value.DailyReminderTime.Should().Be(new TimeSpan(19, 0, 0));
    }

    [Fact]
    public async Task Handle_UpdateAllFields_ShouldUpdateSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_userId);
        SetupUsersDbSet(new List<User> { user });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var newDate = new DateTime(1995, 5, 15);
        var command = new UpdateProfileCommand(
            "Alice",
            "Smith",
            "New bio text",
            newDate,
            "0987654321",
            "New address",
            new TimeSpan(21, 0, 0)
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.FirstName.Should().Be("Alice");
        result.Value.LastName.Should().Be("Smith");
        result.Value.Bio.Should().Be("New bio text");
        result.Value.DateOfBirth.Should().Be(newDate);
        result.Value.Phone.Should().Be("0987654321");
        result.Value.Address.Should().Be("New address");
        result.Value.DailyReminderTime.Should().Be(new TimeSpan(21, 0, 0));
    }

    [Fact]
    public async Task Handle_UserWithoutProfile_ShouldCreateProfileAndUpdateSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        user.UserProfile = null!;

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_userId);
        SetupUsersDbSet(new List<User> { user });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateProfileCommand(
            "Mentor",
            "NoProfile",
            "Mentor bio",
            new DateTime(1992, 2, 2),
            "0123456789",
            "Mentor Address",
            new TimeSpan(20, 30, 0)
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.FirstName.Should().Be("Mentor");
        result.Value.LastName.Should().Be("NoProfile");
        result.Value.Bio.Should().Be("Mentor bio");
        result.Value.Phone.Should().Be("0123456789");
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
            RoleId = Guid.NewGuid(),
            UserProfile = new UserProfile
            {
                UserId = _userId,
                Bio = "Original bio",
                DateOfBirth = new DateTime(1990, 1, 1),
                Phone = "0123456789",
                Address = "Original address",
                AvatarUrl = "https://example.com/avatar.jpg",
                DailyReminderTime = new TimeSpan(19, 0, 0)
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
