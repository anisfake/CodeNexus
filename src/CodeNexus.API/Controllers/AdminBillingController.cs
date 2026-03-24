using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using CodeNexus.Application.Features.Payments.Queries.GetBillingSummary;
using CodeNexus.Application.Features.Payments.Queries.GetBillingTransactionDetail;
using CodeNexus.Application.Features.Payments.Queries.GetBillingTransactions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeNexus.API.Controllers;

[ApiController]
[Route("api/admin/billing")]
[Authorize(Roles = "Admin")]
public class AdminBillingController : ControllerBase
{
    private readonly ISender _sender;

    public AdminBillingController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("transactions")]
    [ProducesResponseType(typeof(PaginationDto<BillingTransactionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] GetBillingTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("transactions/{paymentTransactionId:guid}")]
    [ProducesResponseType(typeof(BillingTransactionDetailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactionDetail(
        Guid paymentTransactionId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetBillingTransactionDetailQuery(paymentTransactionId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(BillingSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetBillingSummaryQuery(fromUtc, toUtc), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "PAYMENT_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}

