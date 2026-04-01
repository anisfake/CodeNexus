using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Resources.Queries.GetResourcePages;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Resources;

public class GetResourcePagesQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetResourcePagesQueryHandler _handler;

    public GetResourcePagesQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetResourcePagesQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WithValidResource_ShouldReturnResourcePages()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Test Resource",
            Type = ResourceType.PDF,
            FilePath = "test.pdf",
            OriginalFileName = "test.pdf",
            TotalPages = 3,
            SubjectId = Guid.NewGuid(),
            IsDeleted = false,
            Pages = new List<ResourcePage>
            {
                new ResourcePage
                {
                    ResourcePageId = Guid.NewGuid(),
                    PageNumber = 1,
                    ImageUrl = "page1.jpg",
                    ExtractedText = "Page 1 content"
                },
                new ResourcePage
                {
                    ResourcePageId = Guid.NewGuid(),
                    PageNumber = 2,
                    ImageUrl = "page2.jpg",
                    ExtractedText = "Page 2 content"
                },
                new ResourcePage
                {
                    ResourcePageId = Guid.NewGuid(),
                    PageNumber = 3,
                    ImageUrl = "page3.jpg",
                    ExtractedText = "Page 3 content"
                }
            }
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var query = new GetResourcePagesQuery { ResourceId = resourceId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(resourceId, result.Value.ResourceId);
        Assert.Equal("Test Resource", result.Value.Title);
        Assert.Equal("test.pdf", result.Value.OriginalFileName);
        Assert.Equal(3, result.Value.TotalPages);
        Assert.Equal(3, result.Value.Pages.Count);
        Assert.Equal(1, result.Value.Pages[0].PageNumber);
        Assert.Equal("page1.jpg", result.Value.Pages[0].ImageUrl);
        Assert.Equal("Page 1 content", result.Value.Pages[0].ExtractedText);
    }

    [Fact]
    public async Task Handle_WithNonExistentResource_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        SetupResourcesDbSet(new List<Resource>());
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var query = new GetResourcePagesQuery { ResourceId = resourceId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("RESOURCE_NOT_FOUND", result.ErrorCode);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithWrongUser_ShouldReturnUnauthorized()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = ownerId,
            Title = "Test Resource",
            Type = ResourceType.PDF,
            SubjectId = Guid.NewGuid(),
            IsDeleted = false,
            Pages = new List<ResourcePage>()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(otherUserId);

        var query = new GetResourcePagesQuery { ResourceId = resourceId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Contains("User not authenticated", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithDeletedResource_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Test Resource",
            Type = ResourceType.PDF,
            SubjectId = Guid.NewGuid(),
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            Pages = new List<ResourcePage>()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var query = new GetResourcePagesQuery { ResourceId = resourceId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("RESOURCE_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithResourceWithoutPages_ShouldReturnEmptyPagesList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            Title = "Test Resource",
            Type = ResourceType.PDF,
            FilePath = "test.pdf",
            OriginalFileName = "test.pdf",
            TotalPages = 0,
            SubjectId = Guid.NewGuid(),
            IsDeleted = false,
            Pages = new List<ResourcePage>()
        };

        SetupResourcesDbSet(new List<Resource> { resource });
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var query = new GetResourcePagesQuery { ResourceId = resourceId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Pages);
        Assert.Equal(0, result.Value.TotalPages);
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

        dbSetMock.As<IQueryable<Resource>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<Resource>(queryable.AsQueryable().Provider));

        _mockContext.Setup(x => x.Resources).Returns(dbSetMock.Object);
    }
}
