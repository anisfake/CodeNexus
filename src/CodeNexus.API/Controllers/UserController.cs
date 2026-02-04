using CloudinaryDotNet.Actions;
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
    private readonly IApplicationDbContext _context;

    public UserController(ISender sender, IApplicationDbContext context)
    {
        _sender = sender;
        _context = context;
    }

    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var command = new ChangePasswordCommand(request.CurrentPassword, request.NewPassword);
        var result = await _sender.Send(command);

        if (result.IsSuccess)
            return Ok(new ChangePasswordResponse("Password changed successfully"));

        if (result.ErrorCode == "UNAUTHORIZED")
            return Unauthorized(new { result.ErrorCode, result.ErrorMessage });

        return BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
