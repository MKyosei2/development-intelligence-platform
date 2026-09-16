// Purpose: Defines the DurableJobDto application contract.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Jobs;

public sealed record DurableJobDto(Guid Id,Guid ProjectId,string Type,string Status,string SubjectKey,int Attempt,int MaxAttempts,decimal ProgressPercent,string? CurrentStage,DateTimeOffset CreatedAtUtc,DateTimeOffset? StartedAtUtc,DateTimeOffset? FinishedAtUtc,bool CancellationRequested,string? FailureCode,string? FailureMessage);
