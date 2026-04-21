using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Mentors.Commands.UpsertMentorReview;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.Mentors;

public class UpsertMentorReviewCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly UpsertMentorReviewCommandHandler _handler;

    public UpsertMentorReviewCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new UpsertMentorReviewCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WhenReviewExists_ShouldUpdateReviewAndReturnLatestStats()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();

        var users = new List<User>
        {
            new()
            {
                UserId = studentId,
                Username = "student-1",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
            },
            new()
            {
                UserId = mentorId,
                Username = "mentor-1",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
            }
        };

        var mentorRatings = new List<MentorRating>
        {
            new()
            {
                RatingId = NewId.NextGuid(),
                MentorId = mentorId,
                StudentId = studentId,
                Score = 3,
                Comment = "Old feedback",
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                RatingId = NewId.NextGuid(),
                MentorId = mentorId,
                StudentId = NewId.NextGuid(),
                Score = 5,
                Comment = "Great",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        var directConversations = new List<DirectConversation>
        {
            new()
            {
                ConversationId = NewId.NextGuid(),
                MentorId = mentorId,
                StudentId = studentId,
                ConversationType = ChatConversationType.Direct
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.MentorRatings).Returns(mentorRatings.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(directConversations.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(
            new UpsertMentorReviewCommand(mentorId, 4, "  Updated feedback  "),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Score.Should().Be(4);
        result.Value.Comment.Should().Be("Updated feedback");
        result.Value.TotalReviews.Should().Be(2);
        result.Value.AverageRating.Should().Be(4.5);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoInteraction_ShouldReturnFailure()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();

        var users = new List<User>
        {
            new()
            {
                UserId = studentId,
                Username = "student-1",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
            },
            new()
            {
                UserId = mentorId,
                Username = "mentor-1",
                Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(users.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.MentorRatings).Returns(new List<MentorRating>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(new List<DirectConversation>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare>().BuildMockDbSet().Object);

        var result = await _handler.Handle(
            new UpsertMentorReviewCommand(mentorId, 5, "Great mentor"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MENTOR_INTERACTION_REQUIRED");
    }
}
