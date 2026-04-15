using CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizQuestions;
using CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizQuestion;
using CodeNexus.Application.Features.Users.Queries.GetMyProfile;
using CodeNexus.Domain.Enums;
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
            await SendWalletBalanceUpdatedAsync();
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

    public async Task RequestSingleQuizQuestion(Guid quizId, QuestionType questionType)
    {
        await Clients.Caller.SendAsync("SingleQuizQuestionLoading", new { quizId, questionType });

        var result = await _sender.Send(new GenerateSingleQuizQuestionCommand(quizId, questionType));

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveSingleQuizQuestion", new
            {
                QuizId = quizId,
                Question = result.Value
            });
            await SendWalletBalanceUpdatedAsync();
        }
        else
        {
            await Clients.Caller.SendAsync("SingleQuizQuestionError", new
            {
                QuizId = quizId,
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
