using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Mentors.Queries.GetMentorProfile;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.Mentors;

public class GetMentorProfileQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMentorProfileQueryHandler _handler;

    public GetMentorProfileQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetMentorProfileQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WhenMentorExists_ShouldReturnMentorProfileWithMyReview()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var subjectAId = NewId.NextGuid();
        var subjectBId = NewId.NextGuid();

        var users = new List<User>
        {
            new()
            {
                UserId = mentorId,
                Username = "mentor-1",
                FirstName = "Mentor",
                LastName = "One",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" },
                UserProfile = new UserProfile
                {
                    ProfileId = NewId.NextGuid(),
                    UserId = mentorId,
                    Bio = "I mentor backend and cloud"
                }
            },
            new()
            {
                UserId = studentId,
                Username = "student-1",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" },
                UserProfile = new UserProfile
                {
                    ProfileId = NewId.NextGuid(),
                    UserId = studentId,
                    AvatarUrl = "student-avatar.png"
                }
            }
        };

        var ratings = new List<MentorRating>
        {
            new()
            {
                RatingId = NewId.NextGuid(),
                MentorId = mentorId,
                StudentId = studentId,
                Score = 5,
                Comment = "Great mentor"
            }
        };

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = subjectAId,
                CreatedByUserId = mentorId,
                Name = "C#",
                Category = SubjectCategory.ProgrammingLanguage
            },
            new()
            {
                SubjectId = subjectBId,
                CreatedByUserId = studentId,
                Name = "Azure",
                Category = SubjectCategory.Cloud
            }
        };

        var learningPaths = new List<LearningPath>
        {
            new()
            {
                PathId = NewId.NextGuid(),
                UserId = mentorId,
                SubjectId = subjectBId,
                Title = "Cloud mentoring path"
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.MentorRatings).Returns(ratings.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(subjects.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(learningPaths.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMentorProfileQuery(mentorId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.MentorId.Should().Be(mentorId);
        result.Value.AverageRating.Should().Be(5.0d);
        result.Value.TotalReviews.Should().Be(1);
        result.Value.MyReview.Should().NotBeNull();
        result.Value.MyReview!.Score.Should().Be(5);
        result.Value.Specializations.Should().Contain(new[] { "ProgrammingLanguage", "Cloud" });
        result.Value.SpecializedSubjects.Should().Contain(new[] { "C#", "Azure" });
    }
}
