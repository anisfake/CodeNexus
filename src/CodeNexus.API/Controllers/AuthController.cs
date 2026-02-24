using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.Commands.ForgotPassword;
using CodeNexus.Application.Features.Auth.Commands.Login;
using CodeNexus.Application.Features.Auth.Commands.LoginWithGoogle;
using CodeNexus.Application.Features.Auth.Commands.Logout;
using CodeNexus.Application.Features.Auth.Commands.RefreshAccessToken;
using CodeNexus.Application.Features.Auth.Commands.Register;
using CodeNexus.Application.Features.Auth.Commands.ResendOtp;
using CodeNexus.Application.Features.Auth.Commands.ResetPassword;
using CodeNexus.Application.Features.Auth.Commands.VerifyOtp;
using CodeNexus.Application.Features.Auth.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var command = new RegisterCommand(request.Email, request.Username, request.FirstName, request.LastName, request.Password);
        var result = await _sender.Send(command);
        return ToActionResult(result);
    }

    [HttpPost("login-google")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LoginGoogle([FromBody] LoginWithGoogleCommand command)
    {
        var result = await _sender.Send(command);
        if (result.IsSuccess)
            return Ok(result.Value);

        return ToActionResult(result);
    }

    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var command = new VerifyOtpCommand(request.Email, request.Otp);
        var result = await _sender.Send(command);
        return ToActionResult(result);
    }

    [HttpPost("resend-otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
    {
        var command = new ResendOtpCommand(request.Email);
        var result = await _sender.Send(command);
        return ToActionResult(result);
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var command = new ForgotPasswordCommand(request.Email);
        var result = await _sender.Send(command);
        return ToActionResult(result);
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var command = new ResetPasswordCommand(request.ResetToken, request.NewPassword);
        var result = await _sender.Send(command);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _sender.Send(command);
        if (result.IsSuccess)
            return Ok(result.Value);

        return ToActionResult(result);
    }

    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var command = new RefreshAccessTokenCommand(request.RefreshToken);
        var result = await _sender.Send(command);
        if (result.IsSuccess)
            return Ok(result.Value);

        return ToActionResult(result);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
    {
        var accessToken = request?.AccessToken
            ?? HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

        var command = new LogoutCommand(accessToken, request?.RefreshToken);
        var result = await _sender.Send(command);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(new { message = "Operation completed successfully" });

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
            return CreatedAtAction(nameof(VerifyOtp), result.Value);

        return result.ErrorCode switch
        {
            "EMAIL_EXISTS" or "USERNAME_EXISTS" => Conflict(new { result.ErrorCode, result.ErrorMessage }),
            "OTP_RATE_LIMITED" or "RESEND_RATE_LIMITED" => StatusCode(StatusCodes.Status429TooManyRequests, new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
