#!/usr/bin/env python3
"""Import the user's canonical `все нужное.zip` into Fallen Forest source paths.

The script is deterministic and release-safe:
- real user FBX/GLB/video/audio are copied, never replaced with placeholders;
- the exact document GLB is additionally converted to a Unity-native OBJ with no simplification;
- the original Boiled One H.264 MP4 is preserved byte-for-byte in StreamingAssets because
  the Linux Unity 6 Editor can only import VP8 video clips;
- `amazing-grace-analog-horror.mp3` is explicitly rejected;
- nested archive names may be Unicode or GitHub-style #UXXXX escaped names;
- grass stays as one source FBX; Unity splits its three variants by measured XZ footprint.

Usage:
    python3 Tools/import_all_needed_archive.py /path/to/все\\ нужное.zip

Run from repository root.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import re
import shutil
import sys
import tempfile
import zipfile

from glb_to_obj import convert_glb_to_obj

ROOT = Path("Assets/FallenForest")
STREAMING_VIDEO = Path("Assets/StreamingAssets/FallenForest/Video/boiled_one_jumpscare.mp4")
FORBIDDEN = "amazing-grace-analog-horror.mp3"


def decode_hash_u(name: str) -> str:
    """Decode archive names like #U0412#U0438... without touching normal text."""
    def repl(match: re.Match[str]) -> str:
        try:
            return chr(int(match.group(1), 16))
        except ValueError:
            return match.group(0)
    return re.sub(r"#U([0-9A-Fa-f]{4})", repl, name)


def safe_extract(zf: zipfile.ZipFile, destination: Path) -> None:
    destination = destination.resolve()
    for info in zf.infolist():
        rel = Path(decode_hash_u(info.filename.replace("\\", "/")))
        if rel.is_absolute() or ".." in rel.parts:
            raise RuntimeError(f"Unsafe archive member: {info.filename}")
        target = (destination / rel).resolve()
        if destination not in target.parents and target != destination:
            raise RuntimeError(f"Archive traversal attempt: {info.filename}")
        if info.is_dir():
            target.mkdir(parents=True, exist_ok=True)
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        with zf.open(info) as src, target.open("wb") as dst:
            shutil.copyfileobj(src, dst)


def zip_members_lower(zpath: Path) -> set[str]:
    with zipfile.ZipFile(zpath) as zf:
        return {decode_hash_u(i.filename).lower() for i in zf.infolist() if not i.is_dir()}


def classify_nested(zpath: Path) -> str:
    names = zip_members_lower(zpath)
    joined = "\n".join(names)
    rules = [
        ("grass", "source/grass.fbx"),
        ("arms", "source/fpsarms.fbx"),
        ("flashlight", "source/flashlightfbx.fbx"),
        ("documents", "document_file_folder"),
        ("pickup", "pickup afghanistan.fbx"),
        ("locust", "t_o_e locust - by doumty.fbx"),
        ("boiled", "source/boiledone.fbx"),
        ("screamers", "jakes-screamer.mp3"),
        ("boiled_video", ".mp4"),
    ]
    for kind, needle in rules:
        if needle in joined:
            return kind
    raise RuntimeError(f"Unknown nested asset archive: {zpath.name}")


def copy_tree(source: Path, destination: Path) -> None:
    if not source.exists():
        raise FileNotFoundError(source)
    destination.mkdir(parents=True, exist_ok=True)
    for item in source.rglob("*"):
        if item.is_dir():
            continue
        rel = item.relative_to(source)
        dst = destination / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(item, dst)


def require_one(root: Path, pattern: str) -> Path:
    matches = list(root.rglob(pattern))
    if len(matches) != 1:
        raise RuntimeError(f"Expected exactly one {pattern} in {root}, found {len(matches)}")
    return matches[0]


def import_archive(outer_zip: Path) -> list[Path]:
    if not outer_zip.is_file():
        raise FileNotFoundError(outer_zip)

    changed: list[Path] = []
    with tempfile.TemporaryDirectory(prefix="fallen_forest_all_needed_") as td:
        outer_dir = Path(td) / "outer"
        outer_dir.mkdir()
        with zipfile.ZipFile(outer_zip) as zf:
            safe_extract(zf, outer_dir)

        nested = sorted(outer_dir.rglob("*.zip"))
        if len(nested) < 9:
            raise RuntimeError(f"Canonical archive should contain at least 9 nested zips; found {len(nested)}")

        classified: dict[str, Path] = {}
        for zpath in nested:
            kind = classify_nested(zpath)
            if kind in classified:
                raise RuntimeError(f"Duplicate nested archive kind {kind}: {classified[kind].name}, {zpath.name}")
            classified[kind] = zpath

        required = {"grass", "arms", "flashlight", "documents", "pickup", "locust", "boiled", "screamers", "boiled_video"}
        missing = sorted(required - classified.keys())
        if missing:
            raise RuntimeError("Missing nested archives: " + ", ".join(missing))

        for kind, zpath in classified.items():
            target = Path(td) / kind
            target.mkdir()
            with zipfile.ZipFile(zpath) as zf:
                safe_extract(zf, target)

        grass_src = Path(td) / "grass"
        grass_dst = ROOT / "Art/Vegetation/UserGrass"
        copy_tree(grass_src / "source", grass_dst / "Source")
        copy_tree(grass_src / "textures", grass_dst / "Textures")
        changed.append(grass_dst)

        arms_src = Path(td) / "arms"
        arms_dst = ROOT / "Art/Viewmodel/Arms"
        copy_tree(arms_src / "source", arms_dst / "Source")
        copy_tree(arms_src / "textures", arms_dst / "Textures")
        changed.append(arms_dst)

        flashlight_src = Path(td) / "flashlight"
        flashlight_dst = ROOT / "Art/Viewmodel/Flashlight"
        copy_tree(flashlight_src / "source", flashlight_dst / "Source")
        copy_tree(flashlight_src / "textures", flashlight_dst / "Textures")
        changed.append(flashlight_dst)

        docs_src = Path(td) / "documents"
        docs_dst = ROOT / "Art/Documents/UserDocument"
        copy_tree(docs_src / "source", docs_dst / "Source")
        copy_tree(docs_src / "textures", docs_dst / "Textures")
        document_glb = require_one(docs_dst / "Source", "*.glb")
        document_obj = docs_dst / "Source/document_file_folder.obj"
        convert_glb_to_obj(document_glb, document_obj, docs_dst / "Textures")
        changed.extend([docs_dst, document_obj, document_obj.with_suffix(".mtl")])

        pickup_src = Path(td) / "pickup"
        pickup_dst = ROOT / "Art/Vehicles/Pickup"
        copy_tree(pickup_src / "source", pickup_dst / "Source")
        copy_tree(pickup_src / "textures", pickup_dst / "Textures")
        changed.append(pickup_dst)

        for kind, folder in (("locust", "Locust"), ("boiled", "Boiled")):
            src = Path(td) / kind
            dst = ROOT / f"Art/Models/DoctorNowhere/{folder}"
            copy_tree(src / "source", dst / "Source")
            copy_tree(src / "textures", dst / "Textures")
            changed.append(dst)

        scream_src = Path(td) / "screamers"
        scream_dst = ROOT / "Audio/Screamers"
        scream_dst.mkdir(parents=True, exist_ok=True)
        approved = ["jakes-screamer.mp3", "the-screamer-shared-between-mallie-and-jenny.mp3"]
        for name in approved:
            src = require_one(scream_src, name)
            shutil.copy2(src, scream_dst / name)
            changed.append(scream_dst / name)
        forbidden_dest = scream_dst / FORBIDDEN
        if forbidden_dest.exists():
            forbidden_dest.unlink()
        if list(scream_src.rglob(FORBIDDEN)):
            print(f"Excluded forbidden screamer: {FORBIDDEN}")

        video_src = Path(td) / "boiled_video"
        mp4 = require_one(video_src, "*.mp4")
        legacy_video = ROOT / "Video/boiled_one_jumpscare.mp4"
        if legacy_video.exists():
            legacy_video.unlink()
        STREAMING_VIDEO.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(mp4, STREAMING_VIDEO)
        changed.append(STREAMING_VIDEO)

    return changed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path, help="Path to the canonical 'все нужное.zip'")
    args = parser.parse_args()

    if not (Path("Assets") / "FallenForest").exists():
        print("Run this script from the FallenForest-Unity repository root.", file=sys.stderr)
        return 2

    try:
        changed = import_archive(args.archive)
    except Exception as exc:
        print(f"IMPORT FAILED: {exc}", file=sys.stderr)
        return 1

    print("Canonical Fallen Forest source assets imported:")
    for path in changed:
        print(f" - {path}")
    print("Next: Unity release integration builds real prefabs/scenes from these sources.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
