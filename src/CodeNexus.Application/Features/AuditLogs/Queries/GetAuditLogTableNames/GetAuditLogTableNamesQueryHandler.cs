using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AuditLogs.Queries.GetAuditLogTableNames;

public class GetAuditLogTableNamesQueryHandler : IRequestHandler<GetAuditLogTableNamesQuery, Result<List<string>>>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogTableNamesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<string>>> Handle(GetAuditLogTableNamesQuery request, CancellationToken cancellationToken)
    {
        var tableNames = await _context.AuditLogs
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.TableName))
            .Select(x => x.TableName!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return Result<List<string>>.Success(tableNames);
    }
}
