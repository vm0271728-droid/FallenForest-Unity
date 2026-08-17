#!/usr/bin/env python3
"""Download vetted public-domain/CC0 ambience used by Fallen Forest.

The script is intentionally deterministic and refuses tiny/error-page downloads. It can be run
locally or by GitHub Actions before Unity imports the project. User-provided screamers/video are
never downloaded by this script and remain separate release inputs.
"""
from __future__ import annotations

import pathlib
import sys
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]

ASSETS = [
    {
        "url": "https://opengameart.org/sites/default/files/forest.ogg",
        "path": "Assets/FallenForest/Audio/Menu/creepy_forest_menu.ogg",
        "minimum_bytes": 5_000_000,
        "source": "OpenGameArt — Creepy Forest (F), Augmentality (Brandon Morris), CC0 option",
    },
    {
        "url": "https://opengameart.org/sites/default/files/Forest_Ambience.mp3",
        "path": "Assets/FallenForest/Audio/Ambience/forest_ambience_cc0.mp3",
        "minimum_bytes": 500_000,
        "source": "OpenGameArt — Forest Ambience, TinyWorlds, CC0",
    },
    {
        "url": "https://opengameart.org/sites/default/files/ambient_horror.ogg",
        "path": "Assets/FallenForest/Audio/Ambience/ambient_horror_cc0.ogg",
        "minimum_bytes": 500_000,
        "source": "OpenGameArt — Ambient horror, techiew, CC0",
    },
]


def download(asset: dict[str, object]) -> None:
    target = ROOT / str(asset["path"])
    target.parent.mkdir(parents=True, exist_ok=True)
    minimum = int(asset["minimum_bytes"])

    if target.exists() and target.stat().st_size >= minimum:
        print(f"OK existing: {target.relative_to(ROOT)} ({target.stat().st_size:,} bytes)")
        return

    request = urllib.request.Request(
        str(asset["url"]),
        headers={"User-Agent": "FallenForest-Unity-AssetFetcher/1.0"},
    )
    print(f"Downloading {asset['source']}...")
    with urllib.request.urlopen(request, timeout=60) as response:
        data = response.read()

    if len(data) < minimum:
        raise RuntimeError(
            f"Download for {target.name} is unexpectedly small ({len(data)} bytes); refusing to save it."
        )

    target.write_bytes(data)
    print(f"Saved: {target.relative_to(ROOT)} ({len(data):,} bytes)")


def main() -> int:
    try:
        for asset in ASSETS:
            download(asset)
    except Exception as exc:
        print(f"CC0 audio fetch failed: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
