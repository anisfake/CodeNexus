using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Resources.Queries.GetMyResources;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;

namespace CodeNexus.UnitTests.Features.Resources;

public class GetMyResourcesQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMyResourcesQueryHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();

    public GetMyResourcesQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetMyResourcesQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    private List<Resource> CreateTestResources()
    {
        var subject = new Subject
        {
            SubjectId = _subjectId,
            Name = "C# Programming",
            CreatedByUserId = Guid.NewGuid()
        };

        return new List<Resource>
        {
            new Resource
            {
                ResourceId = Guid.NewGuid(),
                UserId = _userId,
                SubjectId = _subjectId,
                Title = "C# Basics",
                Type = ResourceType.File,
                URL = "https://example.com/csharp-basics.pdf",
                Description = "Introduction to C#",
                FilePath = "/resources/csharp-basics.pdf",
                OriginalFileName = "csharp-basics.pdf",
                UploadedAt = DateTime.UtcNow.AddDays(-5),
                Subject = subject
            },
            new Resource
            {
                ResourceId = Guid.NewGuid(),
                UserId = _userId,
                SubjectId = _subjectId,
                Title = "Advanced C#",
                Type = ResourceType.Link,
                URL = "https://example.com/advanced-csharp",
                Description = "Advanced C# concepts",
                FilePath = null,
                OriginalFileName = null,
                UploadedAt = DateTime.UtcNow.AddDays(-3),
                Subject = subject
            }
        };
    }

    [Fact]
    public async Task Handle_WithValidQuery_ReturnsResourcesWithPagination()
    {
        // Arrange
        var resources = CreateTestResources();
        _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(_userId);
        _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

        var query = new GetMyResourcesQuery
        {
            PageNumber = 1,
            PageSize = 10,
            Type = ResourceType.All,
            SortDescending = true
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Items[0].Title.Should().Be("Advanced C#");
        result.Items[0].ResourceId.Should().Be(resources[1].ResourceId);
        result.Items[1].Title.Should().Be("C# Basics");
        result.Items[1].ResourceId.Should().Be(resources[0].ResourceId);
    }

    [Fact]
    public async Task Handle_WithSearchTerm_ReturnsFilteredResources()
    {
        // Arrange
        var resources = CreateTestResources();
        _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(_userId);
        _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

        var query = new GetMyResourcesQuery
        {
            PageNumber = 1,
            PageSize = 10,
            Type = ResourceType.All,
            SearchTerm = "Advanced"
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Advanced C#");
        result.Items[0].ResourceId.Should().Be(resources[1].ResourceId);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithSubjectFilter_ReturnsResourcesForSpecificSubject()
    {
        // Arrange
        var subject2 = new Subject
        {
            SubjectId = Guid.NewGuid(),
            Name = "Java",
            CreatedByUserId = Guid.NewGuid()
        };

        var resources = CreateTestResources();
        resources.Add(new Resource
        {
            ResourceId = Guid.NewGuid(),
            UserId = _userId,
            SubjectId = subject2.SubjectId,
            Title = "Java Guide",
            Type = ResourceType.File,
            URL = "https://example.com/java.pdf",
            Description = "Java guide",
            FilePath = "/resources/java.pdf",
            OriginalFileName = "java.pdf",
            UploadedAt = DateTime.UtcNow,
            Subject = subject2
        });

        _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(_userId);
        _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

        var query = new GetMyResourcesQuery
        {
            PageNumber = 1,
            PageSize = 10,
            Type = ResourceType.All,
            SubjectId = _subjectId
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(r => r.SubjectId.Should().Be(_subjectId));
        result.Items.Should().OnlyContain(r => r.ResourceId == resources[0].ResourceId || r.ResourceId == resources[1].ResourceId);
    }

    [Fact]
    public async Task Handle_WithTypeFilter_ReturnsResourcesOfSpecificType()
    {
        // Arrange
        var resources = CreateTestResources();
        _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(_userId);
        _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

        var query = new GetMyResourcesQuery
        {
            PageNumber = 1,
            PageSize = 10,
            Type = ResourceType.File
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Type.Should().Be(ResourceType.File);
        result.Items[0].Title.Should().Be("C# Basics");
        result.Items[0].ResourceId.Should().Be(resources[0].ResourceId);
    }

    [Fact]
    public async Task Handle_WithSortByTitle_ReturnsSortedByTitle()
    {
        // Arrange
        var resources = CreateTestResources();
        _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(_userId);
        _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

        var query = new GetMyResourcesQuery
        {
            PageNumber = 1,
            PageSize = 10,
            Type = ResourceType.All,
            SortBy = ResourceSortBy.Title,
            SortDescending = false
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items[0].Title.Should().Be("Advanced C#");
        result.Items[1].Title.Should().Be("C# Basics");
    }

    [Fact]
    public async Task Handle_WithNoResources_ReturnsEmptyList()
    {
        // Arrange
        var resources = new List<Resource>();
        _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(_userId);
        _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

        var query = new GetMyResourcesQuery
        {
            PageNumber = 1,
            PageSize = 10,
            Type = ResourceType.All
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var subject = new Subject
        {
            SubjectId = _subjectId,
            Name = "Programming",
            CreatedByUserId = Guid.NewGuid()
        };

        var resources = Enumerable.Range(1, 15).Select(i => new Resource
        {
            ResourceId = Guid.NewGuid(),
            UserId = _userId,
            SubjectId = _subjectId,
            Title = $"Resource {i}",
            Type = ResourceType.File,
            URL = $"https://example.com/resource{i}.pdf",
            Description = $"Resource {i} description",
            FilePath = $"/resources/resource{i}.pdf",
            OriginalFileName = $"resource{i}.pdf",
            UploadedAt = DateTime.UtcNow.AddDays(-i),
            Subject = subject
        }).ToList();

        _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(_userId);
        _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

        var query = new GetMyResourcesQuery
        {
            PageNumber = 2,
            PageSize = 5,
            Type = ResourceType.All
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(5);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.TotalCount.Should().Be(15);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithSearchInDescription_ReturnsMatchingResources()
    {
        // Arrange
        var resources = CreateTestResources();
        _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(_userId);
        _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

        var query = new GetMyResourcesQuery
        {
            PageNumber = 1,
            PageSize = 10,
            Type = ResourceType.All,
            SearchTerm = "Introduction"
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("C# Basics");
        result.Items[0].ResourceId.Should().Be(resources[0].ResourceId);
    }
}
