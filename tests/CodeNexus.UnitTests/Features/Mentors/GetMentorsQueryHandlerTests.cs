using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Mentors.Queries.GetMentors;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.Mentors;

public class GetMentorsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMentorsQueryHandler _handler;

    public GetMentorsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetMentorsQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnMentorCardsWithSpecializationsAndRatings()
    {
        var currentUserId = NewId.NextGuid();
        var mentorAId = NewId.NextGuid();
        var mentorBId = NewId.NextGuid();
        var subjectAId = NewId.NextGuid();
        var subjectBId = NewId.NextGuid();

        var users = new List<User>
        {
            new()
            {
                UserId = currentUserId,
                Username = "student",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
            },
            new()
            {
                UserId = mentorAId,
                Username = "mentor-a",
                FirstName = "A",
                LastName = "Mentor",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" },
                UserProfile = new UserProfile { ProfileId = NewId.NextGuid(), UserId = mentorAId, Bio = "Backend mentor" }
            },
            new()
            {
                UserId = mentorBId,
                Username = "mentor-b",
                FirstName = "B",
                LastName = "Mentor",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" },
                UserProfile = new UserProfile { ProfileId = NewId.NextGuid(), UserId = mentorBId, Bio = "Cloud mentor" }
            }
        };

        var ratings = new List<MentorRating>
        {
            new() { RatingId = NewId.NextGuid(), MentorId = mentorAId, StudentId = currentUserId, Score = 4 },
            new() { RatingId = NewId.NextGuid(), MentorId = mentorAId, StudentId = NewId.NextGuid(), Score = 5 }
        };

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = subjectAId,
                CreatedByUserId = mentorAId,
                Name = "ASP.NET Core",
                Category = SubjectCategory.Backend
            },
            new()
            {
                SubjectId = subjectBId,
                CreatedByUserId = currentUserId,
                Name = "Azure",
                Category = SubjectCategory.Cloud
            }
        };

        var learningPaths = new List<LearningPath>
        {
            new()
            {
                PathId = NewId.NextGuid(),
                UserId = mentorBId,
                SubjectId = subjectBId,
                Title = "Cloud path"
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(currentUserId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.MentorRatings).Returns(ratings.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(subjects.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(learningPaths.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMentorsQuery(1, 10, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);

        var mentorA = result.Value.Items.First(x => x.MentorId == mentorAId);
        mentorA.AverageRating.Should().Be(4.5);
        mentorA.TotalReviews.Should().Be(2);
        mentorA.Specializations.Should().Contain("Backend");

        var mentorB = result.Value.Items.First(x => x.MentorId == mentorBId);
        mentorB.Specializations.Should().Contain("Cloud");
    }

    [Fact]
    public async Task Handle_WithSubjectCategoryAndSubjectNameFilter_ShouldReturnMatchedMentorsOnly()
    {
        var currentUserId = NewId.NextGuid();
        var mentorAId = NewId.NextGuid();
        var mentorBId = NewId.NextGuid();
        var subjectAId = NewId.NextGuid();
        var subjectBId = NewId.NextGuid();

        var users = new List<User>
        {
            new()
            {
                UserId = currentUserId,
                Username = "student",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
            },
            new()
            {
                UserId = mentorAId,
                Username = "mentor-a",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
            },
            new()
            {
                UserId = mentorBId,
                Username = "mentor-b",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
            }
        };

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = subjectAId,
                CreatedByUserId = mentorAId,
                Name = "ASP.NET Core",
                Category = SubjectCategory.Backend
            },
            new()
            {
                SubjectId = subjectBId,
                CreatedByUserId = mentorBId,
                Name = "React",
                Category = SubjectCategory.Frontend
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(currentUserId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.MentorRatings).Returns(new List<MentorRating>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(subjects.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);

        var result = await _handler.Handle(
            new GetMentorsQuery(1, 10, null, SubjectCategory.Backend, "asp"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].MentorId.Should().Be(mentorAId);
    }
}
