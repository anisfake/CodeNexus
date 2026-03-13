using CodeNexus.Application.Features.AISummaries.Commands.GenerateResourceSummary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Hubs;

[Authorize]
public class SummaryHub : Hub
{
    private readonly ISender _sender;

    public SummaryHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task RequestResourceSummary(Guid resourceId, int startPage, int endPage)
    {
        await Clients.Caller.SendAsync("SummaryLoading", new { resourceId, startPage, endPage });

        var result = await _sender.Send(new GenerateResourceSummaryCommand(resourceId, startPage, endPage));

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveSummary", result.Value);
        }
        else
        {
            await Clients.Caller.SendAsync("SummaryError", new
            {
                ResourceId = resourceId,
                result.ErrorCode,
                result.ErrorMessage
            });
        }
    }
}
