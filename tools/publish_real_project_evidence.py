#!/usr/bin/env python3
# Purpose: Deprecated compatibility entry point; real-engine project evidence is optional and no longer part of final portfolio publication.
from __future__ import annotations

import sys


def main() -> int:
    print(
        "publish_real_project_evidence.py is no longer part of the final publication path.\n"
        "Use tools/publish_qa_scenario_evidence.py for the reproducible QA -> Project Truth portfolio scenario.\n"
        "If a real Unity/Unreal project becomes available later, run the optional engine verifier separately and present it as optional runtime evidence only.",
        file=sys.stderr,
    )
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
