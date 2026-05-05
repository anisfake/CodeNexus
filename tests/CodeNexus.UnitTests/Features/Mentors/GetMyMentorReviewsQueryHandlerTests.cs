using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Mentors.Queries.GetMyMentorReviews;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.Mentors;

public class GetMyMentorReviewsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMyMentorReviewsQueryHandler _handler;

    public GetMyMentorReviewsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetMyMentorReviewsQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsMentor_ShouldReturnOwnReviews()
    {
        var mentorId = NewId.NextGuid();
        var studentAId = NewId.NextGuid();
        var studentBId = NewId.NextGuid();

        var users = new List<User>
        {
            new()
            {
                UserId = mentorId,
                Username = "mentor-a",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
            },
            new()
            {
                UserId = studentAId,
                Username = "student-a",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" },
                UserProfile = new UserProfile { ProfileId = NewId.NextGuid(), UserId = studentAId, AvatarUrl = "a.png" }
            },
            new()
            {
                UserId = studentBId,
                Username = "student-b",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" },
                UserProfile = new UserProfile { ProfileId = NewId.NextGuid(), UserId = studentBId, AvatarUrl = "b.png" }
            }
        };

        var ratings = new List<MentorRating>
        {
            new()
            {
                RatingId = NewId.NextGuid(),
                MentorId = mentorId,
                StudentId = studentAId,
                Score = 4,
                Comment = "Good",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                RatingId = NewId.NextGuid(),
                MentorId = mentorId,
                StudentId = studentBId,
                Score = 5,
                Comment = "Great",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.MentorRatings).Returns(ratings.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMyMentorReviewsQuery(1, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TotalReviews.Should().Be(2);
        result.Value.AverageRating.Should().Be(4.5);
        result.Value.Reviews.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotMentor_ShouldReturnAccessDenied()
    {
        var studentId = NewId.NextGuid();
        var users = new List<User>
        {
            new()
            {
                UserId = studentId,
                Username = "student-a",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.MentorRatings).Returns(new List<MentorRating>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMyMentorReviewsQuery(1, 10), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }
}
