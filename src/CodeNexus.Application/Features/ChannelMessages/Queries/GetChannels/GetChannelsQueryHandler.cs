using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.ChannelMessages.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

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
        try
        {
            _ = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<List<ChannelDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        await Task.CompletedTask;

        var channels = Enum
            .GetValues<SubjectCategory>()
            .Select(category => new ChannelDto(category, category.ToString()))
            .ToList();

        return Result<List<ChannelDto>>.Success(channels);
    }
}
