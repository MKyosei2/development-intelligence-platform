// Purpose: Defines the QaCrashClusterDto application contract.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record QaCrashClusterDto(string Fingerprint,string Category,string Severity,string Title,int Occurrences,DateTimeOffset FirstSeenAtUtc,DateTimeOffset LastSeenAtUtc,int ReproducedOccurrences,IReadOnlyList<string> Subjects);
