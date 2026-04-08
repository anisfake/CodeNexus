using CodeNexus.Application.Features.Lessons.Commands.GenerateLessonContent;
using CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizSkeleton;
using CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizSkeleton;
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
        try
        {
            await Clients.Caller.SendAsync("LessonContentLoading", new { lessonId });

            var lessonResult = await _sender.Send(new GenerateLessonContentCommand(lessonId));

            if (!lessonResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("LessonContentError", new
                {
                    LessonId = lessonId,
                    lessonResult.ErrorCode,
                    lessonResult.ErrorMessage
                });
                return;
            }

            await Clients.Caller.SendAsync("ReceiveLessonContent", lessonResult.Value);

            await Clients.Caller.SendAsync("QuizSkeletonLoading", new { lessonId });

            var quizResult = await _sender.Send(new GenerateQuizSkeletonCommand(lessonId));

            if (quizResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("ReceiveQuizSkeleton", new
                {
                    LessonId = lessonId,
                    Quizzes = quizResult.Value.Quizzes
                });
            }
            else
            {
                await Clients.Caller.SendAsync("QuizSkeletonError", new
                {
                    LessonId = lessonId,
                    quizResult.ErrorCode,
                    quizResult.ErrorMessage
                });
            }

            await Clients.Caller.SendAsync("LessonGenerationCompleted", new
            {
                LessonId = lessonId,
                Message = "Lesson content and quizzes generated successfully!"
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("LessonContentError", new
            {
                LessonId = lessonId,
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
        }
    }

    public async Task RequestQuizSkeleton(Guid lessonId)
    {
        try
        {
            await Clients.Caller.SendAsync("QuizSkeletonLoading", new { lessonId });

            var quizResult = await _sender.Send(new GenerateQuizSkeletonCommand(lessonId));

            if (quizResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("ReceiveQuizSkeleton", new
                {
                    LessonId = lessonId,
                    Quizzes = quizResult.Value.Quizzes
                });
                return;
            }

            await Clients.Caller.SendAsync("QuizSkeletonError", new
            {
                LessonId = lessonId,
                quizResult.ErrorCode,
                quizResult.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("QuizSkeletonError", new
            {
                LessonId = lessonId,
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
        }
    }

    public async Task RequestSingleQuizSkeleton(Guid lessonId)
    {
        try
        {
            await Clients.Caller.SendAsync("SingleQuizSkeletonLoading", new { lessonId });

            var quizResult = await _sender.Send(new GenerateSingleQuizSkeletonCommand(lessonId));

            if (quizResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("ReceiveSingleQuizSkeleton", new
                {
                    LessonId = lessonId,
                    Quiz = quizResult.Value
                });
                return;
            }

            await Clients.Caller.SendAsync("SingleQuizSkeletonError", new
            {
                LessonId = lessonId,
                quizResult.ErrorCode,
                quizResult.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("SingleQuizSkeletonError", new
            {
                LessonId = lessonId,
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
        }
    }
}
