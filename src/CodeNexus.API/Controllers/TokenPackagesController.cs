using CodeNexus.API.Models.Responses;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TokenPackages.DTOs;
using CodeNexus.Application.Features.TokenPackages.Queries.GetAllTokenPackages;
using CodeNexus.Application.Features.TokenPackages.Queries.GetPublicTokenPricing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/token-packages")]
[Authorize]
public class TokenPackagesController : ControllerBase
{
    private readonly ISender _sender;

    public TokenPackagesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAllTokenPackagesQuery(ActiveOnly: true), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("pricing")]
    public async Task<IActionResult> GetPublicPricing(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPublicTokenPricingQuery(), cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    private IActionResult ToActionResult(Result<List<TokenPackageDto>> result)
    {
        if (result.IsSuccess)
        {
            return Ok((result.Value ?? new List<TokenPackageDto>()).Select(ToResponse).ToList());
        }

        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    private static TokenPackageResponse ToResponse(TokenPackageDto x)
        => new(x.TokenPackageId, x.Name, x.Description, x.PriceVnd, x.CreditedTokens, x.IsActive, x.DisplayOrder, x.CreatedAt, x.UpdatedAt);
}


