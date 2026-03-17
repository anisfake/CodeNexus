using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
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

        var conversations = await _context.DirectConversations
            .AsNoTracking()
            .Include(c => c.Mentor)
            .Include(c => c.Student)
            .Where(c => c.MentorId == currentUserId || c.StudentId == currentUserId)
            .Select(c => new DirectConversationDto(
                c.ConversationId,
                c.MentorId,
                c.Mentor.Username,
                c.StudentId,
                c.Student.Username,
                c.LastMessagePreview,
                c.LastMessageAt,
                _context.DirectMessageReceipts.Count(r =>
                    r.UserId == currentUserId &&
                    !r.SeenAt.HasValue &&
                    r.Message.ConversationId == c.ConversationId)
            ))
            .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
            .ThenByDescending(c => c.ConversationId)
            .ToListAsync(cancellationToken);

        return Result<List<DirectConversationDto>>.Success(conversations);
    }
}
