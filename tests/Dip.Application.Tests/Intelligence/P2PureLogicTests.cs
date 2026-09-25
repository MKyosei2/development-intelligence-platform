// Purpose: Defines P2 Pure Logic Tests responsibilities for this repository area.
using Dip.Application.Features.Intelligence;
using Xunit;
namespace Dip.Application.Tests.Intelligence;
public sealed class P2PureLogicTests
{
    [Fact] public void Contracts_keep_simulation_and_forecast_explicit() { var r = new MinimumHardwareResult(50, 50, 50, 50, 65, 65, 65, 65, 75, "{}");
     Assert.True(r.SafeCpuPercent > r.CpuPercent);
     var f = new ForecastResult(80, "Medium", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(5), new[] { "evidence" });
     Assert.InRange(f.ReleaseRiskPercent, 0, 100);
     }
    [Fact] public void Decision_items_are_claim_level_not_person_score() { var item = new PmDecisionItem(1, "rendering", "Claim is stale", "No fresh evidence", "Collect evidence", "Medium"); Assert.DoesNotContain("trust score", item.Situation, StringComparison.OrdinalIgnoreCase); }
}
