using CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizQuestions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Hubs;

[Authorize]
public class QuizHub : Hub
{
    private readonly ISender _sender;

    public QuizHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task RequestQuizQuestions(Guid quizId)
    {
        await Clients.Caller.SendAsync("QuizQuestionsLoading", new { quizId });

        var result = await _sender.Send(new GenerateQuizQuestionsCommand(quizId));

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveQuizQuestions", result.Value);
        }
        else
        {
            await Clients.Caller.SendAsync("QuizQuestionsError", new
            {
                QuizId = quizId,
                result.ErrorCode,
                result.ErrorMessage
            });
        }
    }
}
