#!/usr/bin/env python3
"""Import the user's canonical `Деревья.zip` into Fallen Forest source paths.

The importer keeps every supplied pack, but only converts archive formats that Python can safely
unpack itself. Black Spruce is fully expanded including LOD0-LOD4 FBX + textures. Dead firs are
expanded from the nested ZIP. The low-poly RAR is preserved byte-for-byte together with textures;
its Blender source can be converted in a later authored asset pass without silently replacing it.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import shutil
import sys
import tempfile
import zipfile

ROOT = Path("Assets/FallenForest/Art/Vegetation/UserTrees")


def safe_extract(zf: zipfile.ZipFile, destination: Path) -> None:
    destination = destination.resolve()
    for info in zf.infolist():
        rel = Path(info.filename.replace("\\", "/"))
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


def copy_tree(source: Path, destination: Path) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    for item in source.rglob("*"):
        if not item.is_file():
            continue
        rel = item.relative_to(source)
        dst = destination / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(item, dst)


def exactly_one(root: Path, pattern: str) -> Path:
    matches = list(root.rglob(pattern))
    if len(matches) != 1:
        raise RuntimeError(f"Expected exactly one {pattern} under {root}, found {len(matches)}")
    return matches[0]


def extract_nested_zip(path: Path, destination: Path) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path) as zf:
        safe_extract(zf, destination)


def import_archive(archive: Path) -> None:
    if not archive.is_file():
        raise FileNotFoundError(archive)

    with tempfile.TemporaryDirectory(prefix="fallen_forest_trees_") as td:
        temp = Path(td)
        outer_dir = temp / "outer"
        outer_dir.mkdir()
        with zipfile.ZipFile(archive) as outer:
            safe_extract(outer, outer_dir)

        packs = sorted(outer_dir.glob("*.zip"))
        if len(packs) != 3:
            raise RuntimeError(f"Canonical tree archive must contain exactly 3 nested packs; found {len(packs)}")

        black_pack = next((p for p in packs if "black-spruce" in p.name.lower()), None)
        low_pack = next((p for p in packs if "low-poly" in p.name.lower()), None)
        dead_pack = next((p for p in packs if "dead-firs" in p.name.lower()), None)
        if black_pack is None or low_pack is None or dead_pack is None:
            raise RuntimeError("Could not classify all three canonical tree packs.")

        black_outer = temp / "black_outer"
        extract_nested_zip(black_pack, black_outer)
        black_source_zip = exactly_one(black_outer / "source", "*.zip")
        black_dst = ROOT / "BlackSpruce"
        extract_nested_zip(black_source_zip, black_dst / "Source")
        if (black_outer / "textures").exists():
            copy_tree(black_outer / "textures", black_dst / "ExtraTextures")

        lod_fbx = sorted((black_dst / "Source").rglob("*.fbx"))
        expected = [f"LOD{i}" for i in range(5)]
        for marker in expected:
            if not any(marker.lower() in p.name.lower() for p in lod_fbx):
                raise RuntimeError(f"Black Spruce source is missing {marker} FBX.")

        dead_outer = temp / "dead_outer"
        extract_nested_zip(dead_pack, dead_outer)
        dead_source_zip = exactly_one(dead_outer / "source", "*.zip")
        dead_dst = ROOT / "DeadFirs"
        extract_nested_zip(dead_source_zip, dead_dst / "Source")
        if (dead_outer / "textures").exists():
            copy_tree(dead_outer / "textures", dead_dst / "ExtraTextures")
        exactly_one(dead_dst / "Source", "firs.obj")

        low_outer = temp / "low_outer"
        extract_nested_zip(low_pack, low_outer)
        low_dst = ROOT / "LowPolyForest"
        if (low_outer / "source").exists():
            copy_tree(low_outer / "source", low_dst / "Source")
        if (low_outer / "textures").exists():
            copy_tree(low_outer / "textures", low_dst / "Textures")
        exactly_one(low_dst / "Source", "*.rar")

    print("Canonical tree archive imported:")
    print(f" - {ROOT / 'BlackSpruce'} (LOD0-LOD4 ready for Unity)")
    print(f" - {ROOT / 'DeadFirs'} (OBJ source ready for Unity)")
    print(f" - {ROOT / 'LowPolyForest'} (RAR/Blend source preserved + textures)")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path, help="Path to canonical Деревья.zip")
    args = parser.parse_args()
    if not Path("Assets/FallenForest").exists():
        print("Run from the FallenForest-Unity repository root.", file=sys.stderr)
        return 2
    try:
        import_archive(args.archive)
    except Exception as exc:
        print(f"TREE IMPORT FAILED: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
