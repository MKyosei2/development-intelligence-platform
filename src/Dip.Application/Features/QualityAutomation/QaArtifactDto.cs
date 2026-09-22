// Purpose: Defines the QaArtifactDto application contract.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record QaArtifactDto(string RelativePath, long Length, string Kind);
