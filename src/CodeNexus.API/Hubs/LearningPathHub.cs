using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateGoalSupplementLearningPath;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.AdoptSuggestedLearningPath;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathSuggestions;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathSuggestionPreview;
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

    public async Task RequestLearningPathSuggestionPreview(
        Guid suggestedPathId,
        Guid subjectId,
        List<CodeNexus.Application.Features.LearningPaths.DTOs.LearningPathGoalRequest> goals,
        string complexityLevel,
        string languageSelection)
    {
        try
        {
            await Clients.Caller.SendAsync("LearningPathSuggestionPreviewStarted");

            if (!Enum.TryParse<Domain.Enums.ComplexityLevel>(complexityLevel, out var complexity))
            {
                await Clients.Caller.SendAsync("LearningPathSuggestionPreviewError", new
                {
                    ErrorCode = "INVALID_COMPLEXITY",
                    ErrorMessage = "Invalid complexity level"
                });
                return;
            }

            if (!Enum.TryParse<Domain.Enums.LanguageSelection>(languageSelection, out var language))
            {
                await Clients.Caller.SendAsync("LearningPathSuggestionPreviewError", new
                {
                    ErrorCode = "INVALID_LANGUAGE",
                    ErrorMessage = "Invalid language selection"
                });
                return;
            }

            var query = new GetLearningPathSuggestionPreviewQuery(
                suggestedPathId,
                subjectId,
                goals,
                complexity,
                language);

            var result = await _sender.Send(query);
            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("LearningPathSuggestionPreviewError", new
                {
                    result.ErrorCode,
                    result.ErrorMessage
                });
                return;
            }

            await Clients.Caller.SendAsync("LearningPathSuggestionPreviewLoaded", result.Value);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("LearningPathSuggestionPreviewError", new
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

    public async Task RequestGoalSupplementLearningPath(
        Guid sourcePathId,
        Guid goalId,
        string? complexityLevel = null,
        string? languageSelection = null,
        bool saveAsDraft = false)
    {
        try
        {
            await Clients.Caller.SendAsync("GoalSupplementLearningPathGenerationStarted", new
            {
                SourcePathId = sourcePathId,
                GoalId = goalId
            });

            Domain.Enums.ComplexityLevel? complexity = null;
            if (!string.IsNullOrWhiteSpace(complexityLevel))
            {
                if (!Enum.TryParse<Domain.Enums.ComplexityLevel>(complexityLevel, out var parsedComplexity))
                {
                    await Clients.Caller.SendAsync("GoalSupplementLearningPathGenerationError", new
                    {
                        ErrorCode = "INVALID_COMPLEXITY",
                        ErrorMessage = "Invalid complexity level"
                    });
                    return;
                }

                complexity = parsedComplexity;
            }

            Domain.Enums.LanguageSelection? language = null;
            if (!string.IsNullOrWhiteSpace(languageSelection))
            {
                if (!Enum.TryParse<Domain.Enums.LanguageSelection>(languageSelection, out var parsedLanguage))
                {
                    await Clients.Caller.SendAsync("GoalSupplementLearningPathGenerationError", new
                    {
                        ErrorCode = "INVALID_LANGUAGE",
                        ErrorMessage = "Invalid language selection"
                    });
                    return;
                }

                language = parsedLanguage;
            }

            var command = new GenerateGoalSupplementLearningPathCommand(
                sourcePathId,
                goalId,
                complexity,
                language,
                saveAsDraft);

            var result = await _sender.Send(command);
            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("GoalSupplementLearningPathGenerationError", new
                {
                    result.ErrorCode,
                    result.ErrorMessage
                });
                return;
            }

            await Clients.Caller.SendAsync("GoalSupplementLearningPathCreated", result.Value);
            await Clients.Caller.SendAsync("GoalSupplementLearningPathGenerationCompleted", new
            {
                result.Value!.SourcePathId,
                result.Value.GoalId,
                result.Value.CurrentProgressPercent,
                result.Value.RemainingPercent,
                result.Value.LearningPath.PathId,
                Message = "Supplement learning path generated successfully."
            });

            await SendWalletTokenBalanceUpdatedAsync();
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("GoalSupplementLearningPathGenerationError", new
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
