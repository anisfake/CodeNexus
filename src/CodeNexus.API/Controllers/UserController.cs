using CloudinaryDotNet.Actions;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.Commands.ChangePassword;
using CodeNexus.Application.Features.Users.Commands.UpdateProfile;
using CodeNexus.Application.Features.Users.Commands.UploadAvatar;
using CodeNexus.Application.Features.Users.DTOs;
using CodeNexus.Application.Features.Users.Queries.GetMyProfile;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly ISender _sender;

    public UserController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPut("change-password")]
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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Profile()
    {
        var query = new GetMyProfileQuery();
        var result = await _sender.Send(query);

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
            request.Address
        );

        var result = await _sender.Send(command);

        if (result.IsSuccess)
            return Ok("Update profile successfully");

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
