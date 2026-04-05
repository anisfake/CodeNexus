using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationSummaries;

public class GetTutorConversationSummariesQueryHandler
    : IRequestHandler<GetTutorConversationSummariesQuery, Result<PaginationDto<TutorConversationSummaryDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetTutorConversationSummariesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginationDto<TutorConversationSummaryDto>>> Handle(
        GetTutorConversationSummariesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == request.ConversationId && !c.IsDeleted, cancellationToken);

        if (conversation == null)
        {
            return Result<PaginationDto<TutorConversationSummaryDto>>.Failure("CONVERSATION_NOT_FOUND", "Conversation not found.");
        }

        if (conversation.UserId != userId)
        {
            return Result<PaginationDto<TutorConversationSummaryDto>>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var totalCount = await _context.ConversationSummaries
            .AsNoTracking()
            .Where(s => s.ConversationId == request.ConversationId)
            .CountAsync(cancellationToken);

        var summaries = await _context.ConversationSummaries
            .AsNoTracking()
            .Where(s => s.ConversationId == request.ConversationId)
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new TutorConversationSummaryDto(
                s.SummaryId,
                s.ConversationId,
                s.SummaryContent,
                s.MessageCount,
                s.StartMessageCreatedAt,
                s.EndMessageCreatedAt,
                s.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Result<PaginationDto<TutorConversationSummaryDto>>.Success(new PaginationDto<TutorConversationSummaryDto>
        {
            Items = summaries,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        });
    }
}
