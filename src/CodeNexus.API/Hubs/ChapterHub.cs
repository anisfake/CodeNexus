using CodeNexus.Application.Features.Chapters.Commands.GenerateChapterContent;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Hubs;

[Authorize]
public class ChapterHub : Hub
{
    private readonly ISender _sender;

    public ChapterHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task RequestChapterContent(Guid chapterId)
    {
        await Clients.Caller.SendAsync("ChapterContentLoading", new { chapterId });

        var result = await _sender.Send(new GenerateChapterContentCommand(chapterId));

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveChapterContent", result.Value);
        }
        else
        {
            await Clients.Caller.SendAsync("ChapterContentError", new
            {
                ChapterId = chapterId,
                result.ErrorCode,
                result.ErrorMessage
            });
        }
    }
}
