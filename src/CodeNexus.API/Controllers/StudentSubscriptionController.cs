using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorPackages.DTOs;
using CodeNexus.Application.Features.MentorPackages.Queries.GetAllMentorPackages;
using CodeNexus.Application.Features.StudentMentorSubscriptions.DTOs;
using CodeNexus.Application.Features.StudentMentorSubscriptions.Queries.GetStudentMentorQuota;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/student/mentor-subscription")]
public class StudentSubscriptionController : ControllerBase
{
    private readonly ISender _sender;

    public StudentSubscriptionController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("quota")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetQuota(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetStudentMentorQuotaQuery(), cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);
        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    [HttpGet("packages")]
    [Authorize]
    public async Task<IActionResult> GetPackages(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAllMentorPackagesQuery(ActiveOnly: true), cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);
        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
