using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Tasks.Commands.DeleteTask;
using CodeNexus.Application.Features.Tasks.Commands.GenerateSingleTask;
using CodeNexus.Application.Features.Tasks.Commands.UpdateTaskStatus;
using CodeNexus.Application.Features.Tasks.Queries.GetChapterTasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize(Roles = "Mentor, Student")]
public class TaskController : ControllerBase
{
    private readonly ISender _sender;

    public TaskController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("chapter/{chapterId}")]
    public async Task<IActionResult> GetChapterTasks(Guid chapterId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetChapterTasksQuery(chapterId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("generate-single")]
    public async Task<IActionResult> GenerateSingleTask([FromBody] GenerateSingleTaskRequest request, CancellationToken cancellationToken)
    {
        var command = new GenerateSingleTaskCommand(request.ChapterId, request.Title, request.TaskType);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{taskId}/status")]
    public async Task<IActionResult> UpdateTaskStatus(Guid taskId, [FromBody] UpdateTaskStatusRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateTaskStatusCommand(taskId, request.Status);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{taskId}")]
    public async Task<IActionResult> DeleteTask(Guid taskId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteTaskCommand(taskId), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(new { message = "Success" });

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "TASK_NOT_FOUND" or "CHAPTER_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "CHAPTER_NOT_FOUND" or "TASK_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
