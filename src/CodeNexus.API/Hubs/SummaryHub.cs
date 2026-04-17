using CodeNexus.Application.Features.AISummaries.Commands.GenerateResourceSummary;
using CodeNexus.Application.Features.Users.Queries.GetMyProfile;
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
            await Clients.Caller.SendAsync("ReceiveSummary", new
            {
                result.Value!.SummaryId,
                result.Value.ResourceId,
                result.Value.Title,
                result.Value.Summary,
                startPage = result.Value.StartPage,
                endPage = result.Value.EndPage
            });
            await SendWalletTokenBalanceUpdatedAsync();
        }
        else
        {
            await Clients.Caller.SendAsync("SummaryError", new
            {
                ResourceId = resourceId,
                startPage,
                endPage,
                result.ErrorCode,
                result.ErrorMessage
            });
        }
    }

    private async Task SendWalletTokenBalanceUpdatedAsync()
    {
        var profileResult = await _sender.Send(new GetMyProfileQuery());
        if (!profileResult.IsSuccess || profileResult.Value == null)
        {
            return;
        }

        await Clients.Caller.SendAsync("WalletTokenBalanceUpdated", new
        {
            TokenBalance = profileResult.Value.TokenBalance,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }
}
