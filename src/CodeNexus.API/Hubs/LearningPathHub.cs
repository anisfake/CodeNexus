using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.AdoptSuggestedLearningPath;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathSuggestions;
using CodeNexus.Application.Features.Users.Queries.GetMyProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;

namespace CodeNexus.API.Hubs;

[Authorize]
public class LearningPathHub : Hub
{
    private readonly ISender _sender;

    public LearningPathHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task RequestLearningPathGeneration(
        Guid subjectId,
        List<CodeNexus.Application.Features.LearningPaths.DTOs.LearningPathGoalRequest> goals,
        string complexityLevel,
        string languageSelection)
    {
        try
        {
            await Clients.Caller.SendAsync("LearningPathGenerationStarted");

            if (!Enum.TryParse<Domain.Enums.ComplexityLevel>(complexityLevel, out var complexity))
            {
                await Clients.Caller.SendAsync("LearningPathGenerationError", new
                {
                    ErrorCode = "INVALID_COMPLEXITY",
                    ErrorMessage = "Invalid complexity level"
                });
                return;
            }

            if (!Enum.TryParse<Domain.Enums.LanguageSelection>(languageSelection, out var language))
            {
                await Clients.Caller.SendAsync("LearningPathGenerationError", new
                {
                    ErrorCode = "INVALID_LANGUAGE",
                    ErrorMessage = "Invalid language selection"
                });
                return;
            }

            var command = new GenerateLearningPathSkeletonCommand(subjectId, goals, complexity, language);
            var result = await _sender.Send(command);

            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("LearningPathGenerationError", new
                {
                    result.ErrorCode,
                    result.ErrorMessage
                });
                return;
            }

            var learningPath = result.Value;
            await Clients.Caller.SendAsync("LearningPathCreated", new
            {
                learningPath.PathId,
                learningPath.Title,
                learningPath.Description,
                learningPath.Goals,
                learningPath.ChapterCount,
                learningPath.ChapterDtos,
                Message = "Learning path with chapters and lessons created successfully!"
            });

            await Clients.Caller.SendAsync("LearningPathGenerationCompleted", new
            {
                learningPath.PathId,
                Message = "Learning path with chapters and lessons created successfully! Click on lessons to generate content."
            });

            await SendWalletTokenBalanceUpdatedAsync();
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("LearningPathGenerationError", new
            {
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
        }
    }

    public async Task RequestLearningPathSuggestions(
        Guid subjectId,
        List<CodeNexus.Application.Features.LearningPaths.DTOs.LearningPathGoalRequest> goals,
        string complexityLevel,
        string languageSelection)
    {
        try
        {
            await Clients.Caller.SendAsync("LearningPathSuggestionsStarted");

            if (!Enum.TryParse<Domain.Enums.ComplexityLevel>(complexityLevel, out var complexity))
            {
                await Clients.Caller.SendAsync("LearningPathSuggestionsError", new
                {
                    ErrorCode = "INVALID_COMPLEXITY",
                    ErrorMessage = "Invalid complexity level"
                });
                return;
            }

            if (!Enum.TryParse<Domain.Enums.LanguageSelection>(languageSelection, out var language))
            {
                await Clients.Caller.SendAsync("LearningPathSuggestionsError", new
                {
                    ErrorCode = "INVALID_LANGUAGE",
                    ErrorMessage = "Invalid language selection"
                });
                return;
            }

            var query = new GetLearningPathSuggestionsQuery(subjectId, goals, complexity, language);
            var result = await _sender.Send(query);

            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("LearningPathSuggestionsError", new
                {
                    result.ErrorCode,
                    result.ErrorMessage
                });
                return;
            }

            await Clients.Caller.SendAsync("LearningPathSuggestionsLoaded", new
            {
                Suggestions = result.Value
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("LearningPathSuggestionsError", new
            {
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            });
        }
    }

    public async Task RequestAdoptSuggestedLearningPath(
        Guid suggestedPathId,
        Guid subjectId,
        List<CodeNexus.Application.Features.LearningPaths.DTOs.LearningPathGoalRequest> goals,
        string complexityLevel,
        string languageSelection)
    {
        try
        {
            await Clients.Caller.SendAsync("AdoptSuggestedLearningPathStarted");

            if (!Enum.TryParse<Domain.Enums.ComplexityLevel>(complexityLevel, out var complexity))
            {
                await Clients.Caller.SendAsync("AdoptSuggestedLearningPathError", new
                {
                    ErrorCode = "INVALID_COMPLEXITY",
                    ErrorMessage = "Invalid complexity level"
                });
                return;
            }

            if (!Enum.TryParse<Domain.Enums.LanguageSelection>(languageSelection, out var language))
            {
                await Clients.Caller.SendAsync("AdoptSuggestedLearningPathError", new
                {
                    ErrorCode = "INVALID_LANGUAGE",
                    ErrorMessage = "Invalid language selection"
                });
                return;
            }

            var command = new AdoptSuggestedLearningPathCommand(
                suggestedPathId,
                subjectId,
                goals,
                complexity,
                language);

            var result = await _sender.Send(command);
            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("AdoptSuggestedLearningPathError", new
                {
                    result.ErrorCode,
                    result.ErrorMessage
                });
                return;
            }

            var learningPath = result.Value;
            await Clients.Caller.SendAsync("SuggestedLearningPathAdopted", new
            {
                learningPath.PathId,
                learningPath.Title,
                learningPath.Description,
                learningPath.Goals,
                learningPath.ChapterCount,
                learningPath.ChapterDtos
            });

            await SendWalletTokenBalanceUpdatedAsync();
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("AdoptSuggestedLearningPathError", new
            {
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
