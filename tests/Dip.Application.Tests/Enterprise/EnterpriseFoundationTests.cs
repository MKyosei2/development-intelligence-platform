// Purpose: Verifies Enterprise Foundation behavior, edge cases, and invariants.
using Dip.Application.Features.Enterprise;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
namespace Dip.Application.Tests;
public sealed class EnterpriseFoundationTests
{
    [Fact] public void AdaptiveQa_SelectsVisualForScreenshot() { var svc = new AdaptiveQaRoutingService();
     var rows = svc.Select(EvidenceSourceType.Unity, EvidenceEventType.ScreenshotGenerated, "ui:main", null);
     Assert.Contains(rows, x => x.PackId == "visual-regression");
     }
    [Fact] public void AgentRelease_ClampsRollout() { var r = new AgentRelease(Guid.NewGuid(), Guid.NewGuid(), "1.2.3", AgentReleaseChannel.Stable, "https://example.invalid/a.zip", new string('a', 64), "sig", "1.0.0", 10, DateTimeOffset.UtcNow);
     r.Activate(200, DateTimeOffset.UtcNow);
     Assert.Equal(100, r.RolloutPercent);
     Assert.Equal(AgentRolloutState.Active, r.State);
     }
    [Fact] public void TenantBinding_RequiresIds() { Assert.Throws<ArgumentException>(() => new ProjectTenantBinding(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), null, DateTimeOffset.UtcNow)); }
}
