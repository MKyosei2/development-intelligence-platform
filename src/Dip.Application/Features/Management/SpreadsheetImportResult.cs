// Purpose: Defines the SpreadsheetImportResult application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record SpreadsheetImportResult(int Teams,int Workstreams,int Tasks,int Milestones,int Blockers,int Requirements,int Risks,int Decisions,int Actions,int ChangeRequests,int ResourceAllocations,IReadOnlyList<string> Warnings);
