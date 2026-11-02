#!/usr/bin/env python3
"""Prepare a source-checked PT-BR Quest worksheet from *extracted* PS1 files.

Development/import utility only. No BIN/CUE is copied into Unity Resources or
used by the standalone Windows game. Text is never machine-invented.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LEGACY = ROOT / "LegacySources/Vigilante8ClassicRecomp/tools/recompone-v8-2"
sys.path.insert(0, str(LEGACY))
sys.path.insert(0, str(ROOT / "scripts"))
import audit_quest_sources as decoder
import import_original_quest_ptbr as importer

# Character-to-team lookup is held in the original sequel executable, not in
# its QUEST.BIN. The original decoder uses this precise native address.
V82_TEAM_OFFSET = 0x8006B08C - 0x80010000 + 0x800
V82_CHARACTER_COUNT = len(decoder.V82_NAMES)


def source_report(v8_quest: bytes, v82_quest: bytes, v82_executable: bytes) -> dict:
    """Decode authentic records; reject incomplete or inconsistent input."""
    if len(v8_quest) < 128 or len(v82_quest) < 128:
        raise ValueError("Os dois QUEST.BIN extraídos são obrigatórios e não podem estar truncados.")
    if len(v82_executable) < V82_TEAM_OFFSET + V82_CHARACTER_COUNT:
        raise ValueError("SLUS_008.68 original ausente ou truncado: equipes Quest não verificáveis.")
    teams = v82_executable[V82_TEAM_OFFSET:V82_TEAM_OFFSET + V82_CHARACTER_COUNT]
    if any(team not in (0, 1, 2) for team in teams):
        raise ValueError("Tabela de equipes nativas do V82 inválida.")
    try:
        original = decoder.decode_original(v8_quest)
        routes, sequel = decoder.decode_sequel(v82_quest, v82_executable)
    except (AssertionError, IndexError, ValueError, struct.error, UnicodeError) as exc:
        raise ValueError("Estrutura QUEST original inválida ou incompleta.") from exc
    report = {
        "original_sha256": hashlib.sha256(v8_quest).hexdigest(),
        "sequel_sha256": hashlib.sha256(v82_quest).hexdigest(),
        "original": original,
        "sequel": sequel,
        "native_routes": routes,
    }
    # Validates original eligibility, character IDs, 48 + 120 missions,
    # actors, routes and objective ranges before producing any file.
    importer.records(report)
    return report


def merge_translations(template: dict, previous: dict) -> int:
    """Keep reviewed text only when its immutable original SHA + text match."""
    if previous.get("schemaVersion") != 1 or previous.get("locale") != "pt-BR" or \
            previous.get("source") != "source-quest-briefings-only" or \
            not isinstance(previous.get("translations"), list):
        raise ValueError("Planilha de tradução anterior incompatível.")
    previous_by_sha = {}
    for row in previous["translations"]:
        if not isinstance(row, dict):
            raise ValueError("Linha de tradução anterior inválida.")
        key = row.get("sha256")
        if not importer.valid_hash(key) or key in previous_by_sha or \
                importer.briefing_hash(row.get("source")) != key:
            raise ValueError("Hash ou texto original da tradução anterior adulterado.")
        previous_by_sha[key] = row
    reused = 0
    for row in template["translations"]:
        old = previous_by_sha.get(row["sha256"])
        if old is None or old["source"] != row["source"]:
            continue
        value = old.get("ptBr")
        if isinstance(value, str) and value.strip() and value != row["source"] and \
                "\x00" not in value and len(value) <= 4096:
            row["ptBr"] = value
            reused += 1
    return reused


def write_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--v8-quest-bin", type=Path, required=True,
                        help="QUEST.BIN already extracted from original Vigilante 8")
    parser.add_argument("--v82-quest-bin", type=Path, required=True,
                        help="QUEST.BIN already extracted from 2nd Offense")
    parser.add_argument("--v82-executable", type=Path, required=True,
                        help="original 2nd Offense SLUS_008.68 (native team lookup)")
    parser.add_argument("--work-dir", type=Path, default=ROOT / "QuestImportWork")
    parser.add_argument("--previous-translations", type=Path,
                        help="optional older worksheet, merged by authenticated source hash")
    parser.add_argument("--translations", type=Path,
                        help="completed, reviewed PT-BR worksheet to compile")
    parser.add_argument("--catalog", type=Path,
                        help="output Unity Resources JSON; requires --translations")
    args = parser.parse_args(argv)
    if bool(args.translations) != bool(args.catalog):
        parser.error("--translations and --catalog must be provided together")
    report = source_report(args.v8_quest_bin.read_bytes(),
                           args.v82_quest_bin.read_bytes(),
                           args.v82_executable.read_bytes())
    template = importer.worksheet(report)
    reused = 0
    if args.previous_translations:
        reused = merge_translations(
            template, json.loads(args.previous_translations.read_text("utf-8")))
    write_json(args.work_dir / "quest-source-audit.json", report)
    write_json(args.work_dir / "quest-ptbr-template.json", template)
    print(f"Origem validada: 12/48 V8 + 15/120 V82. "
          f"{len(template['translations'])} textos distintos; {reused} traduções preservadas.")
    if args.translations:
        importer.verify_binary_provenance(
            report, args.v8_quest_bin, args.v82_quest_bin)
        localized = importer.apply_ptbr(
            report, json.loads(args.translations.read_text("utf-8")))
        write_json(args.catalog, localized)
        print(f"Catálogo PT-BR compilado, sem depender de BIN/CUE: {args.catalog}")
    else:
        print("Pendente: preencher/revisar ptBr para todos os briefings originais.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
