# DIP Maya Plugin

Phase-3 Maya bridge. Install `dip_maya_plugin.py` as a Maya Python plug-in and
load it after `Dip.Agent.Windows` is running.

Required environment variables inherited by Maya:

- `DIP_AGENT_PIPE_NAME` — same pipe name printed by the agent
- `DIP_AGENT_PROJECT_ROOT` — the monitored project root
- `DIP_AGENT_BRIDGE_TOKEN` — optional shared local bridge token

Automatically emits Scene Open/Save/Import/Reference/Export metadata. Use
`dip_maya_plugin.notify_render_completed(path)` from the studio render wrapper
when a render file exists, and `dip_maya_plugin.playblast(path)` for a
DIP-registered playblast.

The plug-in does not contain a DIP API token and does not inspect keystrokes,
clipboard, browser data or files outside the configured project root.
