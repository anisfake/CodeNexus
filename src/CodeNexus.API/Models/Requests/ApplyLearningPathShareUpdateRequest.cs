using CodeNexus.Application.Features.LearningPathShares.DTOs;

namespace CodeNexus.API.Models.Requests;

public record ApplyLearningPathShareUpdateRequest(
    LearningPathShareUpdateAction Action
);
