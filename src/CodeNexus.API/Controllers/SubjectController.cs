using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subjects.Commands.CreateSubject;
using CodeNexus.Application.Features.Subjects.Commands.UpdateSubject;
using CodeNexus.Application.Features.Subjects.Commands.DeleteSubject;
using CodeNexus.Application.Features.Subjects.DTOs;
using CodeNexus.Application.Features.Subjects.Queries.GetSubjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/subjects")]

public class SubjectController : ControllerBase
{
    private readonly ISender _sender;

    public SubjectController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> CreateSubject(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        var command = new CreateSubjectCommand(
            request.Name,
            request.Description,
            request.Color,
            request.Icon
        );

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GetSubjects(CancellationToken cancellationToken)
    {
        var query = new GetSubjectsQuery();
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{subjectId}")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> UpdateSubject(Guid subjectId, UpdateSubjectRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateSubjectCommand(
            subjectId,
            request.Name,
            request.Description,
            request.Color,
            request.Icon
        );

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{subjectId}")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> DeleteSubject(Guid subjectId, CancellationToken cancellationToken)
    {
        var command = new DeleteSubjectCommand(subjectId);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "SUBJECT_EXISTS" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "SUBJECT_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
