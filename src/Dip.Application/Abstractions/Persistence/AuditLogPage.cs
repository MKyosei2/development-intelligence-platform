// Purpose: Defines AuditLogPage.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public sealed record AuditLogPage(IReadOnlyList<AuditLog> Items, DateTimeOffset? NextBeforeUtc, Guid? NextBeforeId);
