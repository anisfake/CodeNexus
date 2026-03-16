using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Commands.RejectLearningPathShare;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class RejectLearningPathShareCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly RejectLearningPathShareCommandHandler _handler;

    public RejectLearningPathShareCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new RejectLearningPathShareCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_PendingShare_UpdatesToRejected()
    {
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var shareId = NewId.NextGuid();

        var student = new User
        {
            UserId = studentId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var share = new LearningPathShare
        {
            ShareId = shareId,
            PathId = NewId.NextGuid(),
            MentorId = mentorId,
            StudentId = studentId,
            Status = LearningPathShareStatus.Pending,
            SentAt = DateTime.UtcNow
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { share }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new RejectLearningPathShareCommand(shareId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(LearningPathShareStatus.Rejected);
        share.Status.Should().Be(LearningPathShareStatus.Rejected);
        share.RespondedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_NonStudentRole_ReturnsAccessDenied()
    {
        var userId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = userId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new RejectLearningPathShareCommand(NewId.NextGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }
}
