using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AISummaries.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AISummaries.Commands.GenerateResourceSummary;

public class GenerateResourceSummaryCommandHandler
    : IRequestHandler<GenerateResourceSummaryCommand, Result<ResourceSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateResourceSummaryCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<ResourceSummaryDto>> Handle(
        GenerateResourceSummaryCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var resource = await _context.Resources
            .Include(r => r.Subject)
            .Include(r => r.Pages)
            .FirstOrDefaultAsync(r => r.ResourceId == request.ResourceId && !r.IsDeleted, cancellationToken);

        if (resource == null)
            return Result<ResourceSummaryDto>.Failure("RESOURCE_NOT_FOUND", "Resource not found.");

        if (resource.UserId != userId)
            return Result<ResourceSummaryDto>.Failure("UNAUTHORIZED", "User not authenticated");

        var existingSummary = await _context.AISummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.ResourceId == request.ResourceId &&
                !s.IsDeleted &&
                s.StartPage == request.StartPage &&
                s.EndPage == request.EndPage, cancellationToken);

        if (existingSummary != null)
        {
            return Result<ResourceSummaryDto>.Success(new ResourceSummaryDto(
                existingSummary.SummaryId,
                existingSummary.ResourceId,
                existingSummary.Title,
                existingSummary.Summary!,
                existingSummary.StartPage,
                existingSummary.EndPage));
        }

        var pages = resource.Pages
            .Where(p => p.PageNumber >= request.StartPage && p.PageNumber <= request.EndPage)
            .OrderBy(p => p.PageNumber)
            .ToList();

        if (pages.Count == 0)
            return Result<ResourceSummaryDto>.Failure("PAGES_NOT_FOUND",
                $"No pages found in the range {request.StartPage}-{request.EndPage}.");

        var pagesWithText = pages.Where(p => !string.IsNullOrWhiteSpace(p.ExtractedText)).ToList();
        if (pagesWithText.Count == 0)
            return Result<ResourceSummaryDto>.Failure("NO_TEXT_CONTENT",
                "The selected pages do not contain any extracted text.");

        try
        {
            var prompt = BuildPrompt(resource, pagesWithText, request.StartPage, request.EndPage);
            var summaryContent = await _aiGeneratorService.GenerateContentAsync(prompt, AIUsageType.Assistant);

            var summaryTitle = $"{resource.Title} - Pages {request.StartPage}-{request.EndPage}";

            var aiSummary = new AISummary
            {
                SummaryId = NewId.NextGuid(),
                ResourceId = resource.ResourceId,
                Title = summaryTitle,
                Summary = summaryContent,
                StartPage = request.StartPage,
                EndPage = request.EndPage,
                GeneratedAt = DateTime.UtcNow
            };

            _context.AISummaries.Add(aiSummary);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<ResourceSummaryDto>.Success(new ResourceSummaryDto(
                aiSummary.SummaryId,
                aiSummary.ResourceId,
                aiSummary.Title,
                summaryContent,
                aiSummary.StartPage,
                aiSummary.EndPage));
        }
        catch (Exception ex)
        {
            return Result<ResourceSummaryDto>.Failure("SUMMARY_GENERATION_FAILED",
                $"Failed to generate summary: {ex.Message}");
        }
    }

	private static string BuildPrompt(Resource resource, List<ResourcePage> pages, int startPage, int endPage)
	{
		var pageContents = string.Join("\n\n", pages.Select(p =>
			$"--- Page {p.PageNumber} ---\n{p.ExtractedText}"));

		var subjectContext = !string.IsNullOrWhiteSpace(resource.Subject?.Description)
			? $"Subject: {resource.Subject.Name}\nSubject Description: {resource.Subject.Description}"
			: $"Subject: {resource.Subject?.Name}";

		return $@"You are an expert academic summarizer.
=== CONTEXT ===
Resource Title: {resource.Title}
{subjectContext}
Pages: {startPage} to {endPage}
=== PAGE CONTENTS ===
{pageContents}
=== INSTRUCTIONS ===
- Summarize the content from the pages above in a clear, concise, and well-structured manner.
- Use the resource title and subject description to provide context-aware summarization.
- Highlight key concepts, definitions, and important points.
- Use bullet points or numbered lists where appropriate.
- Use Markdown formatting for readability.
- Keep the summary focused and avoid unnecessary repetition.
- If the content is technical, preserve important technical terms and formulas.
- If the content contains code snippets, preserve them inside proper ```code``` blocks and explain what each snippet does.
- Do not modify, refactor, or optimize any code found in the original content.
- Detect the primary language of the page content (Vietnamese or English) and write the entire summary in that language.
- If the content is in English, the summary must be written entirely in English.
- If the content is in Vietnamese, the summary must be written entirely in Vietnamese.
Generate the summary now:";
	}
}
