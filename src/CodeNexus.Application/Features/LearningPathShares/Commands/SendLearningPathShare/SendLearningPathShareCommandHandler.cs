using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
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
    private readonly ILearningPathShareRealtimeNotifier _learningPathShareRealtimeNotifier;

    public SendLearningPathShareCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILearningPathShareRealtimeNotifier learningPathShareRealtimeNotifier)
    {
        _context = context;
        _currentUserService = currentUserService;
        _learningPathShareRealtimeNotifier = learningPathShareRealtimeNotifier;
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
            return Result<LearningPathShareDto>.Failure("ACCESS_DENIED", "Access denied.");
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
            return Result<LearningPathShareDto>.Failure("ACCESS_DENIED", "Access denied.");
        }

        if (string.Equals(path.Status, LearningPathStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.Status, LearningPathStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathShareDto>.Failure("INVALID_STATUS", "Invalid status for this operation.");
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

        var existingAcceptedShare = await _context.LearningPathShares
            .AsNoTracking()
            .AnyAsync(s => s.PathId == request.PathId
                        && s.MentorId == mentorId
                        && s.StudentId == request.StudentId
                        && s.Status == LearningPathShareStatus.Accepted,
                cancellationToken);

        if (existingAcceptedShare)
        {
            return Result<LearningPathShareDto>.Failure("SHARE_ALREADY_ACCEPTED", "This learning path has already been accepted by the student.");
        }

        var chapterIds = await _context.Chapters
            .AsNoTracking()
            .Where(c => c.PathId == request.PathId && !c.IsDeleted)
            .Select(c => c.ChapterId)
            .ToListAsync(cancellationToken);

        var chapterIdsWithTask = await _context.Tasks
            .AsNoTracking()
            .Where(t => chapterIds.Contains(t.ChapterId))
            .Select(t => t.ChapterId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (chapterIds.Except(chapterIdsWithTask).Any())
        {
            return Result<LearningPathShareDto>.Failure(
                "CHAPTER_TASK_REQUIRED",
                "Each chapter must have at least one task before sharing.");
        }

        var lessonIds = await _context.Lessons
            .AsNoTracking()
            .Where(l => !l.IsDeleted && chapterIds.Contains(l.ChapterId))
            .Select(l => l.LessonId)
            .ToListAsync(cancellationToken);

        var lessonIdsWithQuiz = await _context.Quizzes
            .AsNoTracking()
            .Where(q => q.LessonId.HasValue && !q.IsDeleted && lessonIds.Contains(q.LessonId.Value))
            .Select(q => q.LessonId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (lessonIds.Except(lessonIdsWithQuiz).Any())
        {
            return Result<LearningPathShareDto>.Failure(
                "LESSON_QUIZ_REQUIRED",
                "Each lesson must have at least one quiz before sharing.");
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(7), DateTimeKind.Unspecified);

        // Quota check: student must have an active subscription with remaining SharesFromMentor
        var subscription = await _context.StudentMentorSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == request.StudentId && s.IsActive, cancellationToken);

        if (subscription == null)
            return Result<LearningPathShareDto>.Failure("MENTOR_SUBSCRIPTION_REQUIRED", "The student does not have an active mentor subscription.");

        if (subscription.SharesFromMentorLimit != -1 && subscription.SharesFromMentorUsed >= subscription.SharesFromMentorLimit)
            return Result<LearningPathShareDto>.Failure("SHARE_QUOTA_EXCEEDED", "The student has reached their share reception limit for this subscription.");

        var share = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = request.PathId,
            MentorId = mentorId,
            StudentId = request.StudentId,
            SnapshotTitle = path.Title,
            Status = LearningPathShareStatus.Pending,
            SentAt = now
        };

        var conversation = await _context.DirectConversations
            .FirstOrDefaultAsync(c => c.MentorId == mentorId && c.StudentId == request.StudentId, cancellationToken);

        if (conversation == null)
        {
            conversation = new DirectConversation
            {
                ConversationId = NewId.NextGuid(),
                MentorId = mentorId,
                StudentId = request.StudentId,
                CreatedAt = now
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
            SentAt = now
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

        // Decrement student's share quota
        subscription.SharesFromMentorUsed++;
        _context.FeatureUsageLogs.Add(new Domain.Entities.FeatureUsageLog
        {
            FeatureUsageLogId = NewId.NextGuid(),
            UserId = request.StudentId,
            FeatureKey = Domain.Enums.SubscriptionFeatureKey.SharesFromMentor,
            CreatedAt = DateTime.UtcNow
        });


        // Decrement student's share quota
        subscription.SharesFromMentorUsed++;
        _context.FeatureUsageLogs.Add(new Domain.Entities.FeatureUsageLog
        {
            FeatureUsageLogId = NewId.NextGuid(),
            UserId = request.StudentId,
            FeatureKey = Domain.Enums.SubscriptionFeatureKey.SharesFromMentor,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        var directMessageDto = new DirectMessageDto(
            message.MessageId,
            message.ConversationId,
            message.SenderId,
            message.Content,
            message.MessageType,
            message.SentAt,
            null,
            null,
            message.LearningPathShareId,
            message.ReplyToMessageId,
            null,
            null);

        await _learningPathShareRealtimeNotifier.NotifyShareSentAsync(
            request.StudentId,
            conversation.ConversationId,
            conversation.LastMessagePreview,
            conversation.LastMessageAt,
            directMessageDto,
            cancellationToken);

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
