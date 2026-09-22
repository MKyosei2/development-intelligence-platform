// Purpose: Defines the CreateQaPolicyCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record CreateQaPolicyCommand(Guid ProjectId, string Name, string Version, QaPolicyRules Rules);
