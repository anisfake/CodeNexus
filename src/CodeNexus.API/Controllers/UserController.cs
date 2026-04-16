using CloudinaryDotNet.Actions;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.Commands.ChangePassword;
using CodeNexus.Application.Features.Users.Commands.UpdateProfile;
using CodeNexus.Application.Features.Users.Commands.UploadAvatar;
using CodeNexus.Application.Features.Users.Commands.BanUser;
using CodeNexus.Application.Features.Users.Commands.UnbanUser;
using CodeNexus.Application.Features.Users.DTOs;
using CodeNexus.Application.Features.Users.Queries.GetMyProfile;
using CodeNexus.Application.Features.Users.Queries.GetAllUsers;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CodeNexus.Application.Features.Users.Queries.GetUserById;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly ISender _sender;

    public UserController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPut("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var command = new ChangePasswordCommand(request.CurrentPassword, request.NewPassword);
        var result = await _sender.Send(command);

        if (result.IsSuccess)
            return Ok(new ChangePasswordResponse("Password changed successfully"));

        return ToActionResult(result);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Profile()
    {
        var query = new GetMyProfileQuery();
        var result = await _sender.Send(query);

        return ToActionResult(result);
    }

    [HttpGet("me/token-balance")]
    [HttpGet("me/balance")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTokenBalance()
    {
        var query = new GetMyProfileQuery();
        var result = await _sender.Send(query);

        if (!result.IsSuccess || result.Value == null)
        {
            return ToActionResult(result);
        }

        return Ok(new
        {
            tokenBalance = result.Value.TokenBalance,
            updatedAtUtc = DateTime.UtcNow
        });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PaginationDto<UserRespone>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] GetAllUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAllUsersQuery
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Role = request.Role,
            SearchTerm = request.SearchTerm,
            SortBy = request.SortBy,
            SortDescending = request.SortDescending
        };

        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{userId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UserRespone), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserById(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserByIdQuery(userId);
        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("/api/users/me/avatar")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var command = new UploadAvatarCommand(stream, file.FileName);
        var result = await _sender.Send(command);

        if (result.IsSuccess)
            return Ok("Upload avatar successfully");

        return ToActionResult(result);
    }

    [HttpPut("/api/users/me")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var command = new UpdateProfileCommand(
            request.FirstName,
            request.LastName,
            request.Bio,
            request.DateOfBirth,
            request.Phone,
            request.Address,
            request.DailyReminderTime
        );

        var result = await _sender.Send(command);

        return ToActionResult(result);
    }

    [HttpPost("{userId}/ban")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BanUser(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var command = new BanUserCommand(userId);
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{userId}/unban")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnbanUser(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var command = new UnbanUserCommand(userId);
        var result = await _sender.Send(command, cancellationToken);
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
            "USER_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "OTP_RATE_LIMITED" or "RESEND_RATE_LIMITED" => StatusCode(StatusCodes.Status429TooManyRequests, new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
