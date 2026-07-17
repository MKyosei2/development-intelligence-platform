using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class QualityIssueRepository : IQualityIssueRepository
{
    private readonly DipDbContext _db;

    public QualityIssueRepository(DipDbContext db) => _db = db;

    public Task<QualityIssue?> FindOpenByFingerprintAsync(
        Guid projectId,
        string fingerprint,
        CancellationToken cancellationToken) =>
        _db.QualityIssues.SingleOrDefaultAsync(
            x => x.ProjectId == projectId
                 && x.Fingerprint == fingerprint
                 && x.Status == IssueStatus.Open,
            cancellationToken);

    public Task AddAsync(
        QualityIssue issue,
        CancellationToken cancellationToken) =>
        _db.QualityIssues.AddAsync(issue, cancellationToken).AsTask();
}
