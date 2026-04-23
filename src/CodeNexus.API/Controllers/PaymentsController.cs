using CodeNexus.API.Models.Requests;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.Commands.CreateVnPayPayment;
using CodeNexus.Application.Features.Payments.Commands.ProcessVnPayCallback;
using CodeNexus.Application.Features.Payments.DTOs;
using CodeNexus.Application.Features.Payments.Queries.GetMyBillingTransactionDetail;
using CodeNexus.Application.Features.Payments.Queries.GetMyBillingTransactions;
using CodeNexus.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using CodeNexus.Domain.Enums;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly VnPaySettings _vnPaySettings;

    public PaymentsController(ISender sender, IOptions<VnPaySettings> vnPayOptions)
    {
        _sender = sender;
        _vnPaySettings = vnPayOptions.Value;
    }

    [HttpGet("my-transactions")]
    [Authorize]
    [ProducesResponseType(typeof(PaginationDto<MyBillingTransactionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTransactions([FromQuery] GetMyBillingTransactionsQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("my-transactions/{paymentTransactionId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(MyBillingTransactionDetailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTransactionDetail(Guid paymentTransactionId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyBillingTransactionDetailQuery(paymentTransactionId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("vnpay/create")]
    [Authorize]
    public async Task<IActionResult> CreateVnPayPayment([FromBody] CreateVnPayPaymentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ReturnUrl))
        {
            return BadRequest(new { ErrorCode = "MISSING_FRONTEND_RETURN_URL", ErrorMessage = "ReturnUrl is required." });
        }

        var callbackUrl = Url.Action(nameof(VnPayReturn), "Payments", values: null, protocol: Request.Scheme)
            ?? $"{Request.Scheme}://{Request.Host}/api/payments/vnpay/return";
        callbackUrl = QueryHelpers.AddQueryString(callbackUrl, "clientReturnUrl", request.ReturnUrl);
        var ipnUrl = Url.Action(nameof(VnPayIpn), "Payments", values: null, protocol: Request.Scheme)
            ?? $"{Request.Scheme}://{Request.Host}/api/payments/vnpay/ipn";

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
        var command = new CreateVnPayPaymentCommand(
            request.TokenPackageId,
            request.TopUpAmountVnd,
            request.OrderInfo,
            callbackUrl,
            ipAddress,
            request.MentorPackageId,
            ipnUrl);

        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("vnpay/ipn")]
    public async Task<IActionResult> VnPayIpn(CancellationToken cancellationToken)
    {
        var parameters = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());
        var command = new ProcessVnPayCallbackCommand(parameters);
        var result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new { RspCode = "00", Message = "Confirm Success" });
        }

        var rspCode = result.ErrorCode switch
        {
            "INVALID_SIGNATURE" => "97",
            "PAYMENT_NOT_FOUND" => "01",
            "INVALID_REQUEST" => "02",
            _ => "99"
        };

        return Ok(new { RspCode = rspCode, Message = result.ErrorMessage ?? "Confirm Fail" });
    }

    [HttpGet("vnpay/return")]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var parameters = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());
        var command = new ProcessVnPayCallbackCommand(parameters);
        var result = await _sender.Send(command, cancellationToken);

        var requestedClientReturnUrl = Request.Query.TryGetValue("clientReturnUrl", out var clientUrl)
            ? clientUrl.ToString()
            : null;
        var safeClientReturnUrl = GetSafeClientReturnUrl(requestedClientReturnUrl);
        var targetReturnUrl = !string.IsNullOrWhiteSpace(safeClientReturnUrl)
            ? safeClientReturnUrl
            : _vnPaySettings.FrontendReturnUrl;

        if (!string.IsNullOrWhiteSpace(targetReturnUrl))
        {
            var redirectParams = new Dictionary<string, string?>
            {
                ["status"] = result.IsSuccess && result.Value?.Status == PaymentStatus.Success ? "success" : "failed"
            };

            var redirectUrl = QueryHelpers.AddQueryString(targetReturnUrl, redirectParams);
            return Redirect(redirectUrl);
        }

        return ToActionResult(result);
    }

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(result);

        return result.ErrorCode switch
        {
            "UNAUTHORIZED" => Unauthorized(new { result.ErrorCode, result.ErrorMessage }),
            "USER_NOT_FOUND" or "PAYMENT_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "INVALID_SIGNATURE" => BadRequest(new { result.ErrorCode, result.ErrorMessage }),
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
            "USER_NOT_FOUND" or "PAYMENT_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            "INVALID_SIGNATURE" => BadRequest(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }

    private static string? GetSafeClientReturnUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return uri.ToString();
    }
}
