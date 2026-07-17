using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;
public sealed class QualityIssueRepository:IQualityIssueRepository
{
    private readonly DipDbContext _db;public QualityIssueRepository(DipDbContext db)=>_db=db;
    public Task<QualityIssue?> FindOpenByFingerprintAsync(Guid projectId,string fp,CancellationToken ct)=>
        _db.QualityIssues.SingleOrDefaultAsync(x=>x.ProjectId==projectId&&x.Fingerprint==fp&&
        x.Status!=IssueStatus.Resolved&&x.Status!=IssueStatus.Ignored&&x.Status!=IssueStatus.FalsePositive,ct);
    public Task AddAsync(QualityIssue issue,CancellationToken ct)=>_db.QualityIssues.AddAsync(issue,ct).AsTask();
}
