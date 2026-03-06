namespace CodeNexus.Application.Features.AISummaries.DTOs;

public record ResourceSummaryDto(
    Guid SummaryId,
    Guid ResourceId,
    string Title,
    string Summary,
    int StartPage,
    int EndPage
);
