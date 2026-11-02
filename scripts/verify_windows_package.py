#!/usr/bin/env python3
"""Verify a *real* Unity Windows IL2CPP build artifact. Never fabricate packages.

This program only verifies an archive produced by WindowsReleaseBuilder.
It does not create a game or bypass the Unity asset/editor build gates.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import zipfile
from pathlib import Path


class PackageVerificationError(ValueError):
    pass


EXPECTED = {
    "Vigilante8Offence3.exe",
    "Vigilante8Offence3_Data/",
    "UnityPlayer.dll",
    "GameAssembly.dll",
    "release-manifest.json",
}


def verify_one(archive: Path) -> dict:
    if not archive.is_file() or archive.stat().st_size == 0:
        raise PackageVerificationError("Unity Windows ZIP is absent or empty")
    sidecar = Path(str(archive) + ".sha256")
    if not sidecar.is_file():
        raise PackageVerificationError("Unity Windows ZIP SHA-256 manifest is missing")
    marker = sidecar.read_text("utf-8")
    match = re.fullmatch(r"([0-9a-f]{64})  " + re.escape(archive.name) + r"\n?", marker)
    if match is None:
        raise PackageVerificationError("Invalid SHA-256 manifest format or asset filename")
    digest = hashlib.sha256()
    with archive.open("rb") as data:
        for block in iter(lambda: data.read(1024 * 1024), b""):
            digest.update(block)
    if digest.hexdigest() != match.group(1):
        raise PackageVerificationError("Windows ZIP SHA-256 does not match the sidecar")

    try:
        with zipfile.ZipFile(archive) as bundle:
            members = bundle.infolist()
            names = [member.filename.replace("\\", "/") for member in members]
            if not names or len(set(name.casefold() for name in names)) != len(names):
                raise PackageVerificationError("Windows ZIP is incomplete or has duplicate asset paths")
            for member, name in zip(members, names):
                segments = name.split("/")
                if (not name or name.startswith("/") or "\\" in member.filename or
                        any(part == ".." for part in segments) or
                        re.match(r"^[a-zA-Z]:", name) or member.flag_bits & 0x1):
                    raise PackageVerificationError("Invalid or encrypted Windows ZIP member")
            if not (EXPECTED - {"Vigilante8Offence3_Data/"}).issubset(names):
                raise PackageVerificationError("Windows EXE, UnityPlayer, GameAssembly or manifest missing")
            if not any(name.startswith("Vigilante8Offence3_Data/") and
                       not name.endswith("/") for name in names):
                raise PackageVerificationError("Unity Windows player data directory is missing")
            for entry in EXPECTED - {"Vigilante8Offence3_Data/"}:
                info = bundle.getinfo(entry)
                if info.file_size <= 0 or info.is_dir():
                    raise PackageVerificationError("Empty Unity Windows asset: " + entry)
            data = json.loads(bundle.read("release-manifest.json"))
            if data.get("product") != "Vigilante 8 Offence 3":
                raise PackageVerificationError("Unexpected product identity in Windows manifest")
            if data.get("platform") != "win-x64" or data.get("unity") != "6000.6.7f1":
                raise PackageVerificationError("Unexpected Windows build platform or Unity version")
            if data.get("assetCatalog") != {"characters": 23, "vehicles": 31, "arenas": 19}:
                raise PackageVerificationError("Official 23/31/19 asset catalog is incomplete")
            if not isinstance(data.get("version"), str) or not data["version"].strip():
                raise PackageVerificationError("Missing Windows version")
            corrupted = bundle.testzip()
            if corrupted is not None:
                raise PackageVerificationError("Windows ZIP checksum failed: " + corrupted)
    except (zipfile.BadZipFile, OSError, KeyError, json.JSONDecodeError) as exc:
        raise PackageVerificationError("Unreadable or incomplete Unity Windows ZIP") from exc

    return {"archive": archive.name, "sha256": digest.hexdigest(),
            "version": data["version"], "verified": True}


def verify_directory(builds: Path) -> dict:
    if not builds.is_dir():
        raise PackageVerificationError("Unity BuildWindows produced no Builds directory")
    candidates = sorted(builds.glob("Vigilante8Offence3-*-win-x64.zip"))
    if len(candidates) != 1:
        raise PackageVerificationError("Exactly one authentic Unity Windows ZIP is required")
    return verify_one(candidates[0])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--builds-dir", type=Path, default=Path("Builds"))
    args = parser.parse_args()
    try:
        result = verify_directory(args.builds_dir)
    except PackageVerificationError as exc:
        print("Windows package verification FAILED: " + str(exc), file=sys.stderr)
        return 1
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
