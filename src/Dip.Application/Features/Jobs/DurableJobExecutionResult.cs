// Purpose: Defines the DurableJobExecutionResult application contract.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Jobs;

public sealed record DurableJobExecutionResult(string? ResultJson,string? ArtifactReference=null);
