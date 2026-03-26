using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DirectChats.Queries.GetConversations;

public class GetConversationsQueryHandler : IRequestHandler<GetConversationsQuery, Result<List<DirectConversationDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetConversationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<DirectConversationDto>>> Handle(GetConversationsQuery request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<List<DirectConversationDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var conversationRows = await _context.DirectConversations
            .AsNoTracking()
            .Include(c => c.Mentor)
            .Include(c => c.Student)
            .Where(c => c.ConversationType == ChatConversationType.Direct && (c.MentorId == currentUserId || c.StudentId == currentUserId))
            .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
            .ThenByDescending(c => c.ConversationId)
            .Select(c => new
            {
                c.ConversationId,
                c.MentorId,
                MentorName = c.Mentor.Username,
                c.StudentId,
                StudentName = c.Student.Username,
                c.LastMessagePreview,
                c.LastMessageAt
            })
            .ToListAsync(cancellationToken);

        var unreadCounts = await _context.DirectMessageReceipts
            .AsNoTracking()
            .Where(r => r.UserId == currentUserId && !r.SeenAt.HasValue)
            .GroupBy(r => r.Message.ConversationId)
            .Select(g => new
            {
                ConversationId = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count, cancellationToken);

        var conversations = conversationRows
            .Select(c => new DirectConversationDto(
                c.ConversationId,
                c.MentorId!.Value,
                c.MentorName,
                c.StudentId!.Value,
                c.StudentName,
                c.LastMessagePreview,
                c.LastMessageAt,
                unreadCounts.TryGetValue(c.ConversationId, out var count) ? count : 0
            ))
            .ToList();

        return Result<List<DirectConversationDto>>.Success(conversations);
    }
}
