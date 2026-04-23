using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorPackages.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.MentorPackages.Queries.GetAllMentorPackages;

public class GetAllMentorPackagesQueryHandler : IRequestHandler<GetAllMentorPackagesQuery, Result<List<MentorPackageDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllMentorPackagesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<MentorPackageDto>>> Handle(GetAllMentorPackagesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.MentorPackages.AsNoTracking().AsQueryable();

        if (request.ActiveOnly)
            query = query.Where(x => x.IsActive);

        var items = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new MentorPackageDto(
                x.MentorPackageId,
                x.Name,
                x.Description,
                x.PriceVnd,
                x.SharesFromMentorLimit,
                x.ValidationRequestLimit,
                x.TaskReviewLimit,
                x.IsActive,
                x.DisplayOrder,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<MentorPackageDto>>.Success(items);
    }
}
