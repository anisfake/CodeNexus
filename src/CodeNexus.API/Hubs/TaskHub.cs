using CodeNexus.Application.Features.Tasks.Commands.GenerateChapterTasks;
using CodeNexus.Application.Features.Tasks.Commands.GenerateSingleTask;
using CodeNexus.Application.Features.Users.Queries.GetMyProfile;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Hubs;

[Authorize]
public class TaskHub : Hub
{
    private readonly ISender _sender;

    public TaskHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task RequestChapterTasks(Guid chapterId)
    {
        var ct = Context.ConnectionAborted;
        await Clients.Caller.SendAsync("ChapterTasksLoading", new { chapterId }, ct);

        var result = await _sender.Send(new GenerateChapterTasksCommand(chapterId), ct);

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveChapterTasks", result.Value, ct);
            await SendWalletTokenBalanceUpdatedAsync(ct);
        }
        else
        {
            await Clients.Caller.SendAsync("ChapterTasksError", new
            {
                ChapterId = chapterId,
                result.ErrorCode,
                result.ErrorMessage
            }, ct);
        }
    }

    public async Task RequestSingleTask(Guid chapterId, string? title, TaskType taskType)
    {
        var ct = Context.ConnectionAborted;
        await Clients.Caller.SendAsync("SingleTaskLoading", new { chapterId, title, taskType }, ct);

        var result = await _sender.Send(new GenerateSingleTaskCommand(chapterId, title, taskType), ct);

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveSingleTask", new
            {
                ChapterId = chapterId,
                Task = result.Value
            }, ct);
            await SendWalletTokenBalanceUpdatedAsync(ct);
        }
        else
        {
            await Clients.Caller.SendAsync("SingleTaskError", new
            {
                ChapterId = chapterId,
                result.ErrorCode,
                result.ErrorMessage
            }, ct);
        }
    }

    private async Task SendWalletTokenBalanceUpdatedAsync(CancellationToken ct)
    {
        var profileResult = await _sender.Send(new GetMyProfileQuery(), ct);
        if (!profileResult.IsSuccess || profileResult.Value == null)
        {
            return;
        }

        await Clients.Caller.SendAsync("WalletTokenBalanceUpdated", new
        {
            TokenBalance = profileResult.Value.TokenBalance,
            UpdatedAtUtc = DateTime.UtcNow
        }, ct);
    }
}
