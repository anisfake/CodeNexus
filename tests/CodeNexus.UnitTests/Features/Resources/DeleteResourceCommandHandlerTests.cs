using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Resources.Commands.DeleteResource;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Resources;

public class DeleteResourceCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ICloudinaryService> _mockCloudinaryService;
    private readonly Mock<IResourceCacheService> _mockResourceCacheService;
    private readonly DeleteResourceCommandHandler _handler;

    public DeleteResourceCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCloudinaryService = new Mock<ICloudinaryService>();
        _mockResourceCacheService = new Mock<IResourceCacheService>();
        _mockResourceCacheService
            .Setup(x => x.InvalidateUserResourcesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockResourceCacheService
            .Setup(x => x.InvalidateResourcePagesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new DeleteResourceCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockCloudinaryService.Object,
            _mockResourceCacheService.Object);
    }

    [Fact]
    public async Task Handle_WithValidPdfResource_ShouldSoftDeleteResourceAndDeleteFile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var command = new DeleteResourceCommand(resourceId);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Test Resource",
            Type = ResourceType.PDF,
            FilePath = "https://res.cloudinary.com/demo/raw/upload/v1234567890/resources/user123/file.pdf",
            SubjectId = Guid.NewGuid(),
            IsDeleted = false
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockCloudinaryService.Setup(x => x.DeleteFileAsync(It.IsAny<string>())).ReturnsAsync(true);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("deleted successfully", result.Value);
        Assert.True(resource.IsDeleted);
        Assert.NotNull(resource.DeletedAt);
        _mockCloudinaryService.Verify(x => x.DeleteFileAsync(It.IsAny<string>()), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentResource_ShouldReturnNotFound()
    {
        // Arrange
        var command = new DeleteResourceCommand(Guid.NewGuid());
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
        var command = new DeleteResourceCommand(resourceId);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = ownerId,
            Title = "Test Resource",
            Type = ResourceType.PDF,
            SubjectId = Guid.NewGuid(),
            IsDeleted = false
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
    public async Task Handle_WhenSaveChangesFails_ShouldReturnError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var command = new DeleteResourceCommand(resourceId);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Test Resource",
            Type = ResourceType.PDF,
            SubjectId = Guid.NewGuid(),
            IsDeleted = false
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

    [Fact]
    public async Task Handle_WithAlreadyDeletedResource_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var command = new DeleteResourceCommand(resourceId);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Test Resource",
            Type = ResourceType.PDF,
            SubjectId = Guid.NewGuid(),
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("RESOURCE_NOT_FOUND", result.ErrorCode);
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
