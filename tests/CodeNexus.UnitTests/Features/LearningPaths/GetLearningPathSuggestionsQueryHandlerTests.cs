using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathSuggestions;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GetLearningPathSuggestionsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IGoalValidationService> _mockGoalValidationService;
    private readonly GetLearningPathSuggestionsQueryHandler _handler;

    public GetLearningPathSuggestionsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockGoalValidationService = new Mock<IGoalValidationService>();

        _handler = new GetLearningPathSuggestionsQueryHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockGoalValidationService.Object);
    }

    [Fact]
    public async Task Handle_WhenWeightMismatch_ShouldNotSuggest()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var systemGoalId = Guid.NewGuid();
        var pathId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.Subjects).Returns(new[]
        {
            new Subject { SubjectId = subjectId, Name = "C#", Description = "C# subject" }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Goals).Returns(new[]
        {
            new CodeNexus.Domain.Entities.Goals
            {
                GoalId = systemGoalId,
                Title = "Become Backend Developer",
                IsSystemDefined = true}
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.SubjectGoals).Returns(new[]
        {
            new SubjectGoal { SubjectId = subjectId, GoalId = systemGoalId }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.GoalMappings).Returns(new List<GoalMapping>().BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath
            {
                PathId = pathId,
                SubjectId = subjectId,
                Title = "LP A",
                Description = "Desc",
                Language = LanguageSelection.English,
                ComplexityLevel = ComplexityLevel.Beginner
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPathGoals).Returns(new[]
        {
            new LearningPathGoal
            {
                PathId = pathId,
                GoalId = systemGoalId,
                Weight = 0.3m
            }
        }.BuildMockDbSet().Object);

        var query = new GetLearningPathSuggestionsQuery(
            subjectId,
            new List<LearningPathGoalRequest> { new(systemGoalId, 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenExactMatchAndWeightsAligned_ShouldSuggest()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var systemGoalId = Guid.NewGuid();
        var pathId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.Subjects).Returns(new[]
        {
            new Subject { SubjectId = subjectId, Name = "C#", Description = "C# subject" }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Goals).Returns(new[]
        {
            new CodeNexus.Domain.Entities.Goals
            {
                GoalId = systemGoalId,
                Title = "Become Backend Developer",
                IsSystemDefined = true}
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.SubjectGoals).Returns(new[]
        {
            new SubjectGoal { SubjectId = subjectId, GoalId = systemGoalId }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.GoalMappings).Returns(new List<GoalMapping>().BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath
            {
                PathId = pathId,
                SubjectId = subjectId,
                Title = "LP A",
                Description = "Desc",
                Language = LanguageSelection.English,
                ComplexityLevel = ComplexityLevel.Beginner
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPathGoals).Returns(new[]
        {
            new LearningPathGoal
            {
                PathId = pathId,
                GoalId = systemGoalId,
                Weight = 1m
            }
        }.BuildMockDbSet().Object);

        var query = new GetLearningPathSuggestionsQuery(
            subjectId,
            new List<LearningPathGoalRequest> { new(systemGoalId, 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Score.Should().Be(1m);
    }

    [Fact]
    public async Task Handle_WhenCandidateBelongsToCurrentUser_ShouldNotSuggestOwnPath()
    {
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var systemGoalId = Guid.NewGuid();
        var ownPathId = Guid.NewGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        _mockContext.Setup(x => x.Subjects).Returns(new[]
        {
            new Subject { SubjectId = subjectId, Name = "Python", Description = "Python subject" }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Goals).Returns(new[]
        {
            new CodeNexus.Domain.Entities.Goals
            {
                GoalId = systemGoalId,
                Title = "Build API",
                IsSystemDefined = true}
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.SubjectGoals).Returns(new[]
        {
            new SubjectGoal { SubjectId = subjectId, GoalId = systemGoalId }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.GoalMappings).Returns(new List<GoalMapping>().BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPaths).Returns(new[]
        {
            new LearningPath
            {
                PathId = ownPathId,
                UserId = userId,
                SubjectId = subjectId,
                Title = "My Path",
                Description = "Desc",
                Language = LanguageSelection.English,
                ComplexityLevel = ComplexityLevel.Beginner
            }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.LearningPathGoals).Returns(new[]
        {
            new LearningPathGoal
            {
                PathId = ownPathId,
                GoalId = systemGoalId,
                Weight = 1m
            }
        }.BuildMockDbSet().Object);

        var query = new GetLearningPathSuggestionsQuery(
            subjectId,
            new List<LearningPathGoalRequest> { new(systemGoalId, 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
