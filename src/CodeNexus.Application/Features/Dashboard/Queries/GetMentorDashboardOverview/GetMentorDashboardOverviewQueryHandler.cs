using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Dashboard.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Dashboard.Queries.GetMentorDashboardOverview;

public class GetMentorDashboardOverviewQueryHandler
    : IRequestHandler<GetMentorDashboardOverviewQuery, Result<MentorDashboardOverviewResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMentorDashboardOverviewQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<MentorDashboardOverviewResponse>> Handle(
        GetMentorDashboardOverviewQuery request,
        CancellationToken cancellationToken)
    {
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<MentorDashboardOverviewResponse>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var mentor = await _context.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserId == mentorId, cancellationToken);

        if (mentor == null)
        {
            return Result<MentorDashboardOverviewResponse>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<MentorDashboardOverviewResponse>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var supportedStudentsCount = await _context.DirectConversations
            .AsNoTracking()
            .Where(c => c.ConversationType == ChatConversationType.Direct
                && c.MentorId == mentorId
                && c.StudentId.HasValue
                && c.Messages.Any())
            .Select(c => c.StudentId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);

        var createdSubjectsCount = await _context.Subjects
            .AsNoTracking()
            .CountAsync(s => s.CreatedByUserId == mentorId && !s.IsDeleted, cancellationToken);

        var draftLearningPathsCount = await _context.LearningPaths
            .AsNoTracking()
            .CountAsync(lp => lp.UserId == mentorId && lp.Status == LearningPathStatus.Draft.ToString(), cancellationToken);

        var recentIncomingMessages = await _context.DirectMessages
            .AsNoTracking()
            .Where(m => m.Conversation.ConversationType == ChatConversationType.Direct
                && m.Conversation.MentorId == mentorId
                && m.Conversation.StudentId.HasValue
                && m.SenderId == m.Conversation.StudentId)
            .OrderByDescending(m => m.SentAt)
            .Select(m => new
            {
                m.MessageId,
                m.ConversationId,
                StudentId = m.Conversation.StudentId!.Value,
                StudentName = m.Conversation.Student!.Username,
                m.Content,
                m.SentAt
            })
            .Take(200)
            .ToListAsync(cancellationToken);

        var recentStudentMessages = recentIncomingMessages
            .GroupBy(x => x.StudentId)
            .Select(g => g.OrderByDescending(x => x.SentAt).First())
            .OrderByDescending(x => x.SentAt)
            .Take(5)
            .Select(x => new MentorRecentStudentMessageDto(
                x.MessageId,
                x.ConversationId,
                x.StudentId,
                x.StudentName,
                x.Content,
                x.SentAt))
            .ToList();

        var response = new MentorDashboardOverviewResponse(
            SupportedStudentsCount: supportedStudentsCount,
            CreatedSubjectsCount: createdSubjectsCount,
            DraftLearningPathsCount: draftLearningPathsCount,
            RecentStudentMessages: recentStudentMessages);

        return Result<MentorDashboardOverviewResponse>.Success(response);
    }
}
