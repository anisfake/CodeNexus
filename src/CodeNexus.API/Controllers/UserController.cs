using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.Commands.ChangePassword;
using CodeNexus.Application.Features.Users.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUserService;

    public UserController(ISender sender, ICurrentUserService currentUserService)
    {
        _sender = sender;
        _currentUserService = currentUserService;
    }

    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = _currentUserService.GetUserId();
        if (userId == null)
            return Unauthorized(new { ErrorCode = "UNAUTHORIZED", ErrorMessage = "User is not authenticated" });

        var command = new ChangePasswordCommand(userId.Value, request.CurrentPassword, request.NewPassword);
        var result = await _sender.Send(command);

        if (result.IsSuccess)
            return Ok(new ChangePasswordResponse("Password changed successfully"));

        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
