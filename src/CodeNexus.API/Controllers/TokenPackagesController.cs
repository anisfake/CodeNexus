using CodeNexus.API.Models.Responses;
using CodeNexus.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/token-packages")]
[Authorize]
public class TokenPackagesController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public TokenPackagesController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var packages = await _context.TokenPackages
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new TokenPackageResponse(
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

        return Ok(packages);
    }
}

