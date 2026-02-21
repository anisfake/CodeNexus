using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Users.Queries.GetUserById;

public record GetUserByIdQuery(Guid UserId) : IRequest<Result<UserRespone>>;
