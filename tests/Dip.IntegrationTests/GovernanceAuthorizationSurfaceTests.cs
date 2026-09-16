// Purpose: Verifies G ov er na nc eA ut ho ri za ti on Su rf ac eT es ts behavior.
using System.Reflection;
using System.Security.Claims;
using Dip.Api.Authorization;
using Dip.Api.Controllers;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Dip.IntegrationTests;

public sealed class GovernanceAuthorizationSurfaceTests
{
    [Fact]
    public void RequestActor_IsDerivedFromAuthenticatedClaims()
    {
        var principal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("sub","subject-42"),new Claim("name","owner-login")},"test"));
        Assert.Equal("subject-42",RequestActor.Subject(principal));
        Assert.Equal("owner-login#subject-42",RequestActor.AuditIdentity(principal));
    }

    [Fact]
    public void GovernanceHttpCommands_DoNotExposeActorOrApproverFields()
    {
        foreach(var type in new[]{typeof(CreateWaiverCommand),typeof(DecideWaiverCommand),typeof(CreateQaPolicyCommand),typeof(PolicyLifecycleCommand),typeof(RequestReleaseGateOverrideCommand),typeof(DecideReleaseGateOverrideCommand)})
        {
            var names=type.GetProperties(BindingFlags.Instance|BindingFlags.Public).Select(x=>x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("Actor",names);Assert.DoesNotContain("ApprovedBy",names);
        }
    }

    [Theory]
    [InlineData(typeof(ArtifactsController),"Download",ProjectResourceKind.Artifact)]
    [InlineData(typeof(PipelineRunsController),"Get",ProjectResourceKind.PipelineRun)]
    [InlineData(typeof(RegressionTriageController),"Latest",ProjectResourceKind.Incident)]
    public void IndirectResourceEndpoints_RequireOwningProjectResolution(Type controller,string method,ProjectResourceKind expected)
    {
        var action=controller.GetMethod(method)??throw new InvalidOperationException($"Missing {controller.Name}.{method}");
        var attr=action.GetCustomAttribute<RequireProjectResourceAttribute>();
        Assert.NotNull(attr);Assert.Equal(expected,attr!.Kind);Assert.Equal(ProjectMembershipRole.Viewer,attr.Role);
    }

    [Fact]
    public void ExplicitAnonymousEndpoints_RemainMarkedAnonymous()
    {
        var attr=typeof(MetricsController).GetMethod(nameof(MetricsController.GetMetrics))!.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(attr);Assert.IsAssignableFrom<IAllowAnonymous>(attr);
        var webhook=typeof(WebhooksController).GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(webhook);Assert.IsAssignableFrom<IAllowAnonymous>(webhook);
    }
}
