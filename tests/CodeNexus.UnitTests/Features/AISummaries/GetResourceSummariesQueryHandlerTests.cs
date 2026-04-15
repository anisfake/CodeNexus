using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AISummaries.Queries.GetResourceSummaries;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.AISummaries;

public class GetResourceSummariesQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetResourceSummariesQueryHandler _handler;

    public GetResourceSummariesQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetResourceSummariesQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_ResourceNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var query = new GetResourceSummariesQuery(resourceId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Resources).Returns(new List<Resource>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("RESOURCE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsFailure()
    {
        // Arrange
        var resourceId = NewId.NextGuid();
        var ownerId = NewId.NextGuid();
        var currentUserId = NewId.NextGuid();
        var query = new GetResourceSummariesQuery(resourceId);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = ownerId,
            SubjectId = NewId.NextGuid(),
            Title = "Test",
            Type = ResourceType.PDF,
            IsDeleted = false
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(currentUserId);
        _mockContext.Setup(x => x.Resources).Returns(new List<Resource> { resource }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsOnlyActiveSummaries()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var query = new GetResourceSummariesQuery(resourceId);

        var resource = new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            SubjectId = NewId.NextGuid(),
            Title = "Test",
            Type = ResourceType.PDF,
            IsDeleted = false
        };

        var summaries = new List<AISummary>
        {
            new()
            {
                SummaryId = NewId.NextGuid(),
                ResourceId = resourceId,
                Title = "S1",
                Summary = "Summary 1",
                StartPage = 1,
                EndPage = 2,
                GeneratedAt = DateTime.UtcNow,
                IsDeleted = false
            },
            new()
            {
                SummaryId = NewId.NextGuid(),
                ResourceId = resourceId,
                Title = "S2",
                Summary = "Summary 2",
                StartPage = 3,
                EndPage = 4,
                GeneratedAt = DateTime.UtcNow.AddMinutes(-1),
                IsDeleted = false
            },
            new()
            {
                SummaryId = NewId.NextGuid(),
                ResourceId = resourceId,
                Title = "Deleted",
                Summary = "Deleted",
                StartPage = 5,
                EndPage = 6,
                GeneratedAt = DateTime.UtcNow.AddMinutes(-2),
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Resources).Returns(new List<Resource> { resource }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AISummaries).Returns(summaries.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Count.Should().Be(2);
        result.Value.Should().OnlyContain(x => x.Title != "Deleted");
    }
}
