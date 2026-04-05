using CodeNexus.Application.Features.TutorChat.Commands.SendTutorMessage;
using CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationMessages;
using CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationSummaries;
using CodeNexus.Application.Features.TutorChat.Queries.ResolveTutorConversation;
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

    public async Task RequestTutorMessages(Guid conversationId, int pageNumber = 1, int pageSize = 30)
    {
        try
        {
            await Clients.Caller.SendAsync("TutorMessagesLoading");

            var query = new GetTutorConversationMessagesQuery(conversationId, pageNumber, pageSize);
            var result = await _sender.Send(query);

            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("TutorMessagesError", new
                {
                    result.ErrorCode,
                    result.ErrorMessage
                });
                return;
            }

            await Clients.Caller.SendAsync("TutorMessagesLoaded", result.Value);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("TutorMessagesError", new
            {
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
        }
    }

    public async Task RequestTutorSummaries(Guid conversationId, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            await Clients.Caller.SendAsync("TutorSummariesLoading");

            var query = new GetTutorConversationSummariesQuery(conversationId, pageNumber, pageSize);
            var result = await _sender.Send(query);

            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("TutorSummariesError", new
                {
                    result.ErrorCode,
                    result.ErrorMessage
                });
                return;
            }

            await Clients.Caller.SendAsync("TutorSummariesLoaded", result.Value);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("TutorSummariesError", new
            {
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
        }
    }

    public async Task RequestResolveTutorConversation(
        Guid? learningPathId,
        Guid? chapterId,
        Guid? lessonId,
        bool createIfMissing = true)
    {
        try
        {
            await Clients.Caller.SendAsync("TutorConversationResolveStarted");

            var query = new ResolveTutorConversationQuery(
                learningPathId,
                chapterId,
                lessonId,
                createIfMissing);

            var result = await _sender.Send(query);

            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("TutorConversationResolveError", new
                {
                    result.ErrorCode,
                    result.ErrorMessage
                });
                return;
            }

            await Clients.Caller.SendAsync("TutorConversationResolved", result.Value);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("TutorConversationResolveError", new
            {
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
        }
    }
}
