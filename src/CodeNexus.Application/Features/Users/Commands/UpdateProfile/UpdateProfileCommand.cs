using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Users.Commands.UpdateProfile;

public record UpdateProfileCommand(
    string? FirstName,
    string? LastName,
    string? Bio,
    DateTime? DateOfBirth,
    string? Phone,
    string? Address
) : IRequest<Result<UserProfileRespone>>;



