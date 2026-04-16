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
        var ct = Context.ConnectionAborted;
        await Clients.Caller.SendAsync("QuizQuestionsLoading", new { quizId }, ct);

        var result = await _sender.Send(new GenerateQuizQuestionsCommand(quizId), ct);

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveQuizQuestions", new
            {
                QuizId = quizId,
                Questions = result.Value
            }, ct);
        }
        else
        {
            await Clients.Caller.SendAsync("QuizQuestionsError", new
            {
                QuizId = quizId,
                result.ErrorCode,
                result.ErrorMessage
            }, ct);
        }
    }

    public async Task RequestSingleQuizQuestion(Guid quizId, QuestionType questionType)
    {
        var ct = Context.ConnectionAborted;
        await Clients.Caller.SendAsync("SingleQuizQuestionLoading", new { quizId, questionType }, ct);

        var result = await _sender.Send(new GenerateSingleQuizQuestionCommand(quizId, questionType), ct);

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveSingleQuizQuestion", new
            {
                QuizId = quizId,
                Question = result.Value
            }, ct);
        }
        else
        {
            await Clients.Caller.SendAsync("SingleQuizQuestionError", new
            {
                QuizId = quizId,
                result.ErrorCode,
                result.ErrorMessage
            }, ct);
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
