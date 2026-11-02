#!/usr/bin/env python3
"""Verify stage raw-mesh discovery results against the eight original scenes."""
from __future__ import annotations

import json
from pathlib import Path

from probe_legacy_stage_geometry import ROOT, probe


def verify(root: Path = ROOT) -> dict:
    actual = probe(root)
    expected = json.loads((root / "docs/v82-stage-geometry-probe.json").read_text("utf-8"))
    if expected != actual:
        raise ValueError("Stage geometry report is stale or original scene sources changed")
    if actual["status"] != "legacy_scene_geometry_probe_not_ported":
        raise ValueError("An incomplete scene may not be marked as ported")
    return actual


if __name__ == "__main__":
    d = verify()
    with_buffers = sum(v["hasRawTmdGeometry"] for v in d["arenas"].values())
    print(f"PASS: 8 legacy stage sources audited ({with_buffers} have raw vertex/face data)")
    print("Source discovery only: no original map prefab or Windows gameplay was certified")
