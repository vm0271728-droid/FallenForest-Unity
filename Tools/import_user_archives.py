#!/usr/bin/env python3
"""Import the exact Fallen Forest user-supplied ZIP archives into Unity asset paths.

This is intentionally strict. It does not search the internet or substitute models/media.
The user can upload the original ZIP files from a phone into ThirdParty/UserUploads and CI
can unpack only the approved contents. The unwanted amazing-grace screamer is never copied.
"""
from __future__ import annotations

import shutil
import sys
import zipfile
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[1]
UPLOADS = ROOT / "ThirdParty" / "UserUploads"
ASSETS = ROOT / "Assets" / "FallenForest"


class ImportFailure(RuntimeError):
    pass


def find_archive(*names: str) -> Path | None:
    if not UPLOADS.is_dir():
        return None
    indexed = {p.name.casefold(): p for p in UPLOADS.iterdir() if p.is_file() and p.suffix.casefold() == ".zip"}
    for name in names:
        match = indexed.get(name.casefold())
        if match:
            return match
    return None


def normalize_member(name: str) -> str:
    return str(PurePosixPath(name.replace("\\", "/")))


def safe_member(archive: zipfile.ZipFile, wanted: str) -> zipfile.ZipInfo:
    wanted_norm = normalize_member(wanted).casefold()
    for info in archive.infolist():
        normalized = normalize_member(info.filename)
        path = PurePosixPath(normalized)
        if path.is_absolute() or ".." in path.parts:
            raise ImportFailure(f"Unsafe path in archive: {info.filename}")
        if normalized.casefold() == wanted_norm:
            return info
    raise ImportFailure(f"Required archive member is missing: {wanted}")


def extract_member(archive_path: Path, member: str, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive_path, "r") as zf:
        info = safe_member(zf, member)
        with zf.open(info, "r") as src, destination.open("wb") as dst:
            shutil.copyfileobj(src, dst, length=1024 * 1024)
    print(f"Imported: {destination.relative_to(ROOT)} ({destination.stat().st_size:,} bytes)")


def import_locust() -> bool:
    archive = find_archive("toe-locust-by-doumty.zip")
    if not archive:
        return False
    base = ASSETS / "Art" / "Models" / "DoctorNowhere" / "Locust"
    extract_member(archive, "source/T_O_E Locust - By Doumty.fbx", base / "T_O_E_Locust_By_Doumty.fbx")
    for filename in (
        "locust_basecolor_tex.png",
        "locust_fibers_tex.png",
        "locust_normal_tex.png",
        "locust_metallic_tex.png",
        "locust_roughness_tex.png",
    ):
        extract_member(archive, f"textures/{filename}", base / "Textures" / filename)
    return True


def import_boiled() -> bool:
    archive = find_archive("the-boiled-one-horror-game-boiled-one.zip")
    if not archive:
        return False
    base = ASSETS / "Art" / "Models" / "DoctorNowhere" / "Boiled"
    extract_member(archive, "source/BoiledOne.fbx", base / "BoiledOne_MGRips.fbx")
    for filename in (
        "BoiledOne_Body_AlbedoTransparency.png",
        "BoiledOne_Eyes_AlbedoTransparency.png",
        "BoiledOne_TeethMaterial_AlbedoTransparency.png",
        "BoiledOne_GumsMaterial_AlbedoTransparency.png",
        "PNG.png",
        "BoiledOne_Details_AlbedoTransparency.png",
    ):
        extract_member(archive, f"textures/{filename}", base / "Textures" / filename)
    return True


def import_pickup() -> bool:
    archive = find_archive("pickup-truck.zip")
    if not archive:
        return False
    base = ASSETS / "Art" / "Models" / "Vehicles" / "Pickup"
    extract_member(archive, "source/Pickup Afghanistan.fbx", base / "Pickup_Afghanistan.fbx")
    extract_member(archive, "textures/Pickup_Afghanistan.png", base / "Textures" / "Pickup_Afghanistan.png")
    return True


def find_single_video_member(archive_path: Path) -> str:
    with zipfile.ZipFile(archive_path, "r") as zf:
        videos = [normalize_member(i.filename) for i in zf.infolist() if not i.is_dir() and i.filename.lower().endswith(".mp4")]
    if len(videos) != 1:
        raise ImportFailure(f"Expected exactly one MP4 in {archive_path.name}, found {len(videos)}")
    return videos[0]


def import_boiled_video() -> bool:
    archive = find_archive(
        "Видео для скримера вареного.zip",
        "boiled-jumpscare.zip",
        "boiled_one_jumpscare.zip",
    )
    if not archive:
        return False
    member = find_single_video_member(archive)
    extract_member(archive, member, ASSETS / "Video" / "boiled_one_jumpscare.mp4")
    return True


def import_screamers() -> bool:
    archive = find_archive("скримеры.zip", "locust-screamers.zip", "screamers.zip")
    if not archive:
        return False
    base = ASSETS / "Audio" / "Screamers"
    extract_member(archive, "jakes-screamer.mp3", base / "jakes-screamer.mp3")
    extract_member(
        archive,
        "the-screamer-shared-between-mallie-and-jenny.mp3",
        base / "the-screamer-shared-between-mallie-and-jenny.mp3",
    )
    forbidden = base / "amazing-grace-analog-horror.mp3"
    if forbidden.exists():
        forbidden.unlink()
        print(f"Removed forbidden release media: {forbidden.relative_to(ROOT)}")
    return True


def main() -> int:
    UPLOADS.mkdir(parents=True, exist_ok=True)
    imported = {
        "Locust model": import_locust(),
        "Boiled model": import_boiled(),
        "pickup truck": import_pickup(),
        "Boiled video": import_boiled_video(),
        "Locust screamers": import_screamers(),
    }

    print("\nUser archive import summary:")
    for label, done in imported.items():
        print(f" - {label}: {'IMPORTED' if done else 'archive not present'}")

    # Missing archives are allowed during normal source development. Release validation remains
    # responsible for failing the final APK build if required media has not been supplied.
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ImportFailure, zipfile.BadZipFile, OSError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
