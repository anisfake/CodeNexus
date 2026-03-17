using CodeNexus.Application.Features.TutorChat.Commands.SendTutorMessage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Hubs;

[Authorize]
public class TutorChatHub : Hub
{
    private readonly ISender _sender;

    public TutorChatHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task SendTutorMessage(
        Guid? conversationId,
        Guid? learningPathId,
        Guid? chapterId,
        Guid? lessonId,
        string message)
    {
        try
        {
            await Clients.Caller.SendAsync("TutorMessageStarted");

            var command = new SendTutorMessageCommand(
                conversationId,
                learningPathId,
                chapterId,
                lessonId,
                message);

            var result = await _sender.Send(command);

            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("TutorMessageError", new
                {
                    result.ErrorCode,
                    result.ErrorMessage
                });
                return;
            }

            await Clients.Caller.SendAsync("TutorMessageReceived", result.Value);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("TutorMessageError", new
            {
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
        }
    }
}
