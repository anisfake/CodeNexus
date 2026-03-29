using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public record MarkNotificationAsReadCommand(Guid NotificationId) : IRequest<Result>;
