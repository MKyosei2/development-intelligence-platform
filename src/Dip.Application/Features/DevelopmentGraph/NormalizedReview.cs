// Purpose: Defines NormalizedReview.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record NormalizedReview(string Reviewer,string State,DateTimeOffset? SubmittedAtUtc=null);
