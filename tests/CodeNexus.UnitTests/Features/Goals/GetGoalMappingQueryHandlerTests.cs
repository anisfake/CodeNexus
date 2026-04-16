using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Goals.Queries.GetGoalMapping;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Goals;

public class GetGoalMappingQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetGoalMappingQueryHandler _handler;

    public GetGoalMappingQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetGoalMappingQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WhenMappingExists_ReturnsMapping()
    {
        var userId = NewId.NextGuid();
        var userGoalId = NewId.NextGuid();
        var systemGoalId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = userGoalId,
                CreatedByUserId = userId,
                Title = "Custom Goal",
                IsSystemDefined = false},
            new()
            {
                GoalId = systemGoalId,
                Title = "System Goal",
                Description = "System goal description",
                IsSystemDefined = true}
        };

        var mappings = new List<CodeNexus.Domain.Entities.GoalMapping>
        {
            new()
            {
                MappingId = NewId.NextGuid(),
                UserGoalId = userGoalId,
                SystemGoalId = systemGoalId,
                Confidence = 0.85m,
                VerifiedByAI = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockContext.Setup(x => x.Goals).Returns(goals.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.GoalMappings).Returns(mappings.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetGoalMappingQuery(userGoalId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.SystemGoalId.Should().Be(systemGoalId);
        result.Value.SystemGoalTitle.Should().Be("System Goal");
    }

    [Fact]
    public async Task Handle_WhenNoMapping_ReturnsNull()
    {
        var userId = NewId.NextGuid();
        var userGoalId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = userGoalId,
                CreatedByUserId = userId,
                Title = "Custom Goal",
                IsSystemDefined = false}
        };

        _mockContext.Setup(x => x.Goals).Returns(goals.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.GoalMappings).Returns(new List<CodeNexus.Domain.Entities.GoalMapping>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetGoalMappingQuery(userGoalId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenGoalNotOwned_ReturnsFailure()
    {
        var userId = NewId.NextGuid();
        var userGoalId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = userGoalId,
                CreatedByUserId = NewId.NextGuid(),
                Title = "Other User Goal",
                IsSystemDefined = false}
        };

        _mockContext.Setup(x => x.Goals).Returns(goals.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetGoalMappingQuery(userGoalId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("GOAL_NOT_FOUND");
    }
}
