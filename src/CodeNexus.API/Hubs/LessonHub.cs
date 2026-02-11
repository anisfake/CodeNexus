using CodeNexus.Application.Features.Lessons.Commands.ConfirmLessonContent;
using CodeNexus.Application.Features.Lessons.Commands.GenerateLessonContent;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Hubs;

[Authorize]
public class LessonHub : Hub
{
    private readonly ISender _sender;

    public LessonHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task RequestLessonContent(Guid lessonId)
    {
        await Clients.Caller.SendAsync("LessonContentLoading", new { lessonId });

        var result = await _sender.Send(new GenerateLessonContentCommand(lessonId));

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveLessonContent", result.Value);
        }
        else
        {
            await Clients.Caller.SendAsync("LessonContentError", new
            {
                LessonId = lessonId,
                result.ErrorCode,
                result.ErrorMessage
            });
        }
    }

    public async Task ConfirmLessonContent(Guid lessonId, string content)
    {
        var result = await _sender.Send(new ConfirmLessonContentCommand(lessonId, content));

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("LessonContentConfirmed", new { lessonId });
        }
        else
        {
            await Clients.Caller.SendAsync("LessonContentError", new
            {
                LessonId = lessonId,
                result.ErrorCode,
                result.ErrorMessage
            });
        }
    }
}
