using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetAllLearningPaths;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using GoalEntity = CodeNexus.Domain.Entities.Goals;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GetAllLearningPathQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetAllLearningPathQueryHandler _handler;
    private readonly Guid _mentorUserId = Guid.NewGuid();
    private readonly Guid _studentUserId = Guid.NewGuid();

    public GetAllLearningPathQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetAllLearningPathQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_MentorUser_ReturnsAllLearningPaths()
    {
        // Arrange
        var mentorUser = CreateMentorUser();
        var learningPaths = GetTestLearningPaths();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_mentorUserId);
        SetupUsersDbSet(new List<User> { mentorUser });
        SetupLearningPathsDbSet(learningPaths);

        var query = new GetAllLearningPathQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NonMentorUser_ReturnsAccessDenied()
    {
        // Arrange
        var studentUser = CreateStudentUser();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_studentUserId);
        SetupUsersDbSet(new List<User> { studentUser });

        var query = new GetAllLearningPathQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // Arrange
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
        SetupUsersDbSet(new List<User>());

        var query = new GetAllLearningPathQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WithSearchTerm_ReturnsFilteredResults()
    {
        // Arrange
        var mentorUser = CreateMentorUser();
        var learningPaths = GetTestLearningPaths();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_mentorUserId);
        SetupUsersDbSet(new List<User> { mentorUser });
        SetupLearningPathsDbSet(learningPaths);

        var query = new GetAllLearningPathQuery(SearchTerm: "calculus");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.First().Title.Should().Contain("Calculus");
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsFilteredResults()
    {
        // Arrange
        var mentorUser = CreateMentorUser();
        var learningPaths = GetTestLearningPaths();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_mentorUserId);
        SetupUsersDbSet(new List<User> { mentorUser });
        SetupLearningPathsDbSet(learningPaths);

        var query = new GetAllLearningPathQuery(Status: LearningPathStatus.Active);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.First().Status.Should().Be(LearningPathStatus.Active.ToString());
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var mentorUser = CreateMentorUser();
        var learningPaths = GetTestLearningPaths();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(_mentorUserId);
        SetupUsersDbSet(new List<User> { mentorUser });
        SetupLearningPathsDbSet(learningPaths);

        var query = new GetAllLearningPathQuery(PageNumber: 1, PageSize: 1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(1);
        result.Value.TotalCount.Should().Be(2);
        result.Value.TotalPages.Should().Be(2);
    }

    private User CreateMentorUser()
    {
        var mentorRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Mentor" };
        return new User
        {
            UserId = _mentorUserId,
            Username = "mentor1",
            Email = "mentor@test.com",
            PasswordHash = "hash",
            RoleId = mentorRole.RoleId,
            Role = mentorRole
        };
    }

    private User CreateStudentUser()
    {
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        return new User
        {
            UserId = _studentUserId,
            Username = "student1",
            Email = "student@test.com",
            PasswordHash = "hash",
            RoleId = studentRole.RoleId,
            Role = studentRole
        };
    }

    private List<LearningPath> GetTestLearningPaths()
    {
        var subjectId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var studentUser = CreateStudentUser();

        var subject = new Subject
        {
            SubjectId = subjectId,
            Name = "Mathematics",
            Description = "Math subject",
            CreatedByUserId = _mentorUserId
        };

        var goal = new GoalEntity
        {
            GoalId = goalId,
            Title = "Learn Calculus",
            Description = "Master calculus",
            UserId = _studentUserId
        };

        return new List<LearningPath>
        {
            new LearningPath
            {
                PathId = Guid.NewGuid(),
                UserId = _studentUserId,
                SubjectId = subjectId,
                GoalId = goalId,
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
                SubjectId = subjectId,
                GoalId = goalId,
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
