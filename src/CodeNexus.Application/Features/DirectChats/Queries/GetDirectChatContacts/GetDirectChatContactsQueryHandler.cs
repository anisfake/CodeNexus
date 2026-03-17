using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DirectChats.Queries.GetDirectChatContacts;

public class GetDirectChatContactsQueryHandler : IRequestHandler<GetDirectChatContactsQuery, Result<List<DirectChatContactDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetDirectChatContactsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<DirectChatContactDto>>> Handle(GetDirectChatContactsQuery request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<List<DirectChatContactDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var currentUser = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == currentUserId, cancellationToken);

        if (currentUser == null)
        {
            return Result<List<DirectChatContactDto>>.Failure("USER_NOT_FOUND", "User not found.");
        }

        var roleName = currentUser.Role?.RoleName;

        if (string.Equals(roleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            var mentorRows = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.UserProfile)
                .Where(u => u.Role != null && u.Role.RoleName == "Mentor")
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    AvatarUrl = u.UserProfile != null ? u.UserProfile.AvatarUrl : null
                })
                .ToListAsync(cancellationToken);

            var conversationByMentorId = await _context.DirectConversations
                .AsNoTracking()
                .Where(c => c.StudentId == currentUserId)
                .Select(c => new
                {
                    c.MentorId,
                    c.ConversationId,
                    c.LastMessageAt
                })
                .ToDictionaryAsync(x => x.MentorId, x => new { x.ConversationId, x.LastMessageAt }, cancellationToken);

            var mentors = mentorRows
                .Select(u =>
                {
                    var hasConversation = conversationByMentorId.TryGetValue(u.UserId, out var conversationData);

                    return new DirectChatContactDto(
                        u.UserId,
                        u.Username,
                        u.AvatarUrl,
                        "Mentor",
                        hasConversation ? conversationData!.ConversationId : null,
                        hasConversation ? conversationData!.LastMessageAt : null
                    );
                })
                .OrderByDescending(x => x.LastMessageAt ?? DateTime.MinValue)
                .ThenBy(x => x.Username)
                .ToList();

            return Result<List<DirectChatContactDto>>.Success(mentors);
        }

        if (string.Equals(roleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            var students = await _context.DirectConversations
                .AsNoTracking()
                .Include(c => c.Student)
                .ThenInclude(s => s.UserProfile)
                .Where(c => c.MentorId == currentUserId &&
                            _context.DirectMessages.Any(m => m.ConversationId == c.ConversationId && m.SenderId == c.StudentId))
                .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
                .Select(c => new DirectChatContactDto(
                    c.StudentId,
                    c.Student.Username,
                    c.Student.UserProfile != null ? c.Student.UserProfile.AvatarUrl : null,
                    "Student",
                    c.ConversationId,
                    c.LastMessageAt
                ))
                .ToListAsync(cancellationToken);

            return Result<List<DirectChatContactDto>>.Success(students);
        }

        return Result<List<DirectChatContactDto>>.Failure("ACCESS_DENIED", "Only mentors and students can access direct chat contacts.");
    }
}
