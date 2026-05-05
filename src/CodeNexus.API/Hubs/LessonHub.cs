using CodeNexus.Application.Features.Lessons.Commands.GenerateLessonContent;
using CodeNexus.Application.Features.Lessons.Queries.EstimateBulkLearningPathGenerationBudget;
using CodeNexus.Application.Features.Lessons.Queries.GetLearningPathGenerationWorkItems;
using CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizSkeleton;
using CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizSkeleton;
using CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizQuestions;
using CodeNexus.Application.Features.Users.Queries.GetMyProfile;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

namespace CodeNexus.API.Hubs;

[Authorize]
public class LessonHub : Hub
{
    private readonly ISender _sender;
    private readonly IServiceScopeFactory _scopeFactory;

    public LessonHub(ISender sender, IServiceScopeFactory scopeFactory)
    {
        _sender = sender;
        _scopeFactory = scopeFactory;
    }

    public async Task RequestLessonContent(Guid lessonId)
    {
        var ct = Context.ConnectionAborted;
        try
        {
            await Clients.Caller.SendAsync("LessonContentLoading", new { lessonId }, ct);

            var lessonResult = await _sender.Send(new GenerateLessonContentCommand(lessonId), ct);

            if (!lessonResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("LessonContentError", new
                {
                    LessonId = lessonId,
                    lessonResult.ErrorCode,
                    lessonResult.ErrorMessage
                }, ct);
                return;
            }

            await Clients.Caller.SendAsync("ReceiveLessonContent", lessonResult.Value, ct);

            await Clients.Caller.SendAsync("QuizSkeletonLoading", new { lessonId }, ct);

            var quizResult = await _sender.Send(new GenerateQuizSkeletonCommand(lessonId), ct);

            if (quizResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("ReceiveQuizSkeleton", new
                {
                    LessonId = lessonId,
                    Quizzes = quizResult.Value.Quizzes
                }, ct);
            }
            else
            {
                await Clients.Caller.SendAsync("QuizSkeletonError", new
                {
                    LessonId = lessonId,
                    quizResult.ErrorCode,
                    quizResult.ErrorMessage
                }, ct);
            }

            await Clients.Caller.SendAsync("LessonGenerationCompleted", new
            {
                LessonId = lessonId,
                Message = "Lesson content and quizzes generated successfully!"
            }, ct);

            await SendWalletTokenBalanceUpdatedAsync();
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

    [Authorize(Roles = "Mentor")]
    public async Task RequestMentorLessonContent(Guid lessonId)
    {
        var ct = Context.ConnectionAborted;
        try
        {
            await Clients.Caller.SendAsync("LessonContentLoading", new { lessonId }, ct);

            var lessonResult = await _sender.Send(new GenerateLessonContentCommand(lessonId), ct);

            if (!lessonResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("LessonContentError", new
                {
                    LessonId = lessonId,
                    lessonResult.ErrorCode,
                    lessonResult.ErrorMessage
                }, ct);
                return;
            }

            await Clients.Caller.SendAsync("ReceiveLessonContent", lessonResult.Value, ct);

            await Clients.Caller.SendAsync("LessonGenerationCompleted", new
            {
                LessonId = lessonId,
                Message = "Lesson content generated successfully!"
            }, ct);
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
        var ct = Context.ConnectionAborted;
        try
        {
            await Clients.Caller.SendAsync("QuizSkeletonLoading", new { lessonId }, ct);

            var quizResult = await _sender.Send(new GenerateQuizSkeletonCommand(lessonId), ct);

            if (quizResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("ReceiveQuizSkeleton", new
                {
                    LessonId = lessonId,
                    Quizzes = quizResult.Value.Quizzes
                }, ct);
                await SendWalletTokenBalanceUpdatedAsync();

                return;
            }

            await Clients.Caller.SendAsync("QuizSkeletonError", new
            {
                LessonId = lessonId,
                quizResult.ErrorCode,
                quizResult.ErrorMessage
            }, ct);
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
        var ct = Context.ConnectionAborted;
        try
        {
            await Clients.Caller.SendAsync("SingleQuizSkeletonLoading", new { lessonId }, ct);

            var quizResult = await _sender.Send(new GenerateSingleQuizSkeletonCommand(lessonId), ct);

            if (quizResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("ReceiveSingleQuizSkeleton", new
                {
                    LessonId = lessonId,
                    Quiz = quizResult.Value
                }, ct);
                return;
            }

            await Clients.Caller.SendAsync("SingleQuizSkeletonError", new
            {
                LessonId = lessonId,
                quizResult.ErrorCode,
                quizResult.ErrorMessage
            }, ct);
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

    public async Task RequestBulkLearningPathContent(Guid pathId, int lessonConcurrency = 4, int quizConcurrency = 6)
    {
        var ct = Context.ConnectionAborted;
        lessonConcurrency = Math.Clamp(lessonConcurrency, 1, 8);
        quizConcurrency = Math.Clamp(quizConcurrency, 1, 10);

        try
        {
            var workItemsResult = await _sender.Send(new GetLearningPathGenerationWorkItemsQuery(pathId), ct);
            if (!workItemsResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("BulkLearningPathGenerationError", new
                {
                    PathId = pathId,
                    workItemsResult.ErrorCode,
                    workItemsResult.ErrorMessage
                }, ct);
                return;
            }

            var workItems = workItemsResult.Value;
            var pendingLessonIds = workItems.PendingLessonIds.Distinct().ToList();
            var pendingQuizzesByLesson = workItems.PendingQuizzesByLesson
                .ToDictionary(x => x.LessonId, x => x.QuizIds.Distinct().ToList());

            var pendingLessonSet = pendingLessonIds.ToHashSet();
            var pendingQuizCount = pendingQuizzesByLesson.Sum(x => x.Value.Count);

            var budgetEstimateResult = await _sender.Send(
                new EstimateBulkLearningPathGenerationBudgetQuery(pendingLessonIds.Count, pendingQuizCount),
                ct);
            if (!budgetEstimateResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("BulkLearningPathGenerationError", new
                {
                    PathId = pathId,
                    budgetEstimateResult.ErrorCode,
                    budgetEstimateResult.ErrorMessage
                }, ct);
                return;
            }

            var budgetEstimate = budgetEstimateResult.Value;
            if (budgetEstimate.IsValidationApplied && !budgetEstimate.IsEnoughTokenBalance)
            {
                await Clients.Caller.SendAsync("BulkLearningPathGenerationError", new
                {
                    PathId = pathId,
                    ErrorCode = "INSUFFICIENT_TOKEN_BALANCE",
                    ErrorMessage =
                        $"Insufficient token balance to generate full learning path content. Required about {budgetEstimate.EstimatedRequiredTokens:0} tokens, current balance {budgetEstimate.CurrentTokenBalance:0}.",
                    budgetEstimate.EstimatedRequiredTokens,
                    budgetEstimate.CurrentTokenBalance,
                    budgetEstimate.PendingLessonCount,
                    budgetEstimate.PendingQuizCount,
                    budgetEstimate.EstimatedAiCalls
                }, ct);
                await SendWalletTokenBalanceUpdatedAsync();
                return;
            }

            await Clients.Caller.SendAsync("BulkLearningPathGenerationStarted", new
            {
                PathId = pathId,
                TotalLessons = pendingLessonIds.Count,
                TotalQuizzes = pendingQuizCount,
                EstimatedRequiredTokens = budgetEstimate.EstimatedRequiredTokens,
                CurrentTokenBalance = budgetEstimate.CurrentTokenBalance,
                LessonConcurrency = lessonConcurrency,
                QuizConcurrency = quizConcurrency
            }, ct);

            if (pendingLessonIds.Count == 0 && pendingQuizCount == 0)
            {
                await Clients.Caller.SendAsync("BulkLearningPathGenerationCompleted", new
                {
                    PathId = pathId,
                    TotalLessons = 0,
                    CompletedLessons = 0,
                    FailedLessons = 0,
                    TotalQuizzes = 0,
                    CompletedQuizzes = 0,
                    FailedQuizzes = 0
                }, ct);
                return;
            }

            var lessonChannel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = true
            });
            var quizChannel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });

            foreach (var lessonId in pendingLessonIds)
            {
                await lessonChannel.Writer.WriteAsync(lessonId, ct);
            }
            lessonChannel.Writer.Complete();

            foreach (var kv in pendingQuizzesByLesson)
            {
                if (pendingLessonSet.Contains(kv.Key))
                {
                    continue;
                }

                foreach (var quizId in kv.Value)
                {
                    await quizChannel.Writer.WriteAsync(quizId, ct);
                }
            }

            var completedLessons = 0;
            var failedLessons = 0;
            var completedQuizzes = 0;
            var failedQuizzes = 0;
            var progressLock = new object();

            Task SendProgressAsync()
            {
                int cl;
                int fl;
                int cq;
                int fq;

                lock (progressLock)
                {
                    cl = completedLessons;
                    fl = failedLessons;
                    cq = completedQuizzes;
                    fq = failedQuizzes;
                }

                return Clients.Caller.SendAsync("BulkLearningPathGenerationProgress", new
                {
                    PathId = pathId,
                    TotalLessons = pendingLessonIds.Count,
                    CompletedLessons = cl,
                    FailedLessons = fl,
                    TotalQuizzes = pendingQuizCount,
                    CompletedQuizzes = cq,
                    FailedQuizzes = fq
                }, ct);
            }

            var lessonWorkers = Enumerable.Range(0, lessonConcurrency)
                .Select(_ => Task.Run(async () =>
                {
                    await foreach (var lessonId in lessonChannel.Reader.ReadAllAsync(ct))
                    {
                        var lessonResult = await SendIsolatedAsync(new GenerateLessonContentCommand(lessonId), ct);
                        if (lessonResult.IsSuccess)
                        {
                            lock (progressLock)
                            {
                                completedLessons++;
                            }

                            await Clients.Caller.SendAsync("ReceiveLessonContent", lessonResult.Value, ct);
                        }
                        else
                        {
                            lock (progressLock)
                            {
                                failedLessons++;
                            }

                            await Clients.Caller.SendAsync("LessonContentError", new
                            {
                                LessonId = lessonId,
                                lessonResult.ErrorCode,
                                lessonResult.ErrorMessage
                            }, ct);
                        }

                        if (pendingQuizzesByLesson.TryGetValue(lessonId, out var quizIds))
                        {
                            foreach (var quizId in quizIds)
                            {
                                await quizChannel.Writer.WriteAsync(quizId, ct);
                            }
                        }

                        await SendProgressAsync();
                    }
                }, ct))
                .ToList();

            var quizWorkers = Enumerable.Range(0, quizConcurrency)
                .Select(_ => Task.Run(async () =>
                {
                    await foreach (var quizId in quizChannel.Reader.ReadAllAsync(ct))
                    {
                        var quizResult = await SendIsolatedAsync(new GenerateQuizQuestionsCommand(quizId), ct);
                        if (quizResult.IsSuccess)
                        {
                            lock (progressLock)
                            {
                                completedQuizzes++;
                            }

                            await Clients.Caller.SendAsync("ReceiveQuizQuestions", new
                            {
                                QuizId = quizId,
                                Questions = quizResult.Value
                            }, ct);
                        }
                        else
                        {
                            lock (progressLock)
                            {
                                failedQuizzes++;
                            }

                            await Clients.Caller.SendAsync("QuizQuestionsError", new
                            {
                                QuizId = quizId,
                                quizResult.ErrorCode,
                                quizResult.ErrorMessage
                            }, ct);
                        }

                        await SendProgressAsync();
                    }
                }, ct))
                .ToList();

            await Task.WhenAll(lessonWorkers);
            quizChannel.Writer.Complete();
            await Task.WhenAll(quizWorkers);

            await Clients.Caller.SendAsync("BulkLearningPathGenerationCompleted", new
            {
                PathId = pathId,
                TotalLessons = pendingLessonIds.Count,
                CompletedLessons = completedLessons,
                FailedLessons = failedLessons,
                TotalQuizzes = pendingQuizCount,
                CompletedQuizzes = completedQuizzes,
                FailedQuizzes = failedQuizzes
            }, ct);

            await SendWalletTokenBalanceUpdatedAsync();
        }
        catch (OperationCanceledException)
        {
            await Clients.Caller.SendAsync("BulkLearningPathGenerationCancelled", new
            {
                PathId = pathId,
                ErrorCode = "CONNECTION_ABORTED",
                ErrorMessage = "Connection was closed while generating learning path content."
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("BulkLearningPathGenerationError", new
            {
                PathId = pathId,
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
        }
    }

    private async Task<Result<T>> SendIsolatedAsync<T>(IRequest<Result<T>> request, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request, cancellationToken);
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
