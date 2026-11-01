#!/usr/bin/env python3
"""Re-verify pinned original scene hashes. No false playable-stage claims."""
import json
from pathlib import Path

from audit_legacy_stages import ROOT, audit

def verify(root: Path = ROOT):
    actual = audit(root)
    manifest_path = root / "Assets/GameContent/LegacyStageManifests/scene-audit.json"
    manifest = json.loads(manifest_path.read_text("utf-8"))
    if manifest != actual:
        raise ValueError("Original source scenes changed or incomplete scene audit")
    if actual["certifiedUnity6Arenas"] != 0:
        raise ValueError("Scene archive does not certify playable arenas")
    return actual

if __name__ == "__main__":
    result = verify()
    print(f"PASS: all {result['stageCount']} original Unity scene source hashes verified.")
