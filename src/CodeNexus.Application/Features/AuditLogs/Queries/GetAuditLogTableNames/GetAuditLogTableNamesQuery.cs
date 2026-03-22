using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.AuditLogs.Queries.GetAuditLogTableNames;

public record GetAuditLogTableNamesQuery : IRequest<Result<List<string>>>;
