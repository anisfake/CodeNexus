using CodeNexus.Application.Features.Chapters.Commands.GenerateChapterContent;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateChapterMentorSkeleton;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateChapterSkeleton;
using CodeNexus.Application.Features.Users.Queries.GetMyProfile;
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

    public async Task RequestChapterSkeleton(Guid pathId, int orderIndex)
    {
        var ct = Context.ConnectionAborted;
        try
        {
            await Clients.Caller.SendAsync("ChapterSkeletonGenerationStarted", new { pathId, orderIndex }, ct);

            var command = new GenerateChapterSkeletonCommand(pathId, orderIndex);
            var result = await _sender.Send(command, ct);

            if (result.IsSuccess)
            {
                await Clients.Caller.SendAsync("ChapterSkeletonGenerated", result.Value, ct);

                await Clients.Caller.SendAsync("ChapterContentLoading", new { chapterId = result.Value.ChapterId }, ct);
                var contentResult = await _sender.Send(new GenerateChapterContentCommand(result.Value.ChapterId), ct);

                if (contentResult.IsSuccess)
                {
                    await Clients.Caller.SendAsync("ReceiveChapterContent", new
                    {
                        ChapterId = result.Value.ChapterId,
                        Content = contentResult.Value
                    }, ct);
                }
                else
                {
                    await Clients.Caller.SendAsync("ChapterContentError", new
                    {
                        ChapterId = result.Value.ChapterId,
                        contentResult.ErrorCode,
                        contentResult.ErrorMessage
                    }, ct);
                }
            }
            else
            {
                await Clients.Caller.SendAsync("ChapterSkeletonError", new
                {
                    PathId = pathId,
                    OrderIndex = orderIndex,
                    result.ErrorCode,
                    result.ErrorMessage
                }, ct);
            }
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ChapterSkeletonError", new
            {
                PathId = pathId,
                OrderIndex = orderIndex,
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
        }
    }

    public async Task RequestChapterContent(Guid chapterId)
    {
        var ct = Context.ConnectionAborted;
        await Clients.Caller.SendAsync("ChapterContentLoading", new { chapterId }, ct);

        var result = await _sender.Send(new GenerateChapterContentCommand(chapterId), ct);

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveChapterContent", new
            {
                ChapterId = chapterId,
                Content = result.Value
            }, ct);
        }
        else
        {
            await Clients.Caller.SendAsync("ChapterContentError", new
            {
                ChapterId = chapterId,
                result.ErrorCode,
                result.ErrorMessage
            }, ct);
        }
    }

    public async Task RequestChapterMentorSkeleton(Guid pathId, string chapterTitle, string? chapterDescription)
    {
        var ct = Context.ConnectionAborted;
        try
        {
            await Clients.Caller.SendAsync("ChapterMentorSkeletonGenerationStarted", new { pathId }, ct);

            if (string.IsNullOrWhiteSpace(chapterTitle))
            {
                await Clients.Caller.SendAsync("ChapterMentorSkeletonError", new
                {
                    PathId = pathId,
                    ErrorCode = "INVALID_CHAPTER_TITLE",
                    ErrorMessage = "Chapter title is required"
                }, ct);
                return;
            }

            var command = new GenerateChapterMentorSkeletonCommand(pathId, chapterTitle, chapterDescription);
            var result = await _sender.Send(command, ct);

            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("ChapterMentorSkeletonError", new
                {
                    PathId = pathId,
                    result.ErrorCode,
                    result.ErrorMessage
                }, ct);
                return;
            }

            await Clients.Caller.SendAsync("ChapterMentorSkeletonGenerated", result.Value, ct);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ChapterMentorSkeletonError", new
            {
                PathId = pathId,
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
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
