using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Chapters.DTOs;
using MediatR;
using System;

namespace CodeNexus.Application.Features.Chapters.Queries.GetChapterCompleteStatus;

public record GetChapterCompleteStatusQuery(Guid ChapterId) : IRequest<Result<ChapterCompleteStatusDto>>;
