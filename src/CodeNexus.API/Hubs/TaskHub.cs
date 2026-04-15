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
        await Clients.Caller.SendAsync("ChapterTasksLoading", new { chapterId });

        var result = await _sender.Send(new GenerateChapterTasksCommand(chapterId));

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveChapterTasks", result.Value);
            await SendWalletBalanceUpdatedAsync();
        }
        else
        {
            await Clients.Caller.SendAsync("ChapterTasksError", new
            {
                ChapterId = chapterId,
                result.ErrorCode,
                result.ErrorMessage
            });
        }
    }

    public async Task RequestSingleTask(Guid chapterId, string? title, TaskType taskType)
    {
        await Clients.Caller.SendAsync("SingleTaskLoading", new { chapterId, title, taskType });

        var result = await _sender.Send(new GenerateSingleTaskCommand(chapterId, title, taskType));

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveSingleTask", result.Value);
            await SendWalletBalanceUpdatedAsync();
        }
        else
        {
            await Clients.Caller.SendAsync("SingleTaskError", new
            {
                ChapterId = chapterId,
                result.ErrorCode,
                result.ErrorMessage
            });
        }
    }

    private async Task SendWalletBalanceUpdatedAsync()
    {
        var profileResult = await _sender.Send(new GetMyProfileQuery());
        if (!profileResult.IsSuccess || profileResult.Value == null)
        {
            return;
        }

        await Clients.Caller.SendAsync("WalletBalanceUpdated", new
        {
            BalanceVnd = profileResult.Value.BalanceVnd,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }
}
