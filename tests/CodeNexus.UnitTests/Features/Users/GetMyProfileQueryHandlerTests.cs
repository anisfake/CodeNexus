using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.DTOs;
using CodeNexus.Application.Features.Users.Queries.GetMyProfile;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Users;

public class GetMyProfileQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMyProfileQueryHandler _handler;

    public GetMyProfileQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetMyProfileQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WithValidUser_ReturnsUserProfile()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyProfileQuery();

        var userProfile = new UserProfile
        {
            ProfileId = NewId.NextGuid(),
            UserId = userId,
            Bio = "Test bio",
            AvatarUrl = "https://example.com/avatar.jpg",
            DateOfBirth = new DateTime(1990, 1, 1),
            Phone = "1234567890",
            Address = "123 Test St",
            DailyReminderTime = new TimeSpan(20, 0, 0)
        };

        var user = new User
        {
            UserId = userId,
            Email = "test@example.com",
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            UserProfile = userProfile
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("test@example.com", result.Value.Email);
        Assert.Equal("Test", result.Value.FirstName);
        Assert.Equal("Test bio", result.Value.Bio);
        Assert.Equal("https://example.com/avatar.jpg", result.Value.AvatarUrl);
        Assert.Equal(new TimeSpan(20, 0, 0), result.Value.DailyReminderTime);
    }

    [Fact]
    public async Task Handle_WithUserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyProfileQuery();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new List<User>().AsQueryable().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("USER_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithUserWithoutProfile_ReturnsEmptyProfileFields()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyProfileQuery();

        var user = new User
        {
            UserId = userId,
            Email = "test@example.com",
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            UserProfile = null
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(string.Empty, result.Value.Bio);
        Assert.Null(result.Value.AvatarUrl);
        Assert.Null(result.Value.DailyReminderTime);
    }

    [Fact]
    public async Task Handle_WithNullableFields_ReturnsEmptyStringsForNullValues()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyProfileQuery();

        var userProfile = new UserProfile
        {
            ProfileId = NewId.NextGuid(),
            UserId = userId,
            Bio = null,
            AvatarUrl = null,
            DateOfBirth = null,
            Phone = null,
            Address = null
        };

        var user = new User
        {
            UserId = userId,
            Email = "test@example.com",
            Username = "testuser",
            FirstName = null,
            LastName = null,
            UserProfile = userProfile
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(string.Empty, result.Value.FirstName);
        Assert.Equal(string.Empty, result.Value.LastName);
        Assert.Equal(string.Empty, result.Value.Bio);
        Assert.Null(result.Value.AvatarUrl);
    }
}
