namespace CodeNexus.Application.Features.Chapters.DTOs;

public record ChapterContentDto(
    Guid ChapterId,
    string Title,
    string Content
);
