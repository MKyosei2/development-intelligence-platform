// Purpose: Defines the IDurableJobHandler application boundary.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Jobs;

public interface IDurableJobHandler
{
    DurableJobType Type{get;}
    Task<DurableJobExecutionResult> ExecuteAsync(DurableJob job,CancellationToken cancellationToken);
}
