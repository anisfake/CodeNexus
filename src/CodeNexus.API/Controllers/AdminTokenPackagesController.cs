using CodeNexus.API.Models.Requests;
using CodeNexus.API.Models.Responses;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Entities;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/admin/token-packages")]
[Authorize(Roles = "Admin")]
public class AdminTokenPackagesController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public AdminTokenPackagesController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var packages = await _context.TokenPackages
            .AsNoTracking()
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTokenPackageRequest request, CancellationToken cancellationToken)
    {
        if (!IsValidRequest(request.Name, request.PriceVnd, request.CreditedBalanceVnd))
        {
            return BadRequest(new { ErrorCode = "INVALID_TOKEN_PACKAGE", ErrorMessage = "Invalid token package payload." });
        }

        var tokenPackage = new TokenPackage
        {
            TokenPackageId = NewId.NextGuid(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PriceVnd = request.PriceVnd,
            CreditedBalanceVnd = request.CreditedBalanceVnd,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTime.UtcNow
        };

        await _context.TokenPackages.AddAsync(tokenPackage, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(tokenPackage));
    }

    [HttpPut("{tokenPackageId:guid}")]
    public async Task<IActionResult> Update(Guid tokenPackageId, [FromBody] UpdateTokenPackageRequest request, CancellationToken cancellationToken)
    {
        if (!IsValidRequest(request.Name, request.PriceVnd, request.CreditedBalanceVnd))
        {
            return BadRequest(new { ErrorCode = "INVALID_TOKEN_PACKAGE", ErrorMessage = "Invalid token package payload." });
        }

        var tokenPackage = await _context.TokenPackages.FirstOrDefaultAsync(x => x.TokenPackageId == tokenPackageId, cancellationToken);
        if (tokenPackage == null)
        {
            return NotFound(new { ErrorCode = "TOKEN_PACKAGE_NOT_FOUND", ErrorMessage = "Token package not found." });
        }

        tokenPackage.Name = request.Name.Trim();
        tokenPackage.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        tokenPackage.PriceVnd = request.PriceVnd;
        tokenPackage.CreditedBalanceVnd = request.CreditedBalanceVnd;
        tokenPackage.IsActive = request.IsActive;
        tokenPackage.DisplayOrder = request.DisplayOrder;
        tokenPackage.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(tokenPackage));
    }

    [HttpDelete("{tokenPackageId:guid}")]
    public async Task<IActionResult> Delete(Guid tokenPackageId, CancellationToken cancellationToken)
    {
        var tokenPackage = await _context.TokenPackages.FirstOrDefaultAsync(x => x.TokenPackageId == tokenPackageId, cancellationToken);
        if (tokenPackage == null)
        {
            return NotFound(new { ErrorCode = "TOKEN_PACKAGE_NOT_FOUND", ErrorMessage = "Token package not found." });
        }

        _context.TokenPackages.Remove(tokenPackage);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { Message = "Deleted" });
    }

    private static bool IsValidRequest(string? name, decimal priceVnd, decimal creditedBalanceVnd)
        => !string.IsNullOrWhiteSpace(name)
           && priceVnd > 0m
           && creditedBalanceVnd > 0m;

    private static TokenPackageResponse ToResponse(TokenPackage x)
        => new(
            x.TokenPackageId,
            x.Name,
            x.Description,
            x.PriceVnd,
            x.CreditedBalanceVnd,
            x.IsActive,
            x.DisplayOrder,
            x.CreatedAt,
            x.UpdatedAt);
}

