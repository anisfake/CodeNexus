using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationMessages;

public class GetTutorConversationMessagesQueryHandler
    : IRequestHandler<GetTutorConversationMessagesQuery, Result<PaginationDto<TutorMessageDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetTutorConversationMessagesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginationDto<TutorMessageDto>>> Handle(GetTutorConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == request.ConversationId && !c.IsDeleted, cancellationToken);

        if (conversation == null)
        {
            return Result<PaginationDto<TutorMessageDto>>.Failure("CONVERSATION_NOT_FOUND", "Conversation not found.");
        }

        if (conversation.UserId != userId)
        {
            return Result<PaginationDto<TutorMessageDto>>.Failure("ACCESS_DENIED", "You do not have access to this conversation.");
        }

        var totalCount = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.ConversationId)
            .CountAsync(cancellationToken);

        var messages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.ConversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = messages.Select(m =>
        {
            var role = ParseRole(m.Content);
            var content = StripRolePrefix(m.Content);
            return new TutorMessageDto(
                m.MessageId,
                m.ConversationId,
                role,
                content,
                m.CreatedAt
            );
        }).ToList();

        return Result<PaginationDto<TutorMessageDto>>.Success(new PaginationDto<TutorMessageDto>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        });
    }

    private static string ParseRole(string content)
    {
        if (content.StartsWith("USER:", StringComparison.OrdinalIgnoreCase))
            return "user";
        if (content.StartsWith("ASSISTANT:", StringComparison.OrdinalIgnoreCase))
            return "assistant";
        return "unknown";
    }

    private static string StripRolePrefix(string content)
    {
        if (content.StartsWith("USER:", StringComparison.OrdinalIgnoreCase))
            return content.Substring("USER:".Length).Trim();
        if (content.StartsWith("ASSISTANT:", StringComparison.OrdinalIgnoreCase))
            return content.Substring("ASSISTANT:".Length).Trim();
        return content.Trim();
    }
}
