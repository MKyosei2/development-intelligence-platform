// Purpose: Defines NormalizedCiRun.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record NormalizedCiRun(Guid ProjectId, ExternalCiProvider Provider, string RepositoryKey, string PipelineKey, string RunId, string Status, string? Revision, string? Branch, string? Url, DateTimeOffset ObservedAtUtc, int PassedTests = 0, int FailedTests = 0, int SkippedTests = 0, string MetadataJson = "{}");
