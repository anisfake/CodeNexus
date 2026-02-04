using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.Commands.UploadAvatar;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Users;

public class UploadAvatarCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ICloudinaryService> _mockCloudinaryService;
    private readonly UploadAvatarCommandHandler _handler;

    public UploadAvatarCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCloudinaryService = new Mock<ICloudinaryService>();
        _handler = new UploadAvatarCommandHandler(_mockContext.Object, _mockCloudinaryService.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WithValidFile_UploadsAvatarAndReturnsUrl()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var fileName = "avatar.jpg";
        var uploadedUrl = "https://cloudinary.com/user_avatar/abc123.jpg";

        var userProfile = new UserProfile
        {
            ProfileId = NewId.NextGuid(),
            UserId = userId,
            AvatarUrl = null
        };

        var command = new UploadAvatarCommand(imageStream, fileName);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.UserProfiles).Returns(new[] { userProfile }.AsQueryable().BuildMockDbSet().Object);
        _mockCloudinaryService.Setup(x => x.UploadImageAsync(imageStream, fileName, "user_avatar"))
            .ReturnsAsync(uploadedUrl);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(uploadedUrl, result.Value);
        Assert.Equal(uploadedUrl, userProfile.AvatarUrl);
        _mockCloudinaryService.Verify(x => x.UploadImageAsync(imageStream, fileName, "user_avatar"), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithUserProfileNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var fileName = "avatar.jpg";

        var command = new UploadAvatarCommand(imageStream, fileName);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.UserProfiles).Returns(new List<UserProfile>().AsQueryable().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("USER_NOT_FOUND", result.ErrorCode);
        _mockCloudinaryService.Verify(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithCloudinaryUploadFailure_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var fileName = "avatar.jpg";

        var userProfile = new UserProfile
        {
            ProfileId = NewId.NextGuid(),
            UserId = userId,
            AvatarUrl = null
        };

        var command = new UploadAvatarCommand(imageStream, fileName);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.UserProfiles).Returns(new[] { userProfile }.AsQueryable().BuildMockDbSet().Object);
        _mockCloudinaryService.Setup(x => x.UploadImageAsync(imageStream, fileName, "user_avatar"))
            .ReturnsAsync((string)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("UPLOAD_AVATAR_FAILED", result.ErrorCode);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithExistingAvatar_ReplacesOldAvatar()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var fileName = "avatar.jpg";
        var oldAvatarUrl = "https://cloudinary.com/user_avatar/old123.jpg";
        var newAvatarUrl = "https://cloudinary.com/user_avatar/new456.jpg";

        var userProfile = new UserProfile
        {
            ProfileId = NewId.NextGuid(),
            UserId = userId,
            AvatarUrl = oldAvatarUrl
        };

        var command = new UploadAvatarCommand(imageStream, fileName);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.UserProfiles).Returns(new[] { userProfile }.AsQueryable().BuildMockDbSet().Object);
        _mockCloudinaryService.Setup(x => x.UploadImageAsync(imageStream, fileName, "user_avatar"))
            .ReturnsAsync(newAvatarUrl);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(newAvatarUrl, result.Value);
        Assert.Equal(newAvatarUrl, userProfile.AvatarUrl);
        Assert.NotEqual(oldAvatarUrl, userProfile.AvatarUrl);
    }

    [Fact]
    public async Task Handle_WithEmptyFileName_StillUploads()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var fileName = string.Empty;
        var uploadedUrl = "https://cloudinary.com/user_avatar/abc123.jpg";

        var userProfile = new UserProfile
        {
            ProfileId = NewId.NextGuid(),
            UserId = userId,
            AvatarUrl = null
        };

        var command = new UploadAvatarCommand(imageStream, fileName);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.UserProfiles).Returns(new[] { userProfile }.AsQueryable().BuildMockDbSet().Object);
        _mockCloudinaryService.Setup(x => x.UploadImageAsync(imageStream, fileName, "user_avatar"))
            .ReturnsAsync(uploadedUrl);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(uploadedUrl, result.Value);
    }
}
