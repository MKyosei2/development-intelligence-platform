using Dip.Application.Abstractions.Persistence;using Dip.Domain.Entities;using Dip.Domain.Enums;using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;
public sealed class QualityIssueRepository(DipDbContext db):IQualityIssueRepository
{
 public Task<QualityIssue?> FindOpenByFingerprintAsync(Guid projectId,string fingerprint,CancellationToken ct)=>db.QualityIssues.FirstOrDefaultAsync(x=>x.ProjectId==projectId&&x.Fingerprint==fingerprint&&(x.Status==IssueStatus.Open||x.Status==IssueStatus.Reopened),ct);
 public Task AddAsync(QualityIssue issue,CancellationToken ct)=>db.QualityIssues.AddAsync(issue,ct).AsTask();
}
