using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Mentors.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Mentors.Commands.UpsertMentorReview;

public class UpsertMentorReviewCommandHandler : IRequestHandler<UpsertMentorReviewCommand, Result<UpsertMentorReviewResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpsertMentorReviewCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<UpsertMentorReviewResponseDto>> Handle(UpsertMentorReviewCommand request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<UpsertMentorReviewResponseDto>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        if (studentId == request.MentorId)
        {
            return Result<UpsertMentorReviewResponseDto>.Failure("SELF_REVIEW_NOT_ALLOWED", "You cannot review yourself.");
        }

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == studentId, cancellationToken);

        if (student == null)
        {
            return Result<UpsertMentorReviewResponseDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(student.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<UpsertMentorReviewResponseDto>.Failure("ACCESS_DENIED", "Only students can review mentors.");
        }

        var mentorExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserId == request.MentorId && u.Role != null && u.Role.RoleName == "Mentor", cancellationToken);

        if (!mentorExists)
        {
            return Result<UpsertMentorReviewResponseDto>.Failure("MENTOR_NOT_FOUND", "Mentor not found.");
        }

        var hasInteraction = await _context.DirectConversations
            .AsNoTracking()
            .AnyAsync(c => c.MentorId == request.MentorId && c.StudentId == studentId, cancellationToken)
            || await _context.LearningPathShares
                .AsNoTracking()
                .AnyAsync(s => s.MentorId == request.MentorId && s.StudentId == studentId, cancellationToken);

        if (!hasInteraction)
        {
            return Result<UpsertMentorReviewResponseDto>.Failure(
                "MENTOR_INTERACTION_REQUIRED",
                "You can review this mentor after at least one interaction.");
        }

        var normalizedComment = NormalizeComment(request.Comment);

        var existingReview = await _context.MentorRatings
            .FirstOrDefaultAsync(r => r.MentorId == request.MentorId && r.StudentId == studentId, cancellationToken);

        if (existingReview == null)
        {
            existingReview = new MentorRating
            {
                MentorId = request.MentorId,
                StudentId = studentId,
                Score = request.Score,
                Comment = normalizedComment,
                CreatedAt = DateTime.UtcNow
            };

            await _context.MentorRatings.AddAsync(existingReview, cancellationToken);
        }
        else
        {
            existingReview.Score = request.Score;
            existingReview.Comment = normalizedComment;
            existingReview.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var stats = await _context.MentorRatings
            .AsNoTracking()
            .Where(r => r.MentorId == request.MentorId)
            .GroupBy(r => r.MentorId)
            .Select(g => new
            {
                AverageRating = g.Average(x => (double)x.Score),
                TotalReviews = g.Count()
            })
            .FirstAsync(cancellationToken);

        var response = new UpsertMentorReviewResponseDto(
            request.MentorId,
            studentId,
            existingReview.Score,
            existingReview.Comment,
            existingReview.CreatedAt,
            existingReview.UpdatedAt,
            Math.Round(stats.AverageRating, 2),
            stats.TotalReviews);

        return Result<UpsertMentorReviewResponseDto>.Success(response);
    }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        return comment.Trim();
    }
}
