// Purpose: Verifies P ro je ct In te ll ig en ce Au th or iz at io nS ur fa ce behavior, edge cases, and invariants for this repository area.
using System.Reflection;
using Dip.Api.Authorization;
using Dip.Api.Controllers;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Dip.IntegrationTests;

public sealed class ProjectIntelligenceAuthorizationSurfaceTests
{
    [Theory]
    [InlineData(nameof(ProjectIntelligenceSourcesController.Overview), ProjectMembershipRole.Viewer)]
    [InlineData(nameof(ProjectIntelligenceSourcesController.Gantt), ProjectMembershipRole.Viewer)]
    [InlineData(nameof(ProjectIntelligenceSourcesController.Digest), ProjectMembershipRole.Viewer)]
    [InlineData(nameof(ProjectIntelligenceSourcesController.Sync), ProjectMembershipRole.Maintainer)]
    [InlineData(nameof(ProjectIntelligenceSourcesController.AnalyzePlan), ProjectMembershipRole.Maintainer)]
    [InlineData(nameof(ProjectIntelligenceSourcesController.ImportPlan), ProjectMembershipRole.Maintainer)]
    [InlineData(nameof(ProjectIntelligenceSourcesController.Bind), ProjectMembershipRole.Maintainer)]
    public void ProjectIntelligenceActions_RequireExpectedMembership(string method, ProjectMembershipRole expected)
    {
        var action = typeof(ProjectIntelligenceSourcesController).GetMethod(method)
            ?? throw new InvalidOperationException($"Missing ProjectIntelligenceSourcesController.{method}");
        var attr = action.GetCustomAttribute<RequireProjectRoleAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(expected, attr!.Role);
        Assert.Equal("projectId", attr.RouteKey);
    }

    [Fact]
    public void CollaborationWebhooks_AreExplicitlyAnonymousBecauseTheyUseIndependentSignatures()
    {
        foreach (var method in new[] { nameof(CollaborationEvidenceWebhooksController.Slack), nameof(CollaborationEvidenceWebhooksController.Teams) })
        {
            var action = typeof(CollaborationEvidenceWebhooksController).GetMethod(method)!;
            Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
        }
    }
}
