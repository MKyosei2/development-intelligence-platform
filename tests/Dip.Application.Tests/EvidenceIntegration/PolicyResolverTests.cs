// Purpose: Verifies Policy Resolver behavior, edge cases, and repository invariants.
using Dip.Application.Features.Policies;
using FluentAssertions;

namespace Dip.Application.Tests.EvidenceIntegration;

public sealed class PolicyResolverTests
{
    [Fact]
    public void Project_override_cannot_enable_video_above_global_ceiling()
    {
        var global = PolicyResolver.ApplyGlobal(
            PolicyResolver.SecureDefaults(),
            new PolicyProfileDocument(
                "global",
                true,
                new EvidencePolicySettings(),
                new AgentPolicySettings(),
                new RetentionPolicySettings(),
                new AlertPolicySettings(),
                new PrivacyPolicySettings(AllowVideo: false)));

        var project = PolicyResolver.ApplyRestriction(
            global,
            new PolicyProfileDocument(
                "project",
                true,
                new EvidencePolicySettings(),
                new AgentPolicySettings(),
                new RetentionPolicySettings(),
                new AlertPolicySettings(),
                new PrivacyPolicySettings(AllowVideo: true)));

        project.Privacy.AllowVideo.Should().BeFalse();
    }

    [Fact]
    public void Project_retention_is_capped_by_global_retention()
    {
        var global = PolicyResolver.ApplyGlobal(
            PolicyResolver.SecureDefaults(),
            new PolicyProfileDocument(
                "global",
                true,
                new EvidencePolicySettings(),
                new AgentPolicySettings(),
                new RetentionPolicySettings(VideoRetentionDays: 30),
                new AlertPolicySettings(),
                new PrivacyPolicySettings()));

        var project = PolicyResolver.ApplyRestriction(
            global,
            new PolicyProfileDocument(
                "project",
                true,
                new EvidencePolicySettings(),
                new AgentPolicySettings(),
                new RetentionPolicySettings(VideoRetentionDays: 90),
                new AlertPolicySettings(),
                new PrivacyPolicySettings()));

        project.Retention.VideoRetentionDays.Should().Be(30);
    }

    [Fact]
    public void Excluded_paths_are_union_of_parent_and_project()
    {
        var global = PolicyResolver.ApplyGlobal(
            PolicyResolver.SecureDefaults(),
            new PolicyProfileDocument(
                "global",
                true,
                new EvidencePolicySettings(),
                new AgentPolicySettings(),
                new RetentionPolicySettings(),
                new AlertPolicySettings(),
                new PrivacyPolicySettings(
                    ExcludedPaths: new[] { "C:/Users/personal" })));

        var project = PolicyResolver.ApplyRestriction(
            global,
            new PolicyProfileDocument(
                "project",
                true,
                new EvidencePolicySettings(),
                new AgentPolicySettings(),
                new RetentionPolicySettings(),
                new AlertPolicySettings(),
                new PrivacyPolicySettings(
                    ExcludedPaths: new[] { "Project/Secrets" })));

        project.Privacy.ExcludedPaths.Should().BeEquivalentTo(
            new[] { "C:/Users/personal", "Project/Secrets" });
    }
}
