using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notifications.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public record MarkNotificationAsReadCommand(Guid NotificationId) : IRequest<Result<MarkNotificationAsReadResultDto>>;
