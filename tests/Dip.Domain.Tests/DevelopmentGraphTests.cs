// Purpose: Defines Development Graph Tests responsibilities for this repository area.
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Domain.Tests;

public sealed class DevelopmentGraphTests
{
    [Fact]
    public void Node_ObservationUpdatesStateWithoutChangingIdentity()
    {
        var projectId = Guid.NewGuid(); var t0 = DateTimeOffset.UtcNow;
        var node = new DevelopmentGraphNode(Guid.NewGuid(), projectId, DevelopmentGraphNodeType.Feature, "feature:combat", "Combat", "Active", "DIP", null, "{}", t0);
        var id = node.Id;
        node.Observe("Combat", "NotReady", "DIP", "https://example.invalid/feature/combat", "{\"risk\":75}", t0.AddMinutes(5));
        Assert.Equal(id, node.Id); Assert.Equal("NotReady", node.Status); Assert.Equal(t0.AddMinutes(5), node.LastObservedAtUtc);
    }

    [Fact]
    public void Edge_RejectsSelfLinkAndClampsWeight()
    {
        var projectId = Guid.NewGuid(); var node = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new DevelopmentGraphEdge(Guid.NewGuid(), projectId, node, node, DevelopmentGraphEdgeType.DependsOn, 50m, false, "{}", DateTimeOffset.UtcNow));
        var edge = new DevelopmentGraphEdge(Guid.NewGuid(), projectId, Guid.NewGuid(), Guid.NewGuid(), DevelopmentGraphEdgeType.Affects, 250m, true, "{}", DateTimeOffset.UtcNow);
        Assert.Equal(100m, edge.Weight); Assert.True(edge.Critical);
    }
}
