using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TokenPackages.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TokenPackages.Queries.GetAllTokenPackages;

public class GetAllTokenPackagesQueryHandler : IRequestHandler<GetAllTokenPackagesQuery, Result<List<TokenPackageDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllTokenPackagesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<TokenPackageDto>>> Handle(GetAllTokenPackagesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.TokenPackages.AsNoTracking().AsQueryable();
        if (request.ActiveOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var items = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new TokenPackageDto(
                x.TokenPackageId,
                x.Name,
                x.Description,
                x.PriceVnd,
                x.CreditedBalanceVnd,
                x.IsActive,
                x.DisplayOrder,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<TokenPackageDto>>.Success(items);
    }
}
