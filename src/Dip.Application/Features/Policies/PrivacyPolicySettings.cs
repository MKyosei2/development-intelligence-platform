// Purpose: Defines the PrivacyPolicySettings application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record PrivacyPolicySettings(bool? AllowScreenshots=null,bool? AllowVideo=null,bool? HashOnly=null,IReadOnlyList<string>? ExcludedPaths=null);
