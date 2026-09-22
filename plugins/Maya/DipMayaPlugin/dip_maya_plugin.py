# Purpose: Defines a focused responsibility within the development intelligence platform.
"""DIP Maya production-evidence bridge.

The plugin never stores API credentials. It writes compact JSON events to the
local Dip.Agent.Windows named pipe. The agent owns authentication, durable
queuing, privacy policy enforcement and optional media upload.
"""
from __future__ import annotations

import json
import os
import uuid
from datetime import datetime, timezone
from pathlib import Path

import maya.api.OpenMaya as om
import maya.cmds as cmds

_CALLBACKS = []


def _config():
    pipe = os.environ.get("DIP_AGENT_PIPE_NAME", "").strip()
    token = os.environ.get("DIP_AGENT_BRIDGE_TOKEN", "").strip() or None
    root = os.environ.get("DIP_AGENT_PROJECT_ROOT", "").strip()
    if not pipe:
        raise RuntimeError("DIP_AGENT_PIPE_NAME is required for the Maya bridge")
    if not root:
        workspace = cmds.workspace(query=True, rootDirectory=True) or ""
        root = workspace
    return pipe, token, str(Path(root).resolve())


def _relative(path: str | None):
    if not path:
        return None
    try:
        root = Path(_config()[2]).resolve()
        candidate = Path(path).resolve()
        return candidate.relative_to(root).as_posix()
    except Exception:
        return None


def _scene_stats():
    try:
        textures = cmds.ls(type="file") or []
        missing = []
        for node in textures:
            try:
                p = cmds.getAttr(node + ".fileTextureName") or ""
                if p and not os.path.exists(p):
                    missing.append(p)
            except Exception:
                pass
        refs = []
        try:
            refs = cmds.file(query=True, reference=True) or []
        except Exception:
            pass
        return {
            "polygonCount": int(cmds.polyEvaluate(face=True) or 0),
            "vertexCount": int(cmds.polyEvaluate(vertex=True) or 0),
            "materialCount": len(cmds.ls(materials=True) or []),
            "textureCount": len(textures),
            "jointCount": len(cmds.ls(type="joint") or []),
            "animationStart": float(cmds.playbackOptions(query=True, minTime=True)),
            "animationEnd": float(cmds.playbackOptions(query=True, maxTime=True)),
            "referenceCount": len(refs),
            "missingTextures": missing[:100],
            "unknownNodeCount": len(cmds.ls(type="unknown") or []),
        }
    except Exception as exc:
        return {"metadataCollectionError": str(exc)[:500]}


def _send(event_kind: int, path: str | None = None, metadata=None,
          media_kind: int | None = None, media_path: str | None = None,
          media_content_type: str | None = None):
    pipe_name, token, _ = _config()
    event = {
        "eventId": "maya:" + uuid.uuid4().hex,
        "source": 1,
        "kind": event_kind,
        "toolVersion": cmds.about(version=True),
        "relativePath": _relative(path),
        "artifactRelativePath": _relative(path),
        "contentHash": None,
        "sizeBytes": os.path.getsize(path) if path and os.path.isfile(path) else None,
        "observedAtUtc": datetime.now(timezone.utc).isoformat(),
        "metadata": metadata or {},
        "mediaKind": media_kind,
        "mediaRelativePath": _relative(media_path),
        "mediaContentType": media_content_type,
    }
    message = json.dumps({"token": token, "event": event}, ensure_ascii=False) + "\n"
    pipe_path = "\\\\.\\pipe\\" + pipe_name
    fd = os.open(pipe_path, os.O_WRONLY)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", closefd=False) as stream:
            stream.write(message)
            stream.flush()
    finally:
        os.close(fd)


def _scene_path():
    return cmds.file(query=True, sceneName=True) or None


def _after_open(*_):
    _send(1, _scene_path(), _scene_stats())


def _after_save(*_):
    _send(2, _scene_path(), _scene_stats())


def _after_import(*_):
    _send(3, _scene_path(), _scene_stats())


def _after_reference(*_):
    _send(4, _scene_path(), _scene_stats())


def _after_export(*_):
    _send(5, _scene_path(), _scene_stats())


def notify_render_completed(rendered_path: str | None = None):
    """Call from a studio render wrapper after a render file exists."""
    _send(6, _scene_path(), _scene_stats(), 2 if rendered_path else None,
          rendered_path, "image/png" if rendered_path and rendered_path.lower().endswith(".png") else None)


def playblast(output_path: str):
    """Generate a Maya playblast and register the resulting file as DIP media."""
    output = str(Path(output_path).resolve())
    Path(output).parent.mkdir(parents=True, exist_ok=True)
    result = cmds.playblast(
        filename=output,
        forceOverwrite=True,
        viewer=False,
        showOrnaments=True,
        percent=100,
        quality=85)
    media = result or output
    _send(7, _scene_path(), _scene_stats(), 3, media, "video/quicktime")
    return result


def initializePlugin(_plugin):
    for message, callback in (
        (om.MSceneMessage.kAfterOpen, _after_open),
        (om.MSceneMessage.kAfterSave, _after_save),
        (om.MSceneMessage.kAfterImport, _after_import),
        (om.MSceneMessage.kAfterReference, _after_reference),
        (om.MSceneMessage.kAfterExport, _after_export),
    ):
        try:
            _CALLBACKS.append(om.MSceneMessage.addCallback(message, callback))
        except Exception:
            pass
    om.MGlobal.displayInfo("DIP Maya bridge initialized")


def uninitializePlugin(_plugin):
    while _CALLBACKS:
        callback = _CALLBACKS.pop()
        try:
            om.MMessage.removeCallback(callback)
        except Exception:
            pass
    om.MGlobal.displayInfo("DIP Maya bridge unloaded")
