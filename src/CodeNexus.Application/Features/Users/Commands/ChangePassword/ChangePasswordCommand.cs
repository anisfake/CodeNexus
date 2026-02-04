using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Users.Commands.ChangePassword;

public record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword
) : IRequest<Result>;
