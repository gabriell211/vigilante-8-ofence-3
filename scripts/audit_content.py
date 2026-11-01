#!/usr/bin/env python3
"""Report real Unity content integration. Catalog entries are NOT imported assets.

Exit status is informational by default (use --strict for an actual release gate).
Checks disk files only; Unity's editor/build validation is still mandatory.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def inspect(root: Path = ROOT) -> dict:
    roster = json.loads((root / "Assets/Resources/Catalog/roster.json").read_text("utf-8"))
    details = []
    for kind, subfolder in (("vehicles", "Vehicles"), ("arenas", "Arenas")):
        for entry in roster[kind]:
            relative = f"Assets/Resources/{subfolder}/{entry['assetId']}.prefab"
            details.append({
                "category": kind,
                "id": entry["id"],
                "name": entry["name"],
                "prefab": relative,
                "present": (root / relative).is_file(),
            })
    found = sum(item["present"] for item in details)
    return {
        "expected": len(details), "present": found,
        "missing": len(details) - found,
        "complete": found == len(details),
        "assets": details,
        "note": "Presence is not proof of correct meshes, materials, gameplay, or licensed redistribution."
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--strict", action="store_true",
                        help="return failure when any release content prefab is missing")
    parser.add_argument("--json", action="store_true")
    arguments = parser.parse_args()
    report = inspect()
    if arguments.json:
        print(json.dumps(report, ensure_ascii=False, indent=2))
    else:
        print(f"Unity content: {report['present']}/{report['expected']} prefabs present "
              f"({report['missing']} missing).")
        for asset in report["assets"]:
            if not asset["present"]:
                print(f"  MISSING {asset['category']}: {asset['name']} ({asset['prefab']})")
        print(report["note"])

    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as summary:
            summary.write(
                "### Conteúdo real do jogo Unity\n"
                f"Prefabs presentes: **{report['present']}/{report['expected']}**. "
                f"Pendentes: **{report['missing']}**.\n\n"
                "O catálogo não conta como asset integrado. "
                "É obrigatório compilar e testar no Unity antes de publicar.\n"
            )
    return 1 if arguments.strict and not report["complete"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
