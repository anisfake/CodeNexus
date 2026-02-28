using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathByUserId;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using GoalEntity = CodeNexus.Domain.Entities.Goals;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GetLearningPathByUserIdQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly GetLearningPathByUserIdQueryHandler _handler;
    private readonly Guid _studentUserId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly Guid _goalId = Guid.NewGuid();

    public GetLearningPathByUserIdQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _handler = new GetLearningPathByUserIdQueryHandler(_mockContext.Object);
    }

    private List<LearningPath> GetTestLearningPaths()
    {
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var studentUser = new User
        {
            UserId = _studentUserId,
            Username = "student1",
            Email = "student1@test.com",
            PasswordHash = "hash",
            RoleId = studentRole.RoleId,
            Role = studentRole
        };

        var subject = new Subject
        {
            SubjectId = _subjectId,
            Name = "Mathematics",
            Description = "Math subject",
            CreatedByUserId = Guid.NewGuid()
        };

        var goal = new GoalEntity
        {
            GoalId = _goalId,
            Title = "Learn Calculus",
            Description = "Master calculus",
            CreatedByUserId = _studentUserId,
            IsSystemDefined = false,
            IsActive = true
        };

        return new List<LearningPath>
        {
            new LearningPath
            {
                PathId = Guid.NewGuid(),
                UserId = _studentUserId,
                SubjectId = _subjectId,
                GoalId = _goalId,
                Title = "Calculus Path",
                Description = "Learn calculus step by step",
                Status = LearningPathStatus.Active.ToString(),
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                CreatedByType = true,
                User = studentUser,
                Subject = subject,
                Goal = goal,
                Chapters = new List<Chapter>()
            },
            new LearningPath
            {
                PathId = Guid.NewGuid(),
                UserId = _studentUserId,
                SubjectId = _subjectId,
                GoalId = _goalId,
                Title = "Algebra Path",
                Description = "Learn algebra fundamentals",
                Status = LearningPathStatus.InProgress.ToString(),
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                CreatedByType = false,
                User = studentUser,
                Subject = subject,
                Goal = goal,
                Chapters = new List<Chapter>()
            }
        };
    }

    [Fact]
    public async Task Handle_ValidUserId_ReturnsLearningPaths()
    {
        // Arrange
        var learningPaths = GetTestLearningPaths();
        SetupUsersDbSet(new List<User> { learningPaths[0].User });
        SetupLearningPathsDbSet(learningPaths);
        var query = new GetLearningPathByUserIdQuery(_studentUserId);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        SetupUsersDbSet(new List<User>());
        var query = new GetLearningPathByUserIdQuery(nonExistentUserId);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WithSearchTerm_ReturnsFilteredResults()
    {
        // Arrange
        var learningPaths = GetTestLearningPaths();
        SetupUsersDbSet(new List<User> { learningPaths[0].User });
        SetupLearningPathsDbSet(learningPaths);
        var query = new GetLearningPathByUserIdQuery(_studentUserId, SearchTerm: "calculus");

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.First().Title.Should().Contain("Calculus");
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsFilteredResults()
    {
        // Arrange
        var learningPaths = GetTestLearningPaths();
        SetupUsersDbSet(new List<User> { learningPaths[0].User });
        SetupLearningPathsDbSet(learningPaths);
        var query = new GetLearningPathByUserIdQuery(_studentUserId, Status: LearningPathStatus.Active);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.First().Status.Should().Be(LearningPathStatus.Active.ToString());
    }

    [Fact]
    public async Task Handle_WithSubjectIdFilter_ReturnsFilteredResults()
    {
        // Arrange
        var learningPaths = GetTestLearningPaths();
        SetupUsersDbSet(new List<User> { learningPaths[0].User });
        SetupLearningPathsDbSet(learningPaths);
        var query = new GetLearningPathByUserIdQuery(_studentUserId, SubjectId: _subjectId);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().AllSatisfy(lp => lp.SubjectId.Should().Be(_subjectId));
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var learningPaths = GetTestLearningPaths();
        SetupUsersDbSet(new List<User> { learningPaths[0].User });
        SetupLearningPathsDbSet(learningPaths);
        var query = new GetLearningPathByUserIdQuery(_studentUserId, PageNumber: 1, PageSize: 1);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(1);
        result.Value.TotalCount.Should().Be(2);
        result.Value.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Handle_SortDescending_ReturnsSortedByCreatedAtDesc()
    {
        // Arrange
        var learningPaths = GetTestLearningPaths();
        SetupUsersDbSet(new List<User> { learningPaths[0].User });
        SetupLearningPathsDbSet(learningPaths);
        var query = new GetLearningPathByUserIdQuery(_studentUserId, SortDescending: true);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.First().Title.Should().Be("Algebra Path");
        result.Value.Items.Last().Title.Should().Be("Calculus Path");
    }

    private void SetupUsersDbSet(List<User> users)
    {
        var queryable = new TestAsyncEnumerable<User>(users);
        var dbSetMock = new Mock<DbSet<User>>();
        dbSetMock.As<IQueryable<User>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<User>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<User>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.Users).Returns(dbSetMock.Object);
    }

    private void SetupLearningPathsDbSet(List<LearningPath> learningPaths)
    {
        var queryable = new TestAsyncEnumerable<LearningPath>(learningPaths);
        var dbSetMock = new Mock<DbSet<LearningPath>>();
        dbSetMock.As<IQueryable<LearningPath>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<LearningPath>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<LearningPath>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<LearningPath>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<LearningPath>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.LearningPaths).Returns(dbSetMock.Object);
    }
}
