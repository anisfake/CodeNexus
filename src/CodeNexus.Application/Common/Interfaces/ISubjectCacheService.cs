using CodeNexus.Application.Features.Subjects.DTOs;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Common.Interfaces;

public interface ISubjectCacheService
{
    Task<List<SubjectDto>?> GetSubjectsAsync(SubjectCategory? category, CancellationToken cancellationToken = default);
    Task SetSubjectsAsync(SubjectCategory? category, List<SubjectDto> subjects, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task InvalidateSubjectsAsync(CancellationToken cancellationToken = default);
}
