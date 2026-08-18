#!/usr/bin/env python3
"""Import every usable model from the user's canonical `Деревья.zip`.

Release rules:
- Black Spruce: fully expand LOD0-LOD4 FBX + textures.
- Dead firs: expand the nested OBJ/MTL pack + textures.
- Low-poly forest pack: preserve the original RAR, then extract its Unity-ready FBX/OBJ/MTL and textures with
  7-Zip. Redundant Blender source files are removed from the Unity Assets tree because the same pack already
  includes FBX/OBJ/MTL and release CI intentionally does not install Blender.
- Never substitute placeholder geometry for a failed source import.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import shutil
import subprocess
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


def extract_rar_with_7zip(rar_path: Path, destination: Path) -> None:
    seven_zip = shutil.which("7z") or shutil.which("7zz") or shutil.which("7za")
    if seven_zip is None:
        raise RuntimeError(
            "7-Zip is required to unpack LOW POLY FOREST TREE PACK.rar. "
            "The release workflow must provide 7-Zip; source substitution is forbidden."
        )
    destination.mkdir(parents=True, exist_ok=True)
    process = subprocess.run(
        [seven_zip, "x", "-y", f"-o{destination}", str(rar_path)],
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        check=False,
    )
    if process.returncode != 0:
        raise RuntimeError(f"7-Zip failed to extract {rar_path.name}:\n{process.stdout}")


def remove_redundant_blender_sources(root: Path) -> None:
    """Keep Unity-ready FBX/OBJ and stop Unity from trying to launch Blender in headless CI."""
    removed = []
    for item in root.rglob("*"):
        if item.is_file() and item.suffix.lower().startswith(".blend"):
            item.unlink()
            removed.append(item)
    if removed:
        print("Removed redundant Blender-only low-poly tree sources from Unity import path:")
        for item in removed:
            print(f" - {item}")


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

        # 1) Black Spruce, exact supplied LOD chain.
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

        # 2) Dead fir OBJ pack.
        dead_outer = temp / "dead_outer"
        extract_nested_zip(dead_pack, dead_outer)
        dead_source_zip = exactly_one(dead_outer / "source", "*.zip")
        dead_dst = ROOT / "DeadFirs"
        extract_nested_zip(dead_source_zip, dead_dst / "Source")
        if (dead_outer / "textures").exists():
            copy_tree(dead_outer / "textures", dead_dst / "ExtraTextures")
        exactly_one(dead_dst / "Source", "firs.obj")

        # 3) Low-poly forest. The public ZIP wraps its Unity-ready FBX/OBJ in RAR.
        low_outer = temp / "low_outer"
        extract_nested_zip(low_pack, low_outer)
        low_dst = ROOT / "LowPolyForest"
        source_rar = exactly_one(low_outer / "source", "*.rar")
        low_dst_source = low_dst / "Source"
        low_dst_source.mkdir(parents=True, exist_ok=True)
        preserved_rar = low_dst_source / source_rar.name
        shutil.copy2(source_rar, preserved_rar)

        unpacked = temp / "low_rar"
        extract_rar_with_7zip(source_rar, unpacked)
        forest_root = unpacked / "FOREST_TREE_PACK"
        if not forest_root.exists():
            raise RuntimeError("Low-poly RAR did not contain FOREST_TREE_PACK root.")
        copy_tree(forest_root / "SOURCE", low_dst_source / "Extracted")
        remove_redundant_blender_sources(low_dst_source / "Extracted")
        copy_tree(forest_root / "TEXTURES", low_dst / "Textures")
        # The outer ZIP duplicates most textures; preserve those too, but never overwrite exact
        # nested paths in a way that loses the author's folder organization.
        if (low_outer / "textures").exists():
            copy_tree(low_outer / "textures", low_dst / "OuterTextures")

        exactly_one(low_dst_source / "Extracted", "Tree_Pack.fbx")
        exactly_one(low_dst_source / "Extracted", "Tree_Pack.obj")
        exactly_one(low_dst_source / "Extracted", "Tree_Pack.mtl")

    print("Canonical tree archive imported completely:")
    print(f" - {ROOT / 'BlackSpruce'} (LOD0-LOD4)")
    print(f" - {ROOT / 'DeadFirs'} (OBJ/MTL)")
    print(f" - {ROOT / 'LowPolyForest'} (RAR preserved + FBX/OBJ/MTL extracted)")


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
