using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Goals.Queries.GetGoals;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Goals;

public class GetGoalsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetGoalsQueryHandler _handler;

    public GetGoalsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();

        _handler = new GetGoalsQueryHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object
        );
    }

    [Fact]
    public async Task Handle_UserHasGoals_ReturnsAll()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetGoalsQuery();

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Learn Python Basics",
                Description = "Master Python fundamentals",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now.AddDays(-1)
            },
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Build Spring Boot App",
                Description = "Create a REST API with Spring Boot",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals).Returns(
            goals.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].Title.Should().Be("Build Spring Boot App");
        result.Value[1].Title.Should().Be("Learn Python Basics");
    }

    [Fact]
    public async Task Handle_UserHasNoGoals_ReturnsEmptyList()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetGoalsQuery();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals).Returns(
            new List<CodeNexus.Domain.Entities.Goals>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OnlyReturnsCurrentUserGoals()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var otherUserId = NewId.NextGuid();
        var query = new GetGoalsQuery();

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "My Goal",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now
            },
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = otherUserId,
                Title = "Other User Goal",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals).Returns(
            goals.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Title.Should().Be("My Goal");
    }
}
