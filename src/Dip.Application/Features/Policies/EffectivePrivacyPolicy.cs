// Purpose: Defines EffectivePrivacyPolicy.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record EffectivePrivacyPolicy(bool AllowScreenshots, bool AllowVideo, bool HashOnly, IReadOnlyList<string> ExcludedPaths);
