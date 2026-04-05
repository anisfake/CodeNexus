using CodeNexus.Application.Common.Events;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Notifications;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class LearningPathDraftVersionUpdatedInvalidatePendingSharesHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly LearningPathDraftVersionUpdatedInvalidatePendingSharesHandler _handler;

    public LearningPathDraftVersionUpdatedInvalidatePendingSharesHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _handler = new LearningPathDraftVersionUpdatedInvalidatePendingSharesHandler(_mockContext.Object);
    }

    [Fact]
    public async Task Handle_PendingSharesExist_InvalidatesPendingShares()
    {
        var pathId = NewId.NextGuid();
        var occurredAt = new DateTime(2026, 04, 05, 10, 0, 0, DateTimeKind.Utc);

        var pendingA = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            MentorId = NewId.NextGuid(),
            StudentId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Pending,
            SentAt = occurredAt.AddHours(-2)
        };

        var pendingB = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            MentorId = NewId.NextGuid(),
            StudentId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Pending,
            SentAt = occurredAt.AddHours(-1)
        };

        var accepted = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            MentorId = NewId.NextGuid(),
            StudentId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Accepted,
            SentAt = occurredAt.AddHours(-3)
        };

        _mockContext
            .Setup(x => x.LearningPathShares)
            .Returns(new[] { pendingA, pendingB, accepted }.BuildMockDbSet().Object);

        _mockContext
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var notification = new LearningPathDraftVersionUpdatedEvent(
            pathId,
            NewId.NextGuid(),
            "mentor_a",
            2,
            occurredAt);

        await _handler.Handle(notification, CancellationToken.None);

        pendingA.Status.Should().Be(LearningPathShareStatus.Rejected);
        pendingB.Status.Should().Be(LearningPathShareStatus.Rejected);
        pendingA.RespondedAt.Should().Be(occurredAt);
        pendingB.RespondedAt.Should().Be(occurredAt);
        pendingA.InvalidatedReason.Should().Be("SUPERSEDED_BY_NEW_VERSION");
        pendingB.InvalidatedReason.Should().Be("SUPERSEDED_BY_NEW_VERSION");
        accepted.Status.Should().Be(LearningPathShareStatus.Accepted);

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoPendingShares_DoesNotPersist()
    {
        var pathId = NewId.NextGuid();
        var occurredAt = new DateTime(2026, 04, 05, 11, 0, 0, DateTimeKind.Utc);

        var accepted = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            MentorId = NewId.NextGuid(),
            StudentId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Accepted,
            SentAt = occurredAt.AddHours(-2)
        };

        _mockContext
            .Setup(x => x.LearningPathShares)
            .Returns(new[] { accepted }.BuildMockDbSet().Object);

        var notification = new LearningPathDraftVersionUpdatedEvent(
            pathId,
            NewId.NextGuid(),
            "mentor_a",
            3,
            occurredAt);

        await _handler.Handle(notification, CancellationToken.None);

        accepted.Status.Should().Be(LearningPathShareStatus.Accepted);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
