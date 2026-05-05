using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Users.Commands.CreateMentorAccount;

public record CreateMentorAccountCommand(
    string Email,
    string? Username,
    string? FirstName,
    string? LastName,
    string? Bio,
    string? Phone,
    string? Address,
    DateTime? DateOfBirth,
    string Role,
    bool SendSetupEmail = true
) : IRequest<Result<CreateMentorAccountResponse>>;
