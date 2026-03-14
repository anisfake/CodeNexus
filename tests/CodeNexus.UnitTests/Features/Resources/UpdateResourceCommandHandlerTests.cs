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
    private readonly Mock<IPdfProcessingService> _mockPdfProcessingService;
    private readonly Mock<IResourceCacheService> _mockResourceCacheService;
    private readonly UpdateResourceCommandHandler _handler;

    public UpdateResourceCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCloudinaryService = new Mock<ICloudinaryService>();
        _mockPdfProcessingService = new Mock<IPdfProcessingService>();
        _mockResourceCacheService = new Mock<IResourceCacheService>();
        _mockResourceCacheService
            .Setup(x => x.InvalidateUserResourcesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockResourceCacheService
            .Setup(x => x.InvalidateResourcePagesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new UpdateResourceCommandHandler(
            _mockContext.Object, 
            _mockCurrentUserService.Object,
            _mockCloudinaryService.Object,
            _mockPdfProcessingService.Object,
            _mockResourceCacheService.Object);
    }

    [Fact]
    public async Task Handle_UpdateResourceMetadata_ShouldUpdateTitleAndDescription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var command = new UpdateResourceCommand(
            resourceId,
            "Updated Title",
            "Updated description",
            null,
            null
        );

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Old Title",
            FilePath = "https://old-url.com/file.pdf",
            Description = "Old description",
            Type = ResourceType.PDF,
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
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdatePdfFile_ShouldUploadNewFileAndDeleteOld()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UpdateResourceCommand(
            resourceId,
            "Updated Title",
            "Updated description",
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
            Type = ResourceType.PDF,
            SubjectId = Guid.NewGuid(),
            Pages = new List<ResourcePage>()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        SetupResourcePagesDbSet(new List<ResourcePage>());
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockCloudinaryService.Setup(x => x.DeleteFileAsync(It.IsAny<string>())).ReturnsAsync(true);
        _mockCloudinaryService.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("new-file-path.pdf");
        
        var pdfProcessingResult = new PdfProcessingResult
        {
            TotalPages = 5,
            Pages = new List<PdfPageData>
            {
                new PdfPageData { PageNumber = 1, ImageUrl = "page1.jpg", ExtractedText = "Text 1" },
                new PdfPageData { PageNumber = 2, ImageUrl = "page2.jpg", ExtractedText = "Text 2" }
            }
        };
        _mockPdfProcessingService.Setup(x => x.ProcessPdfAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(pdfProcessingResult);
        
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Title", resource.Title);
        Assert.Equal("Updated description", resource.Description);
        Assert.Equal("new-file-path.pdf", resource.FilePath);
        Assert.Equal("newfile.pdf", resource.OriginalFileName);
        Assert.Equal(5, resource.TotalPages);
        _mockCloudinaryService.Verify(x => x.DeleteFileAsync(It.IsAny<string>()), Times.Once);
        _mockCloudinaryService.Verify(x => x.UploadFileAsync(It.IsAny<Stream>(), "newfile.pdf", $"resources/{userId}"), Times.Once);
        _mockPdfProcessingService.Verify(x => x.ProcessPdfAsync(It.IsAny<Stream>(), userId.ToString()), Times.Once);
        _mockContext.Verify(x => x.ResourcePages.AddRangeAsync(It.IsAny<IEnumerable<ResourcePage>>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentResource_ShouldReturnNotFound()
    {
        // Arrange
        var command = new UpdateResourceCommand(Guid.NewGuid(), "Title", null, null, null);
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
        var command = new UpdateResourceCommand(resourceId, "New Title", null, null, null);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = ownerId,
            Title = "Old Title",
            Type = ResourceType.PDF,
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
        var command = new UpdateResourceCommand(resourceId, "New Title", "New description", null, null);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Old Title",
            FilePath = "old-file.pdf",
            Description = "Old description",
            Type = ResourceType.PDF,
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
        Assert.Equal("old-file.pdf", resource.FilePath);
        _mockCloudinaryService.Verify(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFileUploadFails_ShouldReturnError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UpdateResourceCommand(resourceId, null, null, fileStream, "file.pdf");

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Title",
            Type = ResourceType.PDF,
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
    public async Task Handle_WithNonPdfFile_ShouldReturnInvalidFileType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UpdateResourceCommand(resourceId, null, null, fileStream, "file.txt");

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Title",
            Type = ResourceType.PDF,
            FilePath = "file.pdf",
            SubjectId = Guid.NewGuid()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_FILE_TYPE", result.ErrorCode);
        Assert.Contains("Only PDF files are allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ShouldReturnError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var command = new UpdateResourceCommand(resourceId, "New Title", null, null, null);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Old Title",
            Type = ResourceType.PDF,
            SubjectId = Guid.NewGuid()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("ERROR", result.ErrorCode);
        Assert.Contains("Database error", result.ErrorMessage);
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

    private void SetupResourcePagesDbSet(List<ResourcePage> pages)
    {
        var dbSetMock = new Mock<DbSet<ResourcePage>>();
        dbSetMock.Setup(m => m.AddRangeAsync(It.IsAny<IEnumerable<ResourcePage>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        dbSetMock.Setup(m => m.RemoveRange(It.IsAny<IEnumerable<ResourcePage>>()));
        _mockContext.Setup(x => x.ResourcePages).Returns(dbSetMock.Object);
    }
}
