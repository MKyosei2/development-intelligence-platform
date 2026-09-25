#!/usr/bin/env python3
# Purpose: Provides v al id at e p ro je ct i nt el li ge nc e tooling for repository verification and development workflows.
"""Static integration contract for the Project Intelligence Layer.

This intentionally does not claim to replace dotnet build/test or live connector E2E.
"""
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
checks: list[tuple[str, bool]] = []

def need(path: str, *tokens: str) -> None:
    p = ROOT / path
    text = p.read_text(encoding="utf-8") if p.exists() else ""
    checks.append((f"{path} exists", p.exists()))
    for token in tokens:
        checks.append((f"{path}: {token}", token in text))

need("src/Dip.Domain/Enums/ProjectControlEnums.cs", "ProjectPlanImport", "PlanItem", "ExternalObservation", "EntityBinding", "EquivalentTo")
need("src/Dip.Domain/Enums/EvidenceSourceType.cs", "Planning", "IssueTracker", "Collaboration")
need("src/Dip.Application/Features/ProjectIntelligence/ProjectPlanUnderstandingEngine.cs", "WbsAliases", "TitleAliases", "ProgressAliases", "InferDottedParent")
need("src/Dip.Application/Features/ProjectIntelligence/ProjectPlanIngestionService.cs", "ManagementXlsxCodec.Read", "MaxUploadBytes", "ProjectPlanImport", "PlanItem")
need("src/Dip.Application/Features/ProjectIntelligence/ProjectEntityResolutionService.cs", "EquivalentTo", "persistHighConfidence", "EntityBinding")
need("src/Dip.Application/Features/ProjectIntelligence/ProjectSourceIngestionService.cs", "IssueTracker", "Collaboration", "HasEvidenceReference", "RawSha256")
need("src/Dip.Application/Features/ProjectIntelligence/ProjectProgressIntelligenceService.cs", "OperationalPercent", "MachineReadiness", "HasFailedCi", "BuildDigest", "No fresh progress/evidence signal")
need("src/Dip.Infrastructure/ProjectIntelligence/RedmineProjectSourceAdapter.cs", "X-Redmine-API-Key", "custom_fields", "relations", "attachments", "IsConfiguredFor")
need("src/Dip.Infrastructure/ProjectIntelligence/SlackProjectSourceAdapter.cs", "conversations.history", "Relevant", "ProjectChannelIds", "IsConfiguredFor")
need("src/Dip.Infrastructure/ProjectIntelligence/CollaborationWebhookVerifier.cs", "HMACSHA256", "FixedTimeEquals", "IsSlackChannelAllowed", "VerifyTeams")
need("src/Dip.Api/Controllers/CollaborationEvidenceWebhooksController.cs", "X-Slack-Request-Timestamp", "X-Slack-Signature", "IsSlackChannelAllowed", "X-DIP-Ingest-Key")
need("src/Dip.Api/Controllers/ProjectIntelligenceSourcesController.cs", "plans/analyze", "plans/import", "entity-bindings", "sources/{provider}/sync", "ProjectMembershipRole.Maintainer")
need("src/Dip.Web/Pages/Dashboard/ProjectSources.razor", "Excel / WBS / Gantt understanding", "Plan vs actual", "WBS / Gantt intelligence viewer", "Cross-source entity resolution")
need("src/Dip.Web/Components/Layout/MainLayout.razor", "Plan & Sources")
need("src/Dip.Application/DependencyInjection.cs", "ProjectPlanIngestionService", "ProjectProgressIntelligenceService")
need("src/Dip.Infrastructure/DependencyInjection.cs", "RedmineProjectSourceAdapter", "SlackProjectSourceAdapter", "CollaborationWebhookVerifier")
need("docs/02-architecture/project-intelligence-layer-20260816.md", "does not require a company to replace", "not an employee skill score", "Source provenance")
need("tests/Dip.Application.Tests/ProjectIntelligence/ProjectPlanUnderstandingEngineTests.cs", "JapaneseWbs_DetectsPlanningColumnsAndHierarchy", "EnglishGantt_FindsHeaderBelowTitleRows")
need("tests/Dip.IntegrationTests/ProjectIntelligenceAuthorizationSurfaceTests.cs", "RequireExpectedMembership", "CollaborationWebhooks_AreExplicitlyAnonymous")

try:
    cfg = json.loads((ROOT / "src/Dip.Api/appsettings.json").read_text(encoding="utf-8"))
    src = cfg["ProjectIntelligenceSources"]
    checks += [
        ("appsettings Redmine source", "Redmine" in src),
        ("appsettings Slack source", "Slack" in src),
        ("appsettings Teams ProjectIngestKeys", isinstance(src.get("Teams", {}).get("ProjectIngestKeys"), dict)),
    ]
except Exception:
    checks.append(("appsettings ProjectIntelligenceSources parses", False))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'} {name}")
print(f"\nProject Intelligence static integration contract: {len(checks)-len(failed)}/{len(checks)} PASS")
if failed:
    sys.exit(1)
