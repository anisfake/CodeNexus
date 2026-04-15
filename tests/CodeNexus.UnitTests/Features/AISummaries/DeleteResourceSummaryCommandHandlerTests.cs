using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AISummaries.Commands.DeleteResourceSummary;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.AISummaries;

public class DeleteResourceSummaryCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly DeleteResourceSummaryCommandHandler _handler;

    public DeleteResourceSummaryCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new DeleteResourceSummaryCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_SummaryNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new DeleteResourceSummaryCommand(NewId.NextGuid());

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.AISummaries).Returns(new List<AISummary>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("SUMMARY_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsFailure()
    {
        // Arrange
        var ownerId = NewId.NextGuid();
        var currentUserId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var summaryId = NewId.NextGuid();

        var summary = new AISummary
        {
            SummaryId = summaryId,
            ResourceId = resourceId,
            Title = "Summary",
            Summary = "Content",
            StartPage = 1,
            EndPage = 2,
            IsDeleted = false,
            Resource = new Resource
            {
                ResourceId = resourceId,
                UserId = ownerId,
                SubjectId = NewId.NextGuid(),
                Title = "Resource",
                Type = ResourceType.PDF
            }
        };

        var command = new DeleteResourceSummaryCommand(summaryId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(currentUserId);
        _mockContext.Setup(x => x.AISummaries).Returns(new List<AISummary> { summary }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_ValidRequest_SoftDeletesSummary()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var summaryId = NewId.NextGuid();

        var summary = new AISummary
        {
            SummaryId = summaryId,
            ResourceId = resourceId,
            Title = "Summary",
            Summary = "Content",
            StartPage = 1,
            EndPage = 2,
            IsDeleted = false,
            Resource = new Resource
            {
                ResourceId = resourceId,
                UserId = userId,
                SubjectId = NewId.NextGuid(),
                Title = "Resource",
                Type = ResourceType.PDF
            }
        };

        var command = new DeleteResourceSummaryCommand(summaryId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.AISummaries).Returns(new List<AISummary> { summary }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        summary.IsDeleted.Should().BeTrue();
        summary.DeletedAt.Should().NotBeNull();
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
