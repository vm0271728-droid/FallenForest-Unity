#!/usr/bin/env python3
"""Tiny dependency-free GLB -> OBJ converter used for the exact user document model.

It intentionally supports the standard triangle-mesh subset needed by Fallen Forest: embedded GLB
buffers, indexed/non-indexed primitives, POSITION/NORMAL/TEXCOORD_0, node hierarchy transforms and
basic base-color/normal material texture links. No geometry is simplified or substituted.
"""
from __future__ import annotations

import json
import math
import os
from pathlib import Path
import struct
from typing import Iterable

JSON_CHUNK = 0x4E4F534A
BIN_CHUNK = 0x004E4942

_COMPONENT_FORMAT = {
    5120: "b",   # BYTE
    5121: "B",   # UNSIGNED_BYTE
    5122: "h",   # SHORT
    5123: "H",   # UNSIGNED_SHORT
    5125: "I",   # UNSIGNED_INT
    5126: "f",   # FLOAT
}
_COMPONENT_COUNT = {
    "SCALAR": 1,
    "VEC2": 2,
    "VEC3": 3,
    "VEC4": 4,
    "MAT2": 4,
    "MAT3": 9,
    "MAT4": 16,
}


def _identity() -> list[list[float]]:
    return [
        [1.0, 0.0, 0.0, 0.0],
        [0.0, 1.0, 0.0, 0.0],
        [0.0, 0.0, 1.0, 0.0],
        [0.0, 0.0, 0.0, 1.0],
    ]


def _mul(a: list[list[float]], b: list[list[float]]) -> list[list[float]]:
    return [[sum(a[r][k] * b[k][c] for k in range(4)) for c in range(4)] for r in range(4)]


def _gltf_matrix(values: Iterable[float]) -> list[list[float]]:
    values = list(values)
    if len(values) != 16:
        raise ValueError("glTF node matrix must contain 16 values")
    # glTF stores matrices column-major.
    return [[float(values[c * 4 + r]) for c in range(4)] for r in range(4)]


def _node_local_matrix(node: dict) -> list[list[float]]:
    if "matrix" in node:
        return _gltf_matrix(node["matrix"])

    tx, ty, tz = node.get("translation", [0.0, 0.0, 0.0])
    x, y, z, w = node.get("rotation", [0.0, 0.0, 0.0, 1.0])
    sx, sy, sz = node.get("scale", [1.0, 1.0, 1.0])

    rotation = [
        [1 - 2 * y * y - 2 * z * z, 2 * x * y - 2 * z * w, 2 * x * z + 2 * y * w, 0.0],
        [2 * x * y + 2 * z * w, 1 - 2 * x * x - 2 * z * z, 2 * y * z - 2 * x * w, 0.0],
        [2 * x * z - 2 * y * w, 2 * y * z + 2 * x * w, 1 - 2 * x * x - 2 * y * y, 0.0],
        [0.0, 0.0, 0.0, 1.0],
    ]
    scale = [
        [sx, 0.0, 0.0, 0.0],
        [0.0, sy, 0.0, 0.0],
        [0.0, 0.0, sz, 0.0],
        [0.0, 0.0, 0.0, 1.0],
    ]
    translation = _identity()
    translation[0][3] = tx
    translation[1][3] = ty
    translation[2][3] = tz
    return _mul(translation, _mul(rotation, scale))


def _transform_point(matrix: list[list[float]], value: Iterable[float]) -> tuple[float, float, float]:
    x, y, z = value
    source = (x, y, z, 1.0)
    return tuple(sum(matrix[r][k] * source[k] for k in range(4)) for r in range(3))


def _transform_normal(matrix: list[list[float]], value: Iterable[float]) -> tuple[float, float, float]:
    # The supplied document hierarchy uses rotations/translations only. This normalized 3x3
    # transform also behaves correctly for uniform scale, which is sufficient for this source.
    x, y, z = value
    out = tuple(matrix[r][0] * x + matrix[r][1] * y + matrix[r][2] * z for r in range(3))
    length = math.sqrt(sum(v * v for v in out)) or 1.0
    return tuple(v / length for v in out)


def _clean_name(value: str, fallback: str) -> str:
    value = (value or fallback).strip().replace(" ", "_")
    return "".join(ch if ch.isalnum() or ch in "_-+." else "_" for ch in value)


def _read_glb(path: Path) -> tuple[dict, bytes]:
    raw = path.read_bytes()
    if len(raw) < 20:
        raise ValueError(f"GLB is too small: {path}")
    magic, version, total = struct.unpack_from("<4sII", raw, 0)
    if magic != b"glTF" or version != 2:
        raise ValueError(f"Only GLB 2.0 is supported: {path}")
    if total > len(raw):
        raise ValueError(f"Truncated GLB: declared {total} bytes, got {len(raw)}")

    document = None
    binary = None
    offset = 12
    while offset + 8 <= total:
        length, chunk_type = struct.unpack_from("<II", raw, offset)
        offset += 8
        chunk = raw[offset:offset + length]
        offset += length
        if chunk_type == JSON_CHUNK:
            document = json.loads(chunk.decode("utf-8").rstrip("\x00 \t\r\n"))
        elif chunk_type == BIN_CHUNK:
            binary = chunk

    if document is None or binary is None:
        raise ValueError("GLB must contain both JSON and BIN chunks")
    if len(document.get("buffers", [])) != 1:
        raise ValueError("Fallen Forest converter expects one embedded GLB buffer")
    return document, binary


def _accessor_reader(document: dict, binary: bytes):
    def read(index: int) -> list[tuple]:
        accessor = document["accessors"][index]
        if accessor.get("sparse"):
            raise ValueError("Sparse GLB accessors are not supported")
        if "bufferView" not in accessor:
            raise ValueError("Accessor without bufferView is not supported")

        component_type = accessor["componentType"]
        component_format = _COMPONENT_FORMAT.get(component_type)
        if component_format is None:
            raise ValueError(f"Unsupported GLB component type: {component_type}")
        component_count = _COMPONENT_COUNT.get(accessor["type"])
        if component_count is None:
            raise ValueError(f"Unsupported GLB accessor type: {accessor['type']}")

        view = document["bufferViews"][accessor["bufferView"]]
        if view.get("buffer", 0) != 0:
            raise ValueError("Only the embedded primary GLB buffer is supported")

        fmt = "<" + component_format * component_count
        packed_size = struct.calcsize(fmt)
        stride = int(view.get("byteStride", packed_size))
        start = int(view.get("byteOffset", 0)) + int(accessor.get("byteOffset", 0))
        count = int(accessor["count"])
        end_needed = start + max(0, count - 1) * stride + packed_size
        if start < 0 or end_needed > len(binary):
            raise ValueError(f"Accessor {index} exceeds GLB binary chunk")

        return [struct.unpack_from(fmt, binary, start + i * stride) for i in range(count)]

    return read


def _world_matrices(document: dict) -> dict[int, list[list[float]]]:
    nodes = document.get("nodes", [])
    result: dict[int, list[list[float]]] = {}

    def walk(index: int, parent: list[list[float]]) -> None:
        local = _node_local_matrix(nodes[index])
        world = _mul(parent, local)
        result[index] = world
        for child in nodes[index].get("children", []):
            walk(int(child), world)

    scenes = document.get("scenes", [])
    if scenes:
        scene_index = int(document.get("scene", 0))
        roots = scenes[scene_index].get("nodes", [])
    else:
        children = {int(c) for n in nodes for c in n.get("children", [])}
        roots = [i for i in range(len(nodes)) if i not in children]

    for root in roots:
        walk(int(root), _identity())
    return result


def _find_embedded_texture(texture_dir: Path, image_index: int) -> Path | None:
    candidates = sorted(texture_dir.glob(f"gltf_embedded_{image_index}.*"))
    candidates = [p for p in candidates if "@channels=" not in p.name]
    return candidates[0] if candidates else None


def _write_mtl(document: dict, mtl_path: Path, obj_dir: Path, texture_dir: Path | None) -> list[str]:
    names: list[str] = []
    lines = ["# Fallen Forest document materials generated from exact user GLB"]
    textures = document.get("textures", [])

    for index, material in enumerate(document.get("materials", [])):
        name = f"material_{index}"
        names.append(name)
        lines.extend([f"newmtl {name}", "Kd 1 1 1", "Ks 0.04 0.04 0.04", "Ns 24"])

        if texture_dir is not None:
            pbr = material.get("pbrMetallicRoughness", {})
            base_ref = pbr.get("baseColorTexture", {}).get("index")
            normal_ref = material.get("normalTexture", {}).get("index")

            if base_ref is not None and int(base_ref) < len(textures):
                image_index = int(textures[int(base_ref)].get("source", -1))
                texture = _find_embedded_texture(texture_dir, image_index)
                if texture is not None:
                    rel = os.path.relpath(texture, obj_dir).replace(os.sep, "/")
                    lines.append(f"map_Kd {rel}")

            if normal_ref is not None and int(normal_ref) < len(textures):
                image_index = int(textures[int(normal_ref)].get("source", -1))
                texture = _find_embedded_texture(texture_dir, image_index)
                if texture is not None:
                    rel = os.path.relpath(texture, obj_dir).replace(os.sep, "/")
                    lines.append(f"map_Bump {rel}")
        lines.append("")

    mtl_path.parent.mkdir(parents=True, exist_ok=True)
    mtl_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return names


def convert_glb_to_obj(glb_path: Path, obj_path: Path, texture_dir: Path | None = None) -> Path:
    """Convert exact GLB geometry to Unity-native OBJ and a neighboring MTL file."""
    glb_path = Path(glb_path)
    obj_path = Path(obj_path)
    texture_dir = Path(texture_dir) if texture_dir is not None else None
    document, binary = _read_glb(glb_path)
    read_accessor = _accessor_reader(document, binary)
    worlds = _world_matrices(document)

    obj_path.parent.mkdir(parents=True, exist_ok=True)
    mtl_path = obj_path.with_suffix(".mtl")
    material_names = _write_mtl(document, mtl_path, obj_path.parent, texture_dir)

    lines = [
        "# Fallen Forest exact document geometry converted from the user GLB",
        f"mtllib {mtl_path.name}",
    ]
    vertex_base = 0
    uv_base = 0
    normal_base = 0
    primitive_count = 0

    for node_index, node in enumerate(document.get("nodes", [])):
        if "mesh" not in node:
            continue
        world = worlds.get(node_index, _identity())
        mesh = document["meshes"][int(node["mesh"])]
        for primitive_index, primitive in enumerate(mesh.get("primitives", [])):
            mode = int(primitive.get("mode", 4))
            if mode != 4:
                raise ValueError(f"Only TRIANGLES primitives are supported; found mode {mode}")
            attributes = primitive.get("attributes", {})
            if "POSITION" not in attributes:
                raise ValueError("GLB mesh primitive has no POSITION accessor")

            positions = read_accessor(int(attributes["POSITION"]))
            uvs = read_accessor(int(attributes["TEXCOORD_0"])) if "TEXCOORD_0" in attributes else []
            normals = read_accessor(int(attributes["NORMAL"])) if "NORMAL" in attributes else []
            if uvs and len(uvs) != len(positions):
                raise ValueError("TEXCOORD_0 count does not match POSITION count")
            if normals and len(normals) != len(positions):
                raise ValueError("NORMAL count does not match POSITION count")

            if "indices" in primitive:
                indices = [int(v[0]) for v in read_accessor(int(primitive["indices"]))]
            else:
                indices = list(range(len(positions)))
            if len(indices) % 3:
                raise ValueError("Triangle index count is not divisible by 3")

            object_name = _clean_name(node.get("name", ""), f"mesh_{node_index}_{primitive_index}")
            lines.append(f"o {object_name}")
            for position in positions:
                x, y, z = _transform_point(world, position[:3])
                lines.append(f"v {x:.9g} {y:.9g} {z:.9g}")
            for uv in uvs:
                lines.append(f"vt {float(uv[0]):.9g} {float(uv[1]):.9g}")
            for normal in normals:
                x, y, z = _transform_normal(world, normal[:3])
                lines.append(f"vn {x:.9g} {y:.9g} {z:.9g}")

            material_index = primitive.get("material")
            if material_index is not None and int(material_index) < len(material_names):
                lines.append(f"usemtl {material_names[int(material_index)]}")

            def token(local_index: int) -> str:
                if local_index < 0 or local_index >= len(positions):
                    raise ValueError(f"GLB index {local_index} exceeds POSITION count {len(positions)}")
                vi = vertex_base + local_index + 1
                ti = uv_base + local_index + 1 if uvs else None
                ni = normal_base + local_index + 1 if normals else None
                if ti is not None and ni is not None:
                    return f"{vi}/{ti}/{ni}"
                if ni is not None:
                    return f"{vi}//{ni}"
                if ti is not None:
                    return f"{vi}/{ti}"
                return str(vi)

            for i in range(0, len(indices), 3):
                lines.append("f " + " ".join(token(index) for index in indices[i:i + 3]))

            vertex_base += len(positions)
            uv_base += len(uvs)
            normal_base += len(normals)
            primitive_count += 1

    if primitive_count == 0 or vertex_base == 0:
        raise ValueError("GLB did not contain any triangle geometry")

    obj_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return obj_path
