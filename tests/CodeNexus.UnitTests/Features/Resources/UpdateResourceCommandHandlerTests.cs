using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Resources.Commands.UpdateResource;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Resources;

public class UpdateResourceCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ICloudinaryService> _mockCloudinaryService;
    private readonly UpdateResourceCommandHandler _handler;

    public UpdateResourceCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCloudinaryService = new Mock<ICloudinaryService>();
        _handler = new UpdateResourceCommandHandler(
            _mockContext.Object, 
            _mockCurrentUserService.Object,
            _mockCloudinaryService.Object);
    }

    [Fact]
    public async Task Handle_UpdateLinkResource_ShouldUpdateUrlAndMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var command = new UpdateResourceCommand(
            resourceId,
            "Updated Title",
            "Updated description",
            "https://updated-url.com",
            null,
            null
        );

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Old Title",
            URL = "https://old-url.com",
            Description = "Old description",
            Type = ResourceType.Link,
            SubjectId = Guid.NewGuid()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Title", resource.Title);
        Assert.Equal("Updated description", resource.Description);
        Assert.Equal("https://updated-url.com", resource.URL);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateFileResource_ShouldUploadNewFile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UpdateResourceCommand(
            resourceId,
            "Updated Title",
            "Updated description",
            null,
            fileStream,
            "newfile.pdf"
        );

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Old Title",
            FilePath = "https://res.cloudinary.com/demo/raw/upload/v1234567890/resources/user123/old-file.pdf",
            Description = "Old description",
            Type = ResourceType.File,
            SubjectId = Guid.NewGuid()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockCloudinaryService.Setup(x => x.DeleteFileAsync("resources/user123/old-file")).ReturnsAsync(true);
        _mockCloudinaryService.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("new-file-path.pdf");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Title", resource.Title);
        Assert.Equal("Updated description", resource.Description);
        Assert.Equal("new-file-path.pdf", resource.FilePath);
        _mockCloudinaryService.Verify(x => x.DeleteFileAsync(It.IsAny<string>()), Times.Once);
        _mockCloudinaryService.Verify(x => x.UploadFileAsync(It.IsAny<Stream>(), "newfile.pdf", $"resources/{userId}"), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentResource_ShouldReturnNotFound()
    {
        // Arrange
        var command = new UpdateResourceCommand(Guid.NewGuid(), "Title", null, null, null, null);
        SetupResourcesDbSet(new List<Resource>());
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("RESOURCE_NOT_FOUND", result.ErrorCode);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithWrongUser_ShouldReturnUnauthorized()
    {
        // Arrange
        var resourceId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var command = new UpdateResourceCommand(resourceId, "New Title", null, null, null, null);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = ownerId,
            Title = "Old Title",
            Type = ResourceType.File,
            SubjectId = Guid.NewGuid()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(otherUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Contains("your own resources", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_UpdateFileResourceWithoutFile_ShouldUpdateOnlyMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var command = new UpdateResourceCommand(resourceId, "New Title", "New description", null, null, null);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Old Title",
            FilePath = "old-file.pdf",
            Description = "Old description",
            Type = ResourceType.File,
            SubjectId = Guid.NewGuid()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("New Title", resource.Title);
        Assert.Equal("New description", resource.Description);
        Assert.Equal("old-file.pdf", resource.FilePath); // FilePath should not change
        _mockCloudinaryService.Verify(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFileUploadFails_ShouldReturnError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UpdateResourceCommand(resourceId, null, null, null, fileStream, "file.pdf");

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Title",
            Type = ResourceType.File,
            SubjectId = Guid.NewGuid()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockCloudinaryService.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("UPLOAD_FAIL", result.ErrorCode);
        Assert.Contains("upload failed", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_UpdateFileResourceWithUrl_ShouldReturnInvalidUpdate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var command = new UpdateResourceCommand(resourceId, null, null, "https://example.com", null, null);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Title",
            Type = ResourceType.File,
            FilePath = "file.pdf",
            SubjectId = Guid.NewGuid()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_UPDATE", result.ErrorCode);
        Assert.Contains("Cannot update URL for a File resource", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_UpdateLinkResourceWithFile_ShouldReturnInvalidUpdate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UpdateResourceCommand(resourceId, null, null, null, fileStream, "file.pdf");

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Title",
            Type = ResourceType.Link,
            URL = "https://example.com",
            SubjectId = Guid.NewGuid()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_UPDATE", result.ErrorCode);
        Assert.Contains("Cannot upload file for a Link resource", result.ErrorMessage);
    }

    private void SetupResourcesDbSet(List<Resource> resources)
    {
        var queryable = new TestAsyncEnumerable<Resource>(resources);
        var dbSetMock = new Mock<DbSet<Resource>>();
        dbSetMock.As<IQueryable<Resource>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<Resource>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<Resource>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<Resource>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<Resource>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.Resources).Returns(dbSetMock.Object);
    }
}
