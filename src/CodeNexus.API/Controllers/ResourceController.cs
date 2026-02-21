using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.Commands.UploadResource;
using CodeNexus.Application.Features.Resources.Commands.UpdateResource;
using CodeNexus.Application.Features.Resources.Commands.DeleteResource;
using CodeNexus.Application.Features.Resources.DTOs;
using CodeNexus.Application.Features.Users.Commands.ChangePassword;
using CodeNexus.Application.Features.Users.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Entities;
using System.Threading.Tasks;
using CodeNexus.Application.Features.Resources.Queries.GetMyResources;

namespace CodeNexus.API.Controllers
{
    [Route("api/resources")]
    [ApiController]
    [Authorize]
    public class ResourceController : ControllerBase
    {
        private readonly ISender _sender;

        public ResourceController(ISender sender)
        {
            _sender = sender;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UploadResource([FromForm] UploadResourceRequest request)
        {
            using var stream = request.File?.OpenReadStream();
            var command = new UploadResourceCommand()
            {
                FileName = request.File?.FileName ?? "",
                Title = request.Title,
                Type = request.Type,
                Url = request.Url,
                Description = request.Description,
                FilePath = stream,
                SubjectId = request.SubjectId
            };
            var result = await _sender.Send(command);

            return ToActionResult(result);
        }

        [HttpGet("/api/users/me/resources")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyResource([FromQuery] GetMyResourceRequest request)
        {
            var query = new GetMyResourcesQuery
            {
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                Type = request.Type,
                SubjectId = request.SubjectId,
                SearchTerm = request.SearchTerm,
                SortBy = request.SortBy,
                SortDescending = request.SortDescending
            };
            var result = await _sender.Send(query);

            return Ok(result);
        }

        [HttpPut("{resourceId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateResource(Guid resourceId, [FromForm] UpdateResourceRequest request)
        {
            using var stream = request.File?.OpenReadStream();
            var command = new UpdateResourceCommand(
                resourceId,
                request.Title,
                request.Description,
                request.Url,
                stream,
                request.File?.FileName
            );
            var result = await _sender.Send(command);

            return ToActionResult(result);
        }

        [HttpDelete("{resourceId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteResource(Guid resourceId)
        {
            var command = new DeleteResourceCommand(resourceId);
            var result = await _sender.Send(command);

            return ToActionResult(result);
        }

        private IActionResult ToActionResult(Result result)
        {
            if (result.IsSuccess)
                return Ok(result);

            return result.ErrorCode switch
            {
                "EMAIL_EXISTS" or "USERNAME_EXISTS" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
                "OTP_RATE_LIMITED" or "RESEND_RATE_LIMITED" => StatusCode(StatusCodes.Status429TooManyRequests, new { result.ErrorCode, result.ErrorMessage }),
                _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
            };
        }

        private IActionResult ToActionResult<T>(Result<T> result)
        {
            if (result.IsSuccess)
                return Ok(result.Value);

            return result.ErrorCode switch
            {
                "UNAUTHORIZED" or "USERNAME_EXISTS" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
                "OTP_RATE_LIMITED" or "RESEND_RATE_LIMITED" => StatusCode(StatusCodes.Status429TooManyRequests, new { result.ErrorCode, result.ErrorMessage }),
                _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
            };
        }
    }
}
