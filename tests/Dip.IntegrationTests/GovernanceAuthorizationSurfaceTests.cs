// Purpose: Verifies governance authorization surfaces derive actors server-side and enforce project-resource ownership boundaries.
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
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "subject-42"),
                new Claim("name", "owner-login")
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);

        Assert.Equal("subject-42", RequestActor.Subject(principal));
        Assert.Equal("owner-login#subject-42", RequestActor.AuditIdentity(principal));
    }

    [Fact]
    public void GovernanceHttpCommands_DoNotExposeActorOrApproverFields()
    {
        var commandTypes = new[]
        {
            typeof(CreateWaiverCommand),
            typeof(DecideWaiverCommand),
            typeof(CreateQaPolicyCommand),
            typeof(PolicyLifecycleCommand),
            typeof(RequestReleaseGateOverrideCommand),
            typeof(DecideReleaseGateOverrideCommand)
        };

        foreach (var type in commandTypes)
        {
            var names = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.DoesNotContain("Actor", names);
            Assert.DoesNotContain("ApprovedBy", names);
        }
    }

    [Theory]
    [InlineData(typeof(ArtifactsController), "Download", ProjectResourceKind.Artifact)]
    [InlineData(typeof(PipelineRunsController), "Get", ProjectResourceKind.PipelineRun)]
    [InlineData(typeof(RegressionTriageController), "Latest", ProjectResourceKind.Incident)]
    public void IndirectResourceEndpoints_RequireOwningProjectResolution(
        Type controller,
        string method,
        ProjectResourceKind expected)
    {
        var action = controller.GetMethod(method)
            ?? throw new InvalidOperationException($"Missing {controller.Name}.{method}");
        var attribute = action.GetCustomAttribute<RequireProjectResourceAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(expected, attribute!.Kind);
        Assert.Equal(ProjectMembershipRole.Viewer, attribute.Role);
    }

    [Fact]
    public void ExplicitAnonymousEndpoints_RemainMarkedAnonymous()
    {
        var metricsAttribute = typeof(MetricsController)
            .GetMethod(nameof(MetricsController.GetMetrics))!
            .GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(metricsAttribute);
        Assert.IsAssignableFrom<IAllowAnonymous>(metricsAttribute);

        var webhookAttribute = typeof(WebhooksController)
            .GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(webhookAttribute);
        Assert.IsAssignableFrom<IAllowAnonymous>(webhookAttribute);
    }
}
