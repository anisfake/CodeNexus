using CodeNexus.API.Models.Requests;
using CodeNexus.API.Models.Responses;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TokenPackages.Commands.CreateTokenPackage;
using CodeNexus.Application.Features.TokenPackages.Commands.DeleteTokenPackage;
using CodeNexus.Application.Features.TokenPackages.Commands.UpdateTokenPackage;
using CodeNexus.Application.Features.TokenPackages.DTOs;
using CodeNexus.Application.Features.TokenPackages.Queries.GetAllTokenPackages;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/admin/token-packages")]
[Authorize(Roles = "Admin")]
public class AdminTokenPackagesController : ControllerBase
{
    private readonly ISender _sender;

    public AdminTokenPackagesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAllTokenPackagesQuery(ActiveOnly: false), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTokenPackageRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateTokenPackageCommand(
            request.Name,
            request.Description,
            request.PriceVnd,
            request.CreditedBalanceVnd,
            request.IsActive,
            request.DisplayOrder);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{tokenPackageId:guid}")]
    public async Task<IActionResult> Update(Guid tokenPackageId, [FromBody] UpdateTokenPackageRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateTokenPackageCommand(
            tokenPackageId,
            request.Name,
            request.Description,
            request.PriceVnd,
            request.CreditedBalanceVnd,
            request.IsActive,
            request.DisplayOrder);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{tokenPackageId:guid}")]
    public async Task<IActionResult> Delete(Guid tokenPackageId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteTokenPackageCommand(tokenPackageId), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result<string> result)
    {
        if (result.IsSuccess)
        {
            return Ok(new { Message = result.Value });
        }

        return result.ErrorCode switch
        {
            "TOKEN_PACKAGE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private IActionResult ToActionResult(Result<List<TokenPackageDto>> result)
    {
        if (result.IsSuccess)
        {
            return Ok((result.Value ?? new List<TokenPackageDto>()).Select(ToResponse).ToList());
        }

        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    private IActionResult ToActionResult(Result<TokenPackageDto> result)
    {
        if (result.IsSuccess)
        {
            return Ok(ToResponse(result.Value!));
        }

        return result.ErrorCode switch
        {
            "TOKEN_PACKAGE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "TOKEN_PACKAGE_EXISTS" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private static TokenPackageResponse ToResponse(TokenPackageDto x)
        => new(x.TokenPackageId, x.Name, x.Description, x.PriceVnd, x.CreditedBalanceVnd, x.IsActive, x.DisplayOrder, x.CreatedAt, x.UpdatedAt);
}

