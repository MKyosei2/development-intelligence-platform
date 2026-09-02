namespace Dip.Application.Features.Capabilities;

public sealed record CapabilityDescriptor(
    string Id,
    string Category,
    string DisplayName,
    string Description,
    bool ProjectScoped,
    string? ReadEndpointTemplate,
    string ImplementationState,
    string VerificationState);

public static class CapabilityRegistry
{
    public static IReadOnlyList<CapabilityDescriptor> All { get; } =
    [
        new("portfolio", "Projects", "Portfolio", "All registered DIP projects.", false, "api/projects", "Implemented", "RuntimeCheckRequired"),
        new("dashboard", "Development", "Dashboard", "Project truth, health and current project summary.", true, "api/v1/dashboard/projects/{projectId}", "Implemented", "RuntimeCheckRequired"),
        new("repository", "Development", "Repository", "GitHub repository state normalized into DIP.", true, "api/v1/integrations/github/projects/{projectId}/repository", "Implemented", "RuntimeCheckRequired"),
        new("ci", "Development", "CI / Builds", "Pipeline run history and build evidence.", true, "api/v1/pipeline-runs/projects/{projectId}?take=100", "Implemented", "RuntimeCheckRequired"),
        new("qa", "Development", "QA", "QA trends and findings for the selected project.", true, "api/v1/qa/projects/{projectId}/trends?take=30", "Implemented", "RuntimeCheckRequired"),
        new("debug", "Intelligence", "Debug Intelligence", "Deterministic local debugging sessions. No external AI endpoint is used.", true, "api/v1/defect-intelligence/projects/{projectId}/autonomous-debug?take=20", "Implemented", "RuntimeCheckRequired"),
        new("intelligence", "Intelligence", "Intelligence", "Evidence-grounded project intelligence dashboard.", true, "api/v1/intelligence-dashboard/projects/{projectId}", "Implemented", "RuntimeCheckRequired"),
        new("management", "Project Management", "Management", "Daily project-management cockpit.", true, "api/v1/management/projects/{projectId}/cockpit?horizonWeeks=8", "Implemented", "RuntimeCheckRequired"),
        new("sources", "Project Management", "Plan & Sources", "Project source providers and plan-ingestion state.", true, "api/v1/project-intelligence/projects/{projectId}/sources", "Implemented", "RuntimeCheckRequired"),
        new("qa-governance", "Release", "QA Governance", "QA policies, findings and governance overview.", true, "api/v1/qa-governance/projects/{projectId}/overview", "Implemented", "RuntimeCheckRequired"),
        new("system", "Platform", "System Status", "Health of API, database, analysis, worker and artifact store.", false, "api/v1/system/status", "Implemented", "RuntimeCheckRequired"),
        new("integrations", "Platform", "Integrations", "Current GitHub integration status.", false, "api/v1/integrations/github/status", "Implemented", "RuntimeCheckRequired"),
        new("capabilities", "Platform", "Capabilities", "DIP capability registry used by the native desktop shell.", false, "api/v1/capabilities", "Implemented", "RuntimeCheckRequired")
    ];
}
