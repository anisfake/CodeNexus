using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DirectChats.Commands.CreateDirectConversation;

public class CreateDirectConversationCommandHandler : IRequestHandler<CreateDirectConversationCommand, Result<DirectConversationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateDirectConversationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<DirectConversationDto>> Handle(CreateDirectConversationCommand request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<DirectConversationDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        if (request.ParticipantId == currentUserId)
        {
            return Result<DirectConversationDto>.Failure("INVALID_PARTICIPANT", "Cannot create conversation with yourself.");
        }

        var users = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Where(u => u.UserId == currentUserId || u.UserId == request.ParticipantId)
            .ToListAsync(cancellationToken);

        if (users.Count != 2)
        {
            return Result<DirectConversationDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        var currentUser = users.First(u => u.UserId == currentUserId);
        var participant = users.First(u => u.UserId == request.ParticipantId);

        var currentRole = currentUser.Role?.RoleName;
        var participantRole = participant.Role?.RoleName;

        var isCurrentMentor = string.Equals(currentRole, "Mentor", StringComparison.OrdinalIgnoreCase);
        var isCurrentStudent = string.Equals(currentRole, "Student", StringComparison.OrdinalIgnoreCase);
        var isParticipantMentor = string.Equals(participantRole, "Mentor", StringComparison.OrdinalIgnoreCase);
        var isParticipantStudent = string.Equals(participantRole, "Student", StringComparison.OrdinalIgnoreCase);

        if (!((isCurrentMentor && isParticipantStudent) || (isCurrentStudent && isParticipantMentor)))
        {
            return Result<DirectConversationDto>.Failure("INVALID_PARTICIPANTS", "Conversation is only allowed between mentor and student.");
        }

        var mentorId = isCurrentMentor ? currentUserId : request.ParticipantId;
        var studentId = isCurrentStudent ? currentUserId : request.ParticipantId;

        var existingConversation = await _context.DirectConversations
            .AsNoTracking()
            .Include(c => c.Mentor)
            .Include(c => c.Student)
            .FirstOrDefaultAsync(c => c.ConversationType == ChatConversationType.Direct && c.MentorId == mentorId && c.StudentId == studentId, cancellationToken);

        if (existingConversation != null)
        {
            var unreadCount = await _context.DirectMessageReceipts
                .AsNoTracking()
                .CountAsync(r =>
                    r.UserId == currentUserId &&
                    !r.SeenAt.HasValue &&
                    r.Message.ConversationId == existingConversation.ConversationId,
                    cancellationToken);

            return Result<DirectConversationDto>.Success(new DirectConversationDto(
                existingConversation.ConversationId,
                existingConversation.MentorId!.Value,
                existingConversation.Mentor.Username,
                existingConversation.StudentId!.Value,
                existingConversation.Student.Username,
                existingConversation.LastMessagePreview,
                existingConversation.LastMessageAt,
                unreadCount
            ));
        }

        var conversation = new DirectConversation
        {
            ConversationId = NewId.NextGuid(),
            MentorId = mentorId,
            StudentId = studentId,
            ConversationType = ChatConversationType.Direct,
            CreatedAt = DateTime.UtcNow
        };

        _context.DirectConversations.Add(conversation);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<DirectConversationDto>.Success(new DirectConversationDto(
            conversation.ConversationId,
            mentorId,
            users.First(u => u.UserId == mentorId).Username,
            studentId,
            users.First(u => u.UserId == studentId).Username,
            null,
            null,
            0
        ));
    }
}
