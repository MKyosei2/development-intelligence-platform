// Purpose: Verifies Management Operations Authorization Surface behavior, edge cases, and repository invariants.
using System.Reflection;
using Dip.Api.Authorization;
using Dip.Api.Controllers;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;

namespace Dip.IntegrationTests;

public sealed class ManagementOperationsAuthorizationSurfaceTests
{
    [Theory]
    [InlineData(nameof(ManagementOperationsController.UpsertManagementRole), ProjectMembershipRole.Owner)]
    [InlineData(nameof(ManagementOperationsController.UpsertSharedResourceMembership), ProjectMembershipRole.Maintainer)]
    [InlineData(nameof(ManagementOperationsController.CreateApproval), ProjectMembershipRole.Maintainer)]
    [InlineData(nameof(ManagementOperationsController.RespondApproval), ProjectMembershipRole.Maintainer)]
    [InlineData(nameof(ManagementOperationsController.UpsertAuthority), ProjectMembershipRole.Owner)]
    [InlineData(nameof(ManagementOperationsController.Synchronize), ProjectMembershipRole.Maintainer)]
    public void SensitiveManagementCommands_HaveExplicitProjectRole(string method, ProjectMembershipRole expected)
    {
        var action = typeof(ManagementOperationsController).GetMethod(method)
            ?? throw new InvalidOperationException($"Missing ManagementOperationsController.{method}");
        var attr = action.GetCustomAttribute<RequireProjectRoleAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(expected, attr!.Role);
        Assert.Equal("projectId", attr.RouteKey);
    }

    [Fact]
    public void GoogleSheetsManagementSync_RequiresMaintainer()
    {
        var action = typeof(ManagementSpreadsheetController).GetMethod(nameof(ManagementSpreadsheetController.SyncGoogleSheets))!;
        var attr = action.GetCustomAttribute<RequireProjectRoleAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(ProjectMembershipRole.Maintainer, attr!.Role);
    }

    [Fact]
    public void PortfolioEndpoint_IsProjectReadProtected()
    {
        var action = typeof(PortfolioManagementController).GetMethod(nameof(PortfolioManagementController.Get))!;
        var authorize = action.GetCustomAttributes().OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single();
        Assert.Equal(Policies.ProjectRead, authorize.Policy);
    }
    [Fact]
    public void NotificationHistory_IsReadOnlyAndViewerScoped()
    {
        var action = typeof(ManagementOperationsController).GetMethod(nameof(ManagementOperationsController.AlertHistory))
            ?? throw new InvalidOperationException("Missing ManagementOperationsController.AlertHistory");
        var role = action.GetCustomAttribute<RequireProjectRoleAttribute>();
        var authorize = action.GetCustomAttributes().OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single();

        Assert.NotNull(role);
        Assert.Equal(ProjectMembershipRole.Viewer, role!.Role);
        Assert.Equal("projectId", role.RouteKey);
        Assert.Equal(Policies.ProjectRead, authorize.Policy);
    }

    [Fact]
    public void ProjectGitHubCapabilityProbe_IsViewerScoped()
    {
        var action = typeof(GitHubConnectionController).GetMethod(nameof(GitHubConnectionController.TestProjectConnection))
            ?? throw new InvalidOperationException("Missing GitHubConnectionController.TestProjectConnection");
        var role = action.GetCustomAttribute<RequireProjectRoleAttribute>();

        Assert.NotNull(role);
        Assert.Equal(ProjectMembershipRole.Viewer, role!.Role);
        Assert.Equal("projectId", role.RouteKey);
    }

    [Fact]
    public void PullRequestDetails_ExposePipelineQaAndEvidenceTraceability()
    {
        var names = typeof(PullRequestDetailsResponse)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("PipelineRuns", names);
        Assert.Contains("QualityIssues", names);
        Assert.Contains("Evidence", names);
    }

}
