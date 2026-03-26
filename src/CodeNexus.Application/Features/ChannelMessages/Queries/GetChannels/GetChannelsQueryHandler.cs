using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.ChannelMessages.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.ChannelMessages.Queries.GetChannels;

public class GetChannelsQueryHandler : IRequestHandler<GetChannelsQuery, Result<List<ChannelDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetChannelsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<ChannelDto>>> Handle(GetChannelsQuery request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<List<ChannelDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var subject = await _context.Subjects
            .AsNoTracking()
            .Where(s => s.SubjectId == request.SubjectId && !s.IsDeleted)
            .Select(s => new { s.SubjectId, s.CreatedByUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (subject == null)
        {
            return Result<List<ChannelDto>>.Failure("SUBJECT_NOT_FOUND", "Subject not found.");
        }

        var hasLearningPath = await _context.LearningPaths
            .AsNoTracking()
            .AnyAsync(lp => lp.SubjectId == request.SubjectId && lp.UserId == currentUserId, cancellationToken);

        if (subject.CreatedByUserId != currentUserId && !hasLearningPath)
        {
            return Result<List<ChannelDto>>.Failure("ACCESS_DENIED", "You do not have access to this subject.");
        }

        var channels = Enum
            .GetValues<SubjectCategory>()
            .Select(category => new ChannelDto(category, category.ToString()))
            .ToList();

        return Result<List<ChannelDto>>.Success(channels);
    }
}
