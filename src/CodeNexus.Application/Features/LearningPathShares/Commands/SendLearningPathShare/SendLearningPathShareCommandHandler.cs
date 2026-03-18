using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.SendLearningPathShare;

public class SendLearningPathShareCommandHandler : IRequestHandler<SendLearningPathShareCommand, Result<LearningPathShareDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SendLearningPathShareCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathShareDto>> Handle(SendLearningPathShareCommand request, CancellationToken cancellationToken)
    {
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LearningPathShareDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var mentor = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == mentorId, cancellationToken);

        if (mentor == null)
        {
            return Result<LearningPathShareDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathShareDto>.Failure("ACCESS_DENIED", "Only mentors can share learning paths.");
        }

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.StudentId, cancellationToken);

        if (student == null)
        {
            return Result<LearningPathShareDto>.Failure("STUDENT_NOT_FOUND", "Student not found.");
        }

        if (!string.Equals(student.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathShareDto>.Failure("INVALID_RECIPIENT", "Recipient must be a student.");
        }

        var path = await _context.LearningPaths
            .FirstOrDefaultAsync(p => p.PathId == request.PathId, cancellationToken);

        if (path == null)
        {
            return Result<LearningPathShareDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (path.UserId != mentorId)
        {
            return Result<LearningPathShareDto>.Failure("ACCESS_DENIED", "You can only share your own learning path.");
        }

        if (string.Equals(path.Status, LearningPathStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.Status, LearningPathStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathShareDto>.Failure("INVALID_STATUS", "Only active or draft learning paths can be shared.");
        }

        var existingPendingShare = await _context.LearningPathShares
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PathId == request.PathId
                                   && s.MentorId == mentorId
                                   && s.StudentId == request.StudentId
                                   && s.Status == LearningPathShareStatus.Pending,
                cancellationToken);

        if (existingPendingShare != null)
        {
            return Result<LearningPathShareDto>.Failure("SHARE_ALREADY_PENDING", "A pending share already exists for this student.");
        }

        var share = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = request.PathId,
            MentorId = mentorId,
            StudentId = request.StudentId,
            Status = LearningPathShareStatus.Pending,
            SentAt = DateTime.UtcNow
        };

        if (string.Equals(path.Status, LearningPathStatus.Draft.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            path.Status = LearningPathStatus.Active.ToString();
        }

        var conversation = await _context.DirectConversations
            .FirstOrDefaultAsync(c => c.MentorId == mentorId && c.StudentId == request.StudentId, cancellationToken);

        if (conversation == null)
        {
            conversation = new DirectConversation
            {
                ConversationId = NewId.NextGuid(),
                MentorId = mentorId,
                StudentId = request.StudentId,
                CreatedAt = DateTime.UtcNow
            };

            _context.DirectConversations.Add(conversation);
        }

        var message = new DirectMessage
        {
            MessageId = NewId.NextGuid(),
            ConversationId = conversation.ConversationId,
            SenderId = mentorId,
            Content = $"Shared learning path: {path.Title}",
            MessageType = DirectMessageType.LearningPathShare,
            LearningPathShareId = share.ShareId,
            SentAt = DateTime.UtcNow
        };

        var receipt = new DirectMessageReceipt
        {
            ReceiptId = NewId.NextGuid(),
            MessageId = message.MessageId,
            UserId = request.StudentId
        };

        conversation.LastMessagePreview = message.Content.Length > 120
            ? message.Content[..120]
            : message.Content;
        conversation.LastMessageAt = message.SentAt;

        _context.LearningPathShares.Add(share);
        _context.DirectMessages.Add(message);
        _context.DirectMessageReceipts.Add(receipt);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<LearningPathShareDto>.Success(new LearningPathShareDto(
            share.ShareId,
            share.PathId,
            share.MentorId,
            share.StudentId,
            share.Status,
            share.SentAt,
            share.RespondedAt
        ));
    }
}
