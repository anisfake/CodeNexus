using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Chapters.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Chapters.Commands.GenerateChapterContent;

public record GenerateChapterContentCommand(Guid ChapterId) : IRequest<Result<ChapterContentDto>>;
