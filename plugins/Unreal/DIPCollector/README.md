# DIP Unreal Collector

Editor plug-in for the Phase-3 local-agent bridge.

Automatic events:
- Asset added/updated

Studio/UAT integration commands:
- `DIP.EmitBuild`
- `DIP.EmitCook`
- `DIP.EmitPackage`
- `DIP.EmitAutomationTests`
- `DIP.RegisterVideo <project-relative.mp4>`

The explicit commands are intentional: Unreal build/cook/package pipelines vary
widely across studios, so Phase 3 supplies a stable event contract that UAT,
BuildGraph or CI can call without scraping editor activity.

Set `DIP_AGENT_PIPE_NAME` and optionally `DIP_AGENT_BRIDGE_TOKEN` in the Unreal
Editor environment. No DIP API credential is stored in the plug-in.
