using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subjects.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subjects.Commands.UpdateSubject;

public class UpdateSubjectCommandHandler : IRequestHandler<UpdateSubjectCommand, Result<SubjectDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateSubjectCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SubjectDto>> Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId, cancellationToken);

        if (subject == null)
        {
            return Result<SubjectDto>.Failure("SUBJECT_NOT_FOUND", "Subject not found.");
        }

        if (subject.CreatedByUserId != userId)
        {
            return Result<SubjectDto>.Failure("UNAUTHORIZED", "You can only update subjects you created.");
        }

        var duplicateSubject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.Name.ToLower() == request.Name.ToLower() 
                && s.SubjectId != request.SubjectId, cancellationToken);

        if (duplicateSubject != null)
        {
            return Result<SubjectDto>.Failure("SUBJECT_EXISTS", "A subject with this name already exists.");
        }

        subject.Name = request.Name;
        subject.Description = request.Description;
        subject.Color = request.Color;
        subject.Icon = request.Icon;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<SubjectDto>.Failure("UPDATE_SUBJECT_FAILED", $"An error occurred while updating the subject: {ex.Message}");
        }

        var subjectDto = new SubjectDto(
            subject.SubjectId,
            subject.Name,
            subject.Description,
            subject.Color,
            subject.Icon,
            subject.CreatedAt
        );

        return Result<SubjectDto>.Success(subjectDto);
    }
}
