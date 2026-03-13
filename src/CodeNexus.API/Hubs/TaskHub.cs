using CodeNexus.Application.Features.Tasks.Commands.GenerateChapterTasks;
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
}
