using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Chapters.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Chapters.Queries.GetChapterCompletionStatus;

public record GetChapterCompletionStatusQuery(Guid ChapterId) : IRequest<Result<ChapterCompletionStatusDto>>;
