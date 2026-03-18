using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.FocusSessions.Commands.StartSession;

public class StartSessionCommandHandler : IRequestHandler<StartSessionCommand, Result<StartSessionResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public StartSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<StartSessionResponseDto>> Handle(StartSessionCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.TaskId == request.TaskId, cancellationToken);

        if (task == null)
        {
            return Result<StartSessionResponseDto>.Failure(
                "TASK_NOT_FOUND",
                "Task not found");
        }

        var activeSession = await _context.FocusSessions
            .FirstOrDefaultAsync(fs => fs.TaskId == request.TaskId &&
                                      (fs.SessionStatus == SessionStatus.Running ||
                                       fs.SessionStatus == SessionStatus.Paused),
                                cancellationToken);

        if (activeSession != null)
        {
            return Result<StartSessionResponseDto>.Failure(
                "SESSION_ALREADY_ACTIVE",
                "There is already an active session for this task");
        }

        int plannedDuration;
        if (request.SessionType == SessionType.Pomodoro)
        {
            var duration = request.PlannedDurationMinutes ?? 25;
            if (duration < 5 || duration > 120)
            {
                return Result<StartSessionResponseDto>.Failure(
                    "INVALID_DURATION",
                    "Pomodoro session duration must be between 5 and 120 minutes");
            }
            plannedDuration = duration;
        }
        else
        {
            plannedDuration = request.PlannedDurationMinutes ?? 0;
            if (plannedDuration < 0 || plannedDuration > 480)
            {
                return Result<StartSessionResponseDto>.Failure(
                    "INVALID_DURATION",
                    "Study session duration must be 0 (unlimited) or between 1 and 480 minutes");
            }
        }

        try
        {
            var focusSession = new FocusSession
            {
                SessionId = NewId.NextGuid(),
                TaskId = request.TaskId,
                Title = request.Title ?? $"{(request.SessionType == SessionType.Pomodoro ? "Pomodoro" : "Study")} Session - {task.Title}",
                StartTime = DateTime.UtcNow,
                PlannedDurationMinutes = plannedDuration,
                SessionStatus = SessionStatus.Running,
                SessionType = request.SessionType,
                CreatedAt = DateTime.UtcNow
            };

            _context.FocusSessions.Add(focusSession);

            if (task.Status == TaskStatus_.Pending)
            {
                task.Status = TaskStatus_.InProgress;
            }

            await _context.SaveChangesAsync(cancellationToken);

            var responseDto = new StartSessionResponseDto(
                focusSession.SessionId,
                focusSession.StartTime,
                focusSession.PlannedDurationMinutes,
                $"{(request.SessionType == SessionType.Pomodoro ? "Pomodoro" : "Study")} session started successfully",
                focusSession.SessionType,
                focusSession.SessionStatus,
                focusSession.Title
            );

            return Result<StartSessionResponseDto>.Success(responseDto);
        }
        catch (Exception ex)
        {
            return Result<StartSessionResponseDto>.Failure(
                "START_SESSION_FAILED",
                $"An error occurred while starting the session: {ex.Message}");
        }
    }
}
