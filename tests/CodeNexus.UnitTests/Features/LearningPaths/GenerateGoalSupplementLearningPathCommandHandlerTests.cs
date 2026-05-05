using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateGoalSupplementLearningPath;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using GoalEntity = CodeNexus.Domain.Entities.Goals;
using CodeNexus.UnitTests.Helpers;
using MediatR;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GenerateGoalSupplementLearningPathCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext = new();
    private readonly Mock<ICurrentUserService> _mockCurrentUserService = new();
    private readonly Mock<ISender> _mockSender = new();

    [Fact]
    public async Task Handle_WhenGoalHasRemainingProgress_ShouldGenerateSupplementWithAbsoluteRemainingWeight()
    {
        var userId = Guid.NewGuid();
        var sourcePathId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var supplementPathId = Guid.NewGuid();

        SetupBaseData(
            userId,
            sourcePathId,
            subjectId,
            goalId,
            progressPercent: 80m);

        var generatedPath = new CreateLearningPathResponse(
            supplementPathId,
            "Supplement Path",
            "Supplement Description",
            new List<LearningPathGoalDto>
            {
                new(goalId, "Goal A", 0.2m, 30, "NotStarted", null, 0m, 20m)
            },
            new List<ChapterDto>(),
            0,
            DateTime.UtcNow);

        _mockSender
            .Setup(x => x.Send(
                It.Is<GenerateLearningPathSkeletonCommand>(c =>
                    c.SubjectId == subjectId &&
                    c.UseAbsoluteGoalWeights &&
                    c.Goals.Count == 1 &&
                    c.Goals[0].GoalId == goalId &&
                    c.Goals[0].Weight == 0.2m &&
                    c.GenerationContextInstruction != null &&
                    c.GenerationContextInstruction.Contains("remaining 20")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateLearningPathResponse>.Success(generatedPath));

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GenerateGoalSupplementLearningPathCommand(sourcePathId, goalId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(sourcePathId, result.Value!.SourcePathId);
        Assert.Equal(goalId, result.Value.GoalId);
        Assert.Equal(80m, result.Value.CurrentProgressPercent);
        Assert.Equal(20m, result.Value.RemainingPercent);
        Assert.Equal(supplementPathId, result.Value.LearningPath.PathId);
    }

    [Fact]
    public async Task Handle_WhenGoalAlreadyCompleted_ShouldReturnFailureAndNotGenerate()
    {
        var userId = Guid.NewGuid();
        var sourcePathId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();

        SetupBaseData(
            userId,
            sourcePathId,
            subjectId,
            goalId,
            progressPercent: 100m);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GenerateGoalSupplementLearningPathCommand(sourcePathId, goalId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("GOAL_ALREADY_COMPLETED", result.ErrorCode);
        _mockSender.Verify(
            x => x.Send(It.IsAny<GenerateLearningPathSkeletonCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSourcePathBelongsToAnotherUser_ShouldReturnAccessDenied()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var sourcePathId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath
            {
                PathId = sourcePathId,
                UserId = otherUserId,
                SubjectId = subjectId,
                Title = "Source",
                ComplexityLevel = ComplexityLevel.Beginner,
                Language = LanguageSelection.VietNamese
            }
        }.BuildMockDbSet().Object);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GenerateGoalSupplementLearningPathCommand(sourcePathId, goalId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ACCESS_DENIED", result.ErrorCode);
    }

    private GenerateGoalSupplementLearningPathCommandHandler CreateHandler()
        => new(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockSender.Object);

    private void SetupBaseData(
        Guid userId,
        Guid sourcePathId,
        Guid subjectId,
        Guid goalId,
        decimal progressPercent)
    {
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath
            {
                PathId = sourcePathId,
                UserId = userId,
                SubjectId = subjectId,
                Title = "Source Path",
                ComplexityLevel = ComplexityLevel.Intermediate,
                Language = LanguageSelection.VietNamese
            }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(new[]
        {
            new LearningPathGoal
            {
                PathId = sourcePathId,
                GoalId = goalId,
                Weight = 0.8m
            }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[]
        {
            new GoalEntity
            {
                GoalId = goalId,
                Title = "Goal A",
                Description = "Goal A description",
                Duration = GoalDuration.OneMonth
            }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.UserGoalProgresses).Returns(new[]
        {
            new UserGoalProgress
            {
                UserGoalProgressId = Guid.NewGuid(),
                UserId = userId,
                GoalId = goalId,
                LearningPathId = sourcePathId,
                ProgressPercent = progressPercent,
                Status = progressPercent >= 100m
                    ? GoalProgressStatus.Completed
                    : GoalProgressStatus.InProgress
            }
        }.BuildMockDbSet().Object);
    }
}
