using CodeNexus.Application.Features.Chapters.Commands.GenerateChapterContent;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateChapterSkeleton;
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
        try
        {
            await Clients.Caller.SendAsync("ChapterSkeletonGenerationStarted", new { pathId, orderIndex });

            var command = new GenerateChapterSkeletonCommand(pathId, orderIndex);
            var result = await _sender.Send(command);

            if (result.IsSuccess)
            {
                await Clients.Caller.SendAsync("ChapterSkeletonGenerated", result.Value);

                await Clients.Caller.SendAsync("ChapterContentLoading", new { chapterId = result.Value.ChapterId });
                var contentResult = await _sender.Send(new GenerateChapterContentCommand(result.Value.ChapterId));

                if (contentResult.IsSuccess)
                {
                    await Clients.Caller.SendAsync("ReceiveChapterContent", contentResult.Value);
                }
                else
                {
                    await Clients.Caller.SendAsync("ChapterContentError", new
                    {
                        ChapterId = result.Value.ChapterId,
                        contentResult.ErrorCode,
                        contentResult.ErrorMessage
                    });
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
                });
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
