using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using CodeNexus.Application.Features.LearningPathShares.Services;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.RequestLearningPathMentorReview;

public class RequestLearningPathMentorReviewCommandHandler
    : IRequestHandler<RequestLearningPathMentorReviewCommand, Result<RequestLearningPathMentorReviewResponseDto>>
{
    private const int DefaultMaxRejections = 3;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILearningPathSharePathSyncService _pathSyncService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RequestLearningPathMentorReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILearningPathSharePathSyncService pathSyncService,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pathSyncService = pathSyncService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<RequestLearningPathMentorReviewResponseDto>> Handle(
        RequestLearningPathMentorReviewCommand request,
        CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<RequestLearningPathMentorReviewResponseDto>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == studentId, cancellationToken);

        if (student == null)
        {
            return Result<RequestLearningPathMentorReviewResponseDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(student.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<RequestLearningPathMentorReviewResponseDto>.Failure("ACCESS_DENIED", "Only students can request mentor review.");
        }

        var mentor = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.MentorId, cancellationToken);

        if (mentor == null)
        {
            return Result<RequestLearningPathMentorReviewResponseDto>.Failure("MENTOR_NOT_FOUND", "Mentor not found.");
        }

        if (!string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<RequestLearningPathMentorReviewResponseDto>.Failure("INVALID_RECIPIENT", "Recipient must be a mentor.");
        }

        if (request.MentorId == studentId)
        {
            return Result<RequestLearningPathMentorReviewResponseDto>.Failure("INVALID_RECIPIENT", "Student cannot request review from self.");
        }

        var sourcePath = await _context.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.LearningPathGoals)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                    .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

        if (sourcePath == null)
        {
            return Result<RequestLearningPathMentorReviewResponseDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (sourcePath.UserId != studentId)
        {
            return Result<RequestLearningPathMentorReviewResponseDto>.Failure("ACCESS_DENIED", "You can only request review for your own learning path.");
        }

        var existingReview = await _context.LearningPathMentorReviews
            .FirstOrDefaultAsync(r => r.PathId == request.PathId && r.MentorId == request.MentorId, cancellationToken);

        var maxRejections = request.MaxRejectCount ?? existingReview?.MaxRejections ?? DefaultMaxRejections;
        maxRejections = Math.Clamp(maxRejections, 1, 20);

        if (existingReview != null && existingReview.RejectionCount >= maxRejections)
        {
            return Result<RequestLearningPathMentorReviewResponseDto>.Failure(
                "MENTOR_REVIEW_REJECT_LIMIT_REACHED",
                $"You have reached the maximum reject attempts ({maxRejections}) for this mentor review.");
        }

        var now = _dateTimeProvider.UtcNow.AddHours(7);
        var revisedPathId = await EnsureMentorWorkspaceAsync(
            sourcePath,
            request.MentorId,
            existingReview?.RevisedPathId,
            now,
            cancellationToken);

        var normalizedRequestNote = string.IsNullOrWhiteSpace(request.StudentRequestNote)
            ? null
            : request.StudentRequestNote.Trim();

        if (existingReview == null)
        {
            existingReview = new LearningPathMentorReview
            {
                ReviewId = Guid.NewGuid(),
                PathId = request.PathId,
                RevisedPathId = revisedPathId,
                MentorId = request.MentorId,
                StudentId = studentId,
                Score = 0,
                Feedback = "Review requested by student.",
                Suggestions = null,
                ChangeSummary = null,
                ChangeReason = null,
                StudentRequestNote = normalizedRequestNote,
                DecisionStatus = LearningPathMentorReviewDecisionStatus.Pending,
                StudentDecisionNote = null,
                StudentDecidedAt = null,
                RejectionCount = 0,
                MaxRejections = maxRejections,
                CreatedAt = now
            };

            await _context.LearningPathMentorReviews.AddAsync(existingReview, cancellationToken);
        }
        else
        {
            existingReview.RevisedPathId = revisedPathId;
            existingReview.StudentRequestNote = normalizedRequestNote;
            existingReview.DecisionStatus = LearningPathMentorReviewDecisionStatus.Pending;
            existingReview.StudentDecisionNote = null;
            existingReview.StudentDecidedAt = null;
            existingReview.MaxRejections = maxRejections;
            existingReview.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<RequestLearningPathMentorReviewResponseDto>.Success(
            new RequestLearningPathMentorReviewResponseDto(
                existingReview.ReviewId,
                existingReview.PathId,
                existingReview.MentorId,
                existingReview.StudentId,
                existingReview.RevisedPathId,
                existingReview.StudentRequestNote,
                existingReview.DecisionStatus,
                existingReview.RejectionCount,
                existingReview.MaxRejections,
                existingReview.RejectionCount < existingReview.MaxRejections,
                existingReview.CreatedAt,
                existingReview.UpdatedAt));
    }

    private async Task<Guid> EnsureMentorWorkspaceAsync(
        LearningPath sourcePath,
        Guid mentorId,
        Guid? existingRevisedPathId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (existingRevisedPathId.HasValue)
        {
            var currentWorkspace = await _context.LearningPaths
                .Include(lp => lp.LearningPathGoals)
                .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                        .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
                .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
                .FirstOrDefaultAsync(
                    lp => lp.PathId == existingRevisedPathId.Value && lp.UserId == mentorId,
                    cancellationToken);

            if (currentWorkspace != null)
            {
                await _pathSyncService.RebuildCurrentPathFromSourceAsync(
                    currentWorkspace,
                    sourcePath,
                    mentorId,
                    now,
                    cancellationToken);

                currentWorkspace.Status = LearningPathStatus.Draft.ToString();
                return currentWorkspace.PathId;
            }
        }

        var workspacePathId = await _pathSyncService.ClonePathForStudentAsync(sourcePath, mentorId, now, cancellationToken);
        var workspacePath = _context.LearningPaths.Local?.FirstOrDefault(lp => lp.PathId == workspacePathId)
                            ?? await _context.LearningPaths.FirstOrDefaultAsync(lp => lp.PathId == workspacePathId, cancellationToken);
        if (workspacePath != null)
        {
            workspacePath.Status = LearningPathStatus.Draft.ToString();
        }

        return workspacePathId;
    }
}
