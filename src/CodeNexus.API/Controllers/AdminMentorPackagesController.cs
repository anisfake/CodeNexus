using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorPackages.Commands.CreateMentorPackage;
using CodeNexus.Application.Features.MentorPackages.Commands.DeleteMentorPackage;
using CodeNexus.Application.Features.MentorPackages.Commands.UpdateMentorPackage;
using CodeNexus.Application.Features.MentorPackages.DTOs;
using CodeNexus.Application.Features.MentorPackages.Queries.GetAllMentorPackages;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/admin/mentor-packages")]
[Authorize(Roles = "Admin")]
public class AdminMentorPackagesController : ControllerBase
{
    private readonly ISender _sender;

    public AdminMentorPackagesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAllMentorPackagesQuery(ActiveOnly: false), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMentorPackageRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateMentorPackageCommand(
            request.Name,
            request.Description,
            request.PriceVnd,
            request.SharesFromMentorLimit,
            request.ValidationRequestLimit,
            request.TaskReviewLimit,
            request.IsActive,
            request.DisplayOrder);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{mentorPackageId:guid}")]
    public async Task<IActionResult> Update(Guid mentorPackageId, [FromBody] UpdateMentorPackageRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateMentorPackageCommand(
            mentorPackageId,
            request.Name,
            request.Description,
            request.PriceVnd,
            request.SharesFromMentorLimit,
            request.ValidationRequestLimit,
            request.TaskReviewLimit,
            request.IsActive,
            request.DisplayOrder);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{mentorPackageId:guid}")]
    public async Task<IActionResult> Delete(Guid mentorPackageId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteMentorPackageCommand(mentorPackageId), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result<List<MentorPackageDto>> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "MENTOR_PACKAGE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private IActionResult ToActionResult(Result<MentorPackageDto> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "MENTOR_PACKAGE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "PACKAGE_NAME_EXISTS" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private IActionResult ToActionResult(Result<string> result)
    {
        if (result.IsSuccess)
            return Ok(new { Message = result.Value });

        return result.ErrorCode switch
        {
            "MENTOR_PACKAGE_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
