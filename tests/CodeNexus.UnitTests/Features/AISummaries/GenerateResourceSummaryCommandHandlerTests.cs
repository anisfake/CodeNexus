using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AISummaries.Commands.GenerateResourceSummary;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.AISummaries;

public class GenerateResourceSummaryCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IAIGeneratorService> _mockAIGeneratorService;
    private readonly GenerateResourceSummaryCommandHandler _handler;

    public GenerateResourceSummaryCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockAIGeneratorService = new Mock<IAIGeneratorService>();

        _handler = new GenerateResourceSummaryCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockAIGeneratorService.Object);
    }

    [Fact]
    public async Task Handle_ResourceNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateResourceSummaryCommand(NewId.NextGuid(), 1, 3);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Resources).Returns(
            new List<Resource>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("RESOURCE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_DeletedResource_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var command = new GenerateResourceSummaryCommand(resourceId, 1, 3);

        var resource = CreateResource(resourceId, userId);
        resource.IsDeleted = true;

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Resources).Returns(
            new List<Resource> { resource }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("RESOURCE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var otherUserId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var command = new GenerateResourceSummaryCommand(resourceId, 1, 3);

        var resource = CreateResource(resourceId, otherUserId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Resources).Returns(
            new List<Resource> { resource }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_ExistingSummary_ReturnsCachedResult()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var summaryId = NewId.NextGuid();
        var command = new GenerateResourceSummaryCommand(resourceId, 2, 4);

        var resource = CreateResourceWithPages(resourceId, userId, 1, 5);

        var existingSummary = new AISummary
        {
            SummaryId = summaryId,
            ResourceId = resourceId,
            Title = "Test - Pages 2-4",
            Summary = "Existing summary content",
            StartPage = 2,
            EndPage = 4
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Resources).Returns(
            new List<Resource> { resource }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AISummaries).Returns(
            new List<AISummary> { existingSummary }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.SummaryId.Should().Be(summaryId);
        result.Value.Summary.Should().Be("Existing summary content");
        _mockAIGeneratorService.Verify(
            x => x.GenerateContentAsync(It.IsAny<string>(), It.IsAny<AIUsageType>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PagesNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var command = new GenerateResourceSummaryCommand(resourceId, 10, 12);

        var resource = CreateResourceWithPages(resourceId, userId, 1, 5);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Resources).Returns(
            new List<Resource> { resource }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AISummaries).Returns(
            new List<AISummary>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("PAGES_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_PagesWithNoTextContent_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var command = new GenerateResourceSummaryCommand(resourceId, 1, 3);

        var resource = CreateResource(resourceId, userId);
        resource.Pages = new List<ResourcePage>
        {
            new() { ResourcePageId = NewId.NextGuid(), ResourceId = resourceId, PageNumber = 1, ImageUrl = "url1", ExtractedText = null },
            new() { ResourcePageId = NewId.NextGuid(), ResourceId = resourceId, PageNumber = 2, ImageUrl = "url2", ExtractedText = "" },
            new() { ResourcePageId = NewId.NextGuid(), ResourceId = resourceId, PageNumber = 3, ImageUrl = "url3", ExtractedText = "   " }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Resources).Returns(
            new List<Resource> { resource }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AISummaries).Returns(
            new List<AISummary>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("NO_TEXT_CONTENT");
    }

    [Fact]
    public async Task Handle_ValidRequest_GeneratesSavesAndReturns()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var command = new GenerateResourceSummaryCommand(resourceId, 1, 3);

        var resource = CreateResourceWithPages(resourceId, userId, 1, 5);
        var generatedSummary = "## Summary\nThis is a generated summary...";

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Resources).Returns(
            new List<Resource> { resource }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AISummaries).Returns(
            new List<AISummary>().BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateContentAsync(It.IsAny<string>(), AIUsageType.Assistant))
            .ReturnsAsync(generatedSummary);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ResourceId.Should().Be(resourceId);
        result.Value.Summary.Should().Be(generatedSummary);
        result.Value.StartPage.Should().Be(1);
        result.Value.EndPage.Should().Be(3);
        result.Value.Title.Should().Be($"{resource.Title} - Pages 1-3");
        _mockContext.Verify(x => x.AISummaries, Times.AtLeast(1));
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AIGenerationFails_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var command = new GenerateResourceSummaryCommand(resourceId, 1, 2);

        var resource = CreateResourceWithPages(resourceId, userId, 1, 5);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Resources).Returns(
            new List<Resource> { resource }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AISummaries).Returns(
            new List<AISummary>().BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateContentAsync(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ThrowsAsync(new InvalidOperationException("AI service timeout"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("SUMMARY_GENERATION_FAILED");
        result.ErrorMessage.Should().Contain("AI service timeout");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SubjectWithDescription_IncludesInPrompt()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var resourceId = NewId.NextGuid();
        var command = new GenerateResourceSummaryCommand(resourceId, 1, 1);

        var resource = CreateResourceWithPages(resourceId, userId, 1, 3);
        resource.Subject.Description = "Advanced C# programming concepts";

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Resources).Returns(
            new List<Resource> { resource }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.AISummaries).Returns(
            new List<AISummary>().BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateContentAsync(It.IsAny<string>(), AIUsageType.Assistant))
            .ReturnsAsync("Summary content");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockAIGeneratorService.Verify(x => x.GenerateContentAsync(
            It.Is<string>(p => p.Contains("Advanced C# programming concepts")),
            AIUsageType.Assistant), Times.Once);
    }

    private static Resource CreateResource(Guid resourceId, Guid userId)
    {
        var subjectId = NewId.NextGuid();
        return new Resource
        {
            ResourceId = resourceId,
            UserId = userId,
            SubjectId = subjectId,
            Title = "Test Resource",
            Type = ResourceType.PDF,
            IsDeleted = false,
            Subject = new Subject
            {
                SubjectId = subjectId,
                CreatedByUserId = userId,
                Name = "C# Programming",
                Description = null
            },
            Pages = new List<ResourcePage>()
        };
    }

    private static Resource CreateResourceWithPages(Guid resourceId, Guid userId, int fromPage, int toPage)
    {
        var resource = CreateResource(resourceId, userId);
        resource.Pages = Enumerable.Range(fromPage, toPage - fromPage + 1)
            .Select(i => new ResourcePage
            {
                ResourcePageId = NewId.NextGuid(),
                ResourceId = resourceId,
                PageNumber = i,
                ImageUrl = $"https://example.com/page{i}.png",
                ExtractedText = $"Content of page {i}. This is sample text for testing."
            })
            .ToList();
        return resource;
    }
}
