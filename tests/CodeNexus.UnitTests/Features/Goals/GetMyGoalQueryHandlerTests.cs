using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Goals.Queries.GetMyGoal;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Goals;

public class GetMyGoalQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMyGoalQueryHandler _handler;

    public GetMyGoalQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();

        _handler = new GetMyGoalQueryHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsPaginatedGoals()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyGoalQuery
        {
            PageNumber = 1,
            PageSize = 10
        };

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Learn C#",
                Description = "Master C# fundamentals",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now.AddDays(-2),
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            },
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Build API",
                Description = "Create REST API",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now.AddDays(-1),
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals).Returns(goals.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_WithSearchTerm_ReturnsFilteredGoals()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyGoalQuery
        {
            SearchTerm = "C#",
            PageNumber = 1,
            PageSize = 10
        };

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Learn C#",
                Description = "Master C# fundamentals",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now,
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            },
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Learn Python",
                Description = "Master Python",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now,
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals).Returns(goals.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Title.Should().Be("Learn C#");
    }

    [Fact]
    public async Task Handle_WithSearchTermInDescription_ReturnsFilteredGoals()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyGoalQuery
        {
            SearchTerm = "fundamentals",
            PageNumber = 1,
            PageSize = 10
        };

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Learn C#",
                Description = "Master C# fundamentals",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now,
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            },
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Build API",
                Description = "Create REST API",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now,
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals).Returns(goals.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Title.Should().Be("Learn C#");
    }

    [Fact]
    public async Task Handle_WithSortDescending_ReturnsSortedByCreatedAtDesc()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyGoalQuery
        {
            SortDescending = true,
            PageNumber = 1,
            PageSize = 10
        };

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Old Goal",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now.AddDays(-5),
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            },
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "New Goal",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now,
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals).Returns(goals.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items[0].Title.Should().Be("New Goal");
        result.Value.Items[1].Title.Should().Be("Old Goal");
    }

    [Fact]
    public async Task Handle_WithSortAscending_ReturnsSortedByCreatedAtAsc()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyGoalQuery
        {
            SortDescending = false,
            PageNumber = 1,
            PageSize = 10
        };

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Old Goal",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now.AddDays(-5),
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            },
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "New Goal",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now,
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals).Returns(goals.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items[0].Title.Should().Be("Old Goal");
        result.Value.Items[1].Title.Should().Be("New Goal");
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyGoalQuery
        {
            PageNumber = 2,
            PageSize = 2
        };

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Goal 1",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now.AddDays(-3),
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            },
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Goal 2",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now.AddDays(-2),
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            },
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "Goal 3",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now.AddDays(-1),
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals).Returns(goals.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Title.Should().Be("Goal 1");
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_UserHasNoGoals_ReturnsEmptyPaginatedResult()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyGoalQuery
        {
            PageNumber = 1,
            PageSize = 10
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals).Returns(
            new List<CodeNexus.Domain.Entities.Goals>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnlyReturnsCurrentUserGoals()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var otherUserId = NewId.NextGuid();
        var query = new GetMyGoalQuery
        {
            PageNumber = 1,
            PageSize = 10
        };

        var goals = new List<CodeNexus.Domain.Entities.Goals>
        {
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = userId,
                Title = "My Goal",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now,
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            },
            new()
            {
                GoalId = NewId.NextGuid(),
                CreatedByUserId = otherUserId,
                Title = "Other User Goal",
                IsSystemDefined = false,
                CreatedAt = DateTime.Now,
                UserGoalProgresses = new List<CodeNexus.Domain.Entities.UserGoalProgress>()
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Goals).Returns(goals.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Title.Should().Be("My Goal");
    }
}