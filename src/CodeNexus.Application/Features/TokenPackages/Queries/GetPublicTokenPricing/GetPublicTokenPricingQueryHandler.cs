using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments;
using CodeNexus.Application.Features.TokenPackages.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.TokenPackages.Queries.GetPublicTokenPricing;

public class GetPublicTokenPricingQueryHandler
    : IRequestHandler<GetPublicTokenPricingQuery, Result<PublicTokenPricingDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPublicTokenPricingQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PublicTokenPricingDto>> Handle(GetPublicTokenPricingQuery request, CancellationToken cancellationToken)
    {
        var pricingPolicy = await TokenPricingPolicyResolver.ResolveAsync(_context, cancellationToken);

        var dto = new PublicTokenPricingDto(
            VndPerToken: pricingPolicy.VndPerToken,
            TokensPer1000Vnd: Math.Round(1000m / pricingPolicy.VndPerToken, 4, MidpointRounding.AwayFromZero),
            MinimumTopUpVnd: pricingPolicy.MinCustomTopUpVnd,
            MaximumTopUpVnd: pricingPolicy.MaxCustomTopUpVnd);

        return Result<PublicTokenPricingDto>.Success(dto);
    }
}
