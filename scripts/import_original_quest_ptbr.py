#!/usr/bin/env python3
"""Import original V8 and V82 Quest missions with text-only PT-BR overlay.

Input is quest-source-audit.json from the archived, authoritative PS1 decoder.
No game missions, conditions, actors, route data or order are authored here.
Absent original data or untranslated briefings are always rejected.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path

PROVENANCE = "v8-v82-source-quest-bin-verbatim-mechanics-ptbr-text-only"
EXPECTED = {"v8": (12, 48), "v82": (15, 120)}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def briefing_hash(text):
    if not isinstance(text, str) or not text:
        raise ValueError("Missing authentic source briefing")
    return digest(text.encode("latin1", errors="strict"))


def valid_hash(value):
    return isinstance(value, str) and len(value) == 64 and all(
        c in "0123456789abcdef" for c in value)


def records(source):
    if not valid_hash(source.get("original_sha256")) or not valid_hash(
            source.get("sequel_sha256")):
        raise ValueError("Original QUEST.BIN SHA-256 provenance missing")
    routes = source.get("native_routes")
    if not isinstance(routes, list) or len(routes) != 3:
        raise ValueError("Native route binary metadata missing")
    all_campaigns = []
    for game, key in (("v8", "original"), ("v82", "sequel")):
        characters = source.get(key)
        if not isinstance(characters, list):
            raise ValueError("Original characters missing")
        campaigns = []
        seen_characters = set()
        for item in characters:
            missions = item.get("missions")
            if not isinstance(missions, list):
                raise ValueError("Original campaign record missing")
            if not missions:
                continue
            index = item.get("source_type")
            if not isinstance(index, int) or index < 0 or index in seen_characters:
                raise ValueError("Duplicate or missing source character slot")
            seen_characters.add(index)
            name = item.get("stable_id") if game == "v8" else item.get("name")
            if not isinstance(name, str) or not name.strip():
                raise ValueError("Original source identity missing")
            team = item.get("team") if game == "v82" else -1
            if game == "v82" and (not isinstance(team, int) or team not in (0, 1, 2)):
                raise ValueError("Original native team missing")
            route = routes[team] if game == "v82" else None
            if route is not None and (route.get("count") != len(missions) or
                                      len(route.get("records", [])) != len(missions)):
                raise ValueError("Native source route does not match mission count")
            campaign_id = f"{game}:{index}"
            steps = []
            for pos, mission in enumerate(missions):
                english = mission.get("briefing")
                source_hash = briefing_hash(english)
                actors = mission.get("actors")
                if not isinstance(actors, list) or len(actors) > 64 or any(
                    not isinstance(actor, dict) or not all(
                        k in actor for k in ("type", "flags", "spawn", "aux"))
                    for actor in actors):
                    raise ValueError("Original mission actor descriptors incomplete")
                route_hex = route["records"][pos].lower() if route is not None else ""
                if route is not None and (
                    len(route_hex) != 40 or any(c not in "0123456789abcdef" for c in route_hex)):
                    raise ValueError("Native V82 route source bytes invalid")
                arena = mission.get("arena") if game == "v8" else -1
                first = mission.get("target_first") if game == "v8" else -1
                last = mission.get("target_last") if game == "v8" else -1
                card = mission.get("loading_card_variant") if game == "v8" else -1
                if game == "v8" and (
                    not isinstance(arena, int) or not 0 <= arena < 10 or
                    not isinstance(first, int) or not isinstance(last, int) or
                    not 0 <= first <= last <= 65535 or
                    not isinstance(card, int) or not 0 <= card <= 255):
                    raise ValueError("V8 original source objective range invalid")
                raw_mechanics = json.dumps({
                    "sourceMissionIndex": pos, "sourceArenaIndex": arena,
                    "targetFirst": first, "targetLast": last,
                    "loadingCardVariant": card, "sourceTeamIndex": team,
                    "actors": actors, "sourceRouteRecordHex": route_hex,
                    "sourceBriefingSha256": source_hash,
                }, sort_keys=True, ensure_ascii=True, separators=(",", ":")).encode("ascii")
                directive = english.rstrip("\r\n\x00").splitlines()[-1].strip()
                goal = ("DESTROY" if directive.startswith("DESTROY: ") else
                        "PROTECT" if directive.startswith("PROTECT: ") else "")
                steps.append({
                    "id": f"{campaign_id}:mission:{pos}",
                    "sourceMissionIndex": pos,
                    "briefingPtBr": "",
                    "sourceBriefingSha256": source_hash,
                    "sourceRecordSha256": digest(raw_mechanics),
                    "sourceObjectiveDirective": goal if game == "v8" else "",
                    "sourceRouteRecordHex": route_hex,
                    "sourceArenaIndex": arena,
                    "targetFirst": first, "targetLast": last,
                    "loadingCardVariant": card, "sourceTeamIndex": team,
                    "sourceActorCount": len(actors),
                })
            campaigns.append({
                "id": campaign_id, "game": game,
                "sourceCharacter": name, "sourceCharacterIndex": index,
                "missions": steps,
            })
        if (len(campaigns), sum(len(c["missions"]) for c in campaigns)) != EXPECTED[game]:
            raise ValueError(f"Incorrect authentic {game} campaign/mission source count")
        all_campaigns.extend(campaigns)
    return all_campaigns


def worksheet(source):
    originals = {}
    for key in ("original", "sequel"):
        for character in source[key]:
            for mission in character["missions"]:
                briefing = mission["briefing"]
                key_hash = briefing_hash(briefing)
                if key_hash in originals and originals[key_hash]["source"] != briefing:
                    raise ValueError("Source briefing SHA collision")
                originals.setdefault(key_hash, {
                    "sha256": key_hash, "source": briefing, "ptBr": ""
                })
    return {"schemaVersion": 1, "locale": "pt-BR",
            "source": "source-quest-briefings-only",
            "translations": list(originals.values())}


def apply_ptbr(source, translated):
    campaigns = records(source)
    if translated.get("schemaVersion") != 1 or translated.get("locale") != "pt-BR" or \
            translated.get("source") != "source-quest-briefings-only":
        raise ValueError("PT-BR translation worksheet provenance invalid")
    values = {}
    for entry in translated.get("translations", []):
        if not isinstance(entry, dict):
            raise ValueError("Invalid translation entry")
        key = entry.get("sha256")
        original = entry.get("source")
        ptbr = entry.get("ptBr")
        if not valid_hash(key) or briefing_hash(original) != key or key in values or \
                not isinstance(ptbr, str) or not ptbr.strip() or \
                ptbr == original or "\x00" in ptbr or len(ptbr) > 4096:
            raise ValueError("Missing, unchanged or unverified PT-BR translation")
        values[key] = ptbr
    keys = {m["sourceBriefingSha256"] for c in campaigns for m in c["missions"]}
    if values.keys() != keys:
        raise ValueError("Original briefing entries missing or extraneous")
    for campaign in campaigns:
        for mission in campaign["missions"]:
            mission["briefingPtBr"] = values[mission["sourceBriefingSha256"]]
    return {
        "schemaVersion": 1, "locale": "pt-BR", "provenance": PROVENANCE,
        "v8QuestSha256": source["original_sha256"],
        "v82QuestSha256": source["sequel_sha256"],
        "campaigns": campaigns,
    }


def verify_binary_provenance(source, v8_quest: Path, v82_quest: Path):
    """Verify real decoded QUEST bytes, not just a user-editable audit digest."""
    for path, key in ((v8_quest, "original_sha256"),
                      (v82_quest, "sequel_sha256")):
        if not path.is_file() or path.stat().st_size < 16 or                 digest(path.read_bytes()) != source.get(key):
            raise ValueError("QUEST.BIN original SHA-256 mismatch or file missing")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--audit", type=Path, required=True)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--template", type=Path)
    mode.add_argument("--translations", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--v8-quest-bin", type=Path,
                        help="original extracted V8 QUEST.BIN bytes")
    parser.add_argument("--v82-quest-bin", type=Path,
                        help="original extracted 2nd Offense QUEST.BIN bytes")
    args = parser.parse_args()
    if args.translations and (not args.output or not args.v8_quest_bin or
                              not args.v82_quest_bin):
        parser.error("--translations requires --output and BOTH original QUEST.BIN files")
    source = json.loads(args.audit.read_text("utf-8"))
    records(source)
    if args.translations:
        verify_binary_provenance(source, args.v8_quest_bin, args.v82_quest_bin)
    if args.template:
        path, data = args.template, worksheet(source)
    else:
        path, data = args.output, apply_ptbr(source, json.loads(
            args.translations.read_text("utf-8")))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", "utf-8")
    print("Source-authenticated Quest PT-BR:", path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
