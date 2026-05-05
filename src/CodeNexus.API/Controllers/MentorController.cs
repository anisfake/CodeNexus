using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using CodeNexus.Application.Features.LearningPathMentorReviews.Queries.GetMyLearningPathReviews;
using CodeNexus.Application.Features.Mentors.Commands.UpsertMentorReview;
using CodeNexus.Application.Features.Mentors.DTOs;
using CodeNexus.Application.Features.Mentors.Queries.GetMentorProfile;
using CodeNexus.Application.Features.Mentors.Queries.GetMentors;
using CodeNexus.Application.Features.Mentors.Queries.GetMyMentorReviews;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/mentors")]
public class MentorController : ControllerBase
{
    private readonly ISender _sender;

    public MentorController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(PaginationDto<MentorListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMentors([FromQuery] GetMentorsRequest request, CancellationToken cancellationToken)
    {
        var query = new GetMentorsQuery(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.SubjectCategory,
            request.SubjectName);

        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{mentorId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(MentorProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMentorProfile(Guid mentorId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMentorProfileQuery(mentorId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me/reviews")]
    [Authorize(Roles = "Mentor")]
    [ProducesResponseType(typeof(MentorReviewListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyReviews(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetMyMentorReviewsQuery(pageNumber, pageSize), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me/learning-path-reviews")]
    [Authorize(Roles = "Mentor")]
    [ProducesResponseType(typeof(List<AdminMentorReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyLearningPathReviews(
        [FromQuery] LearningPathMentorReviewDecisionStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetMyLearningPathReviewsQuery(status, page, pageSize), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{mentorId:guid}/review")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(UpsertMentorReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertReview(
        Guid mentorId,
        [FromBody] UpsertMentorReviewRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpsertMentorReviewCommand(mentorId, request.Score, request.Comment);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "ACCESS_DENIED" => StatusCode(StatusCodes.Status403Forbidden, new { result.ErrorCode, result.ErrorMessage }),
            "MENTOR_NOT_FOUND" or "USER_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
