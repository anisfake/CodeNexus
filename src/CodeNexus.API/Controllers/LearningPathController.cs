using Azure.Core;
using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Chapters.Commands.GenerateChapterContent;
using CodeNexus.Application.Features.Chapters.Queries.GetChapterCompletionStatus;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.CreateMentorLearningPathDraft;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.AdoptSuggestedLearningPath;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateChapterSkeleton;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateMentorLearningPathDraft;
using CodeNexus.Application.Features.LearningPathShares.Commands.SendLearningPathShare;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetAllLearningPaths;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathDraftDetail;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathByUserId;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetMyLearningPathDrafts;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathSuggestions;
using CodeNexus.Application.Features.LearningPaths.Queries.GetLearningPathProgress;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Application.Features.Lessons.Commands.GenerateLessonContent;
using CodeNexus.Application.Features.Lessons.Commands.MarkLessonContentRead;
using CodeNexus.Application.Features.Lessons.Queries.GetLessonReadStatus;
using CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizQuestions;
using CodeNexus.Application.Features.Tasks.Commands.GenerateChapterTasks;
using CodeNexus.Application.Features.AISummaries.Commands.GenerateResourceSummary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/learningpaths")]

public class LearningPathController : ControllerBase
{
    private readonly ISender _sender;
    public LearningPathController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GenerateSkeleton([FromBody] GenerateLearningPathSkeletonRequest request, CancellationToken cancellationToken)
    {
        var command = new GenerateLearningPathSkeletonCommand(
            request.SubjectId,
            request.Goals,
            request.ComplexityLevel,
            request.LanguageSelection,
            request.SaveAsDraft
        );

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { errorCode = result.ErrorCode, errorMessage = result.ErrorMessage });
        }

        return ToActionResult(result);
    }

    [HttpGet("my-drafts")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> GetMyDrafts([FromQuery] GetMyLearningPathDraftsRequest request, CancellationToken cancellationToken)
    {
        var query = new GetMyLearningPathDraftsQuery(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.SubjectId,
            request.SortDescending);

        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("my-drafts/{pathId:guid}")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> GetMyDraftById(Guid pathId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetLearningPathDraftDetailQuery(pathId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("ai-draft")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> GenerateAIDraft([FromBody] GenerateLearningPathSkeletonRequest request, CancellationToken cancellationToken)
    {
        var command = new GenerateLearningPathSkeletonCommand(
            request.SubjectId,
            request.Goals,
            request.ComplexityLevel,
            request.LanguageSelection,
            true
        );

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { errorCode = result.ErrorCode, errorMessage = result.ErrorMessage });
        }

        return ToActionResult(result);
    }

    [HttpPost("manual-draft")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> CreateManualDraft([FromBody] CreateMentorLearningPathDraftRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateMentorLearningPathDraftCommand(
            request.SubjectId,
            request.Goals,
            request.ComplexityLevel,
            request.LanguageSelection,
            request.Title,
            request.Description,
            request.StartDate,
            request.EndDate,
            request.Chapters);

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("manual-draft/{pathId:guid}")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> UpdateManualDraft(Guid pathId, [FromBody] UpdateMentorLearningPathDraftRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateMentorLearningPathDraftCommand(
            pathId,
            request.SubjectId,
            request.Goals,
            request.ComplexityLevel,
            request.LanguageSelection,
            request.Title,
            request.Description,
            request.StartDate,
            request.EndDate,
            request.Chapters);

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("suggestions")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GetSuggestions([FromBody] GenerateLearningPathSkeletonRequest request, CancellationToken cancellationToken)
    {
        var query = new GetLearningPathSuggestionsQuery(
            request.SubjectId,
            request.Goals,
            request.ComplexityLevel,
            request.LanguageSelection
        );

        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("suggestions/{suggestedPathId:guid}/adopt")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> AdoptSuggestedLearningPath(
        Guid suggestedPathId,
        [FromBody] AdoptSuggestedLearningPathRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AdoptSuggestedLearningPathCommand(
            suggestedPathId,
            request.SubjectId,
            request.Goals,
            request.ComplexityLevel,
            request.LanguageSelection
        );

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet]
    [Authorize]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> GetAllLearningPath([FromQuery] GetAllLearningPathRequest request, CancellationToken cancellationToken)
    {
        var query = new GetAllLearningPathQuery(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.SubjectId,
            request.Status,
            request.SortDescending
        );
        var result = await _sender.Send(query, cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("user/{userId:guid}")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GetLearningPathByUserId(Guid userId, [FromQuery] GetLearningPathByUserIdRequest request, CancellationToken cancellationToken)
    {
        var query = new GetLearningPathByUserIdQuery(
            userId,
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.SubjectId,
            request.Status,
            request.SortDescending
        );
        var result = await _sender.Send(query, cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("lessons/{lessonId:guid}/content")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GenerateLessonContent(Guid lessonId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GenerateLessonContentCommand(lessonId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("lessons/{lessonId:guid}/read")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> MarkLessonContentRead(Guid lessonId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkLessonContentReadCommand(lessonId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("lessons/{lessonId:guid}/read-status")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GetLessonReadStatus(Guid lessonId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetLessonReadStatusQuery(lessonId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("chapters/{chapterId:guid}/generate-content")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GenerateChapterContent(Guid chapterId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GenerateChapterContentCommand(chapterId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("chapters/{chapterId:guid}/completion-status")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GetChapterCompletionStatus(Guid chapterId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetChapterCompletionStatusQuery(chapterId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("quizzes/{quizId:guid}/generate-questions")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GenerateQuizQuestions(Guid quizId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GenerateQuizQuestionsCommand(quizId), cancellationToken);
        return ToActionResult(result);
    }


    [HttpPost("chapters/{chapterId:guid}/generate-tasks")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GenerateChapterTasks(Guid chapterId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GenerateChapterTasksCommand(chapterId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{pathId:guid}/chapters/{orderIndex:int}/skeleton")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GenerateChapterSkeleton(Guid pathId, int orderIndex, CancellationToken cancellationToken)
    {
        var command = new GenerateChapterSkeletonCommand(pathId, orderIndex);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{pathId:guid}/share/{studentId:guid}")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> ShareLearningPathViaChat(Guid pathId, Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SendLearningPathShareCommand(pathId, studentId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{pathId:guid}/progress")]
    [Authorize(Roles = "Mentor, Student")]
    public async Task<IActionResult> GetLearningPathProgress(Guid pathId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetLearningPathProgressQuery(pathId), cancellationToken);
        return ToActionResult(result);
    }


    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(result);

        return result.ErrorCode switch
        {
            "ACCESS_DENIED" => StatusCode(StatusCodes.Status403Forbidden, new { result.ErrorCode, result.ErrorMessage }),
            "EMAIL_EXISTS" or "USERNAME_EXISTS" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "SHARE_ALREADY_PENDING" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "LEARNING_PATH_NOT_FOUND" or "STUDENT_NOT_FOUND" or "SHARE_NOT_FOUND" or "LESSON_NOT_FOUND" or "CHAPTER_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "OTP_RATE_LIMITED" or "RESEND_RATE_LIMITED" or "LEARNING_PATH_LIMIT_EXCEEDED" => StatusCode(StatusCodes.Status429TooManyRequests, new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "ACCESS_DENIED" => StatusCode(StatusCodes.Status403Forbidden, new { result.ErrorCode, result.ErrorMessage }),
            "UNAUTHORIZED" or "USERNAME_EXISTS" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "SHARE_ALREADY_PENDING" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "LEARNING_PATH_NOT_FOUND" or "STUDENT_NOT_FOUND" or "SHARE_NOT_FOUND" or "LESSON_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "OTP_RATE_LIMITED" or "RESEND_RATE_LIMITED" or "LEARNING_PATH_LIMIT_EXCEEDED" => StatusCode(StatusCodes.Status429TooManyRequests, new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}

