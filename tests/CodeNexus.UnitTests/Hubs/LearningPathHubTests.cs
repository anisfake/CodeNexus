using CodeNexus.API.Hubs;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Hubs;

public class LearningPathHubTests
{
    private readonly Mock<ISender> _mockSender;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<ISingleClientProxy> _mockClientProxy;
    private readonly LearningPathHub _hub;

    public LearningPathHubTests()
    {
        _mockSender = new Mock<ISender>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<ISingleClientProxy>();
        
        _mockClients.Setup(x => x.Caller).Returns(_mockClientProxy.Object);
        
        _hub = new LearningPathHub(_mockSender.Object);
        
        // Use reflection to set the Clients property
        var clientsProperty = typeof(Hub).GetProperty("Clients");
        clientsProperty?.SetValue(_hub, _mockClients.Object);
    }

    [Fact]
    public async Task RequestLearningPathGeneration_WithValidInput_ShouldEmitSuccessEvents()
    {
        // Arrange
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var pathId = Guid.NewGuid();
        var goals = new List<LearningPathGoalRequest>
        {
            new LearningPathGoalRequest(goalId, 1m)
        };
        
        var expectedResponse = new CreateLearningPathResponse(
            pathId,
            "Test Learning Path",
            "Test Description",
            new List<LearningPathGoalDto>
            {
                new LearningPathGoalDto(goalId, "Goal 1", 1m, 30)
            },
            new List<ChapterDto>
            {
                new ChapterDto(Guid.NewGuid(), "Chapter 1", null, 0, new List<LessonDto>(), new List<TaskDto>())
            },
            1,
            DateTime.UtcNow,
            true
        );

        _mockSender.Setup(x => x.Send(It.IsAny<GenerateLearningPathSkeletonCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateLearningPathResponse>.Success(expectedResponse));

        // Act
        await _hub.RequestLearningPathGeneration(subjectId, goals, "Beginner", "VietNamese");

        // Assert
        _mockClientProxy.Verify(x => x.SendCoreAsync(
            "LearningPathGenerationStarted",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockClientProxy.Verify(x => x.SendCoreAsync(
            "LearningPathCreated",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockClientProxy.Verify(x => x.SendCoreAsync(
            "LearningPathGenerationCompleted",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestLearningPathGeneration_WithInvalidComplexity_ShouldEmitError()
    {
        // Arrange
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var goals = new List<LearningPathGoalRequest>
        {
            new LearningPathGoalRequest(goalId, 1m)
        };

        // Act
        await _hub.RequestLearningPathGeneration(subjectId, goals, "InvalidComplexity", "VietNamese");

        // Assert
        _mockClientProxy.Verify(x => x.SendCoreAsync(
            "LearningPathGenerationError",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestLearningPathGeneration_WithFailedCommand_ShouldEmitError()
    {
        // Arrange
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var goals = new List<LearningPathGoalRequest>
        {
            new LearningPathGoalRequest(goalId, 1m)
        };

        _mockSender.Setup(x => x.Send(It.IsAny<GenerateLearningPathSkeletonCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateLearningPathResponse>.Failure("TEST_ERROR", "Test error message"));

        // Act
        await _hub.RequestLearningPathGeneration(subjectId, goals, "Beginner", "VietNamese");

        // Assert
        _mockClientProxy.Verify(x => x.SendCoreAsync(
            "LearningPathGenerationError",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
