#!/usr/bin/env python3
"""
convert_models.py — Convert HeyMac's Core ML models to ONNX for the Windows port.

Usage:
    python convert_models.py <resources-dir> <output-dir>

<resources-dir> should contain the two .mlpkgdata folders:
    ArcFace.mlpkgdata/
    AntiSpoof.mlpkgdata/

Each .mlpkgdata/Data/com.apple.CoreML/model.mlmodel is extracted and converted
to ONNX via coremltools. Output files: arcface.onnx, antispoof.onnx.
"""

import os
import sys
import shutil
import tempfile
from pathlib import Path

try:
    import coremltools as ct
    import onnx
except ImportError as exc:
    sys.exit(f"Missing dependency: {exc}. Install coremltools and onnx first.")

MODELS = {
    "ArcFace": "arcface.onnx",
    "AntiSpoof": "antispoof.onnx",
}

def find_mlmodel(pkg_dir: Path) -> Path | None:
    """Locate model.mlmodel inside an .mlpkgdata bundle."""
    candidates = list(pkg_dir.rglob("model.mlmodel"))
    return candidates[0] if candidates else None


def convert(resources_dir: Path, output_dir: Path):
    output_dir.mkdir(parents=True, exist_ok=True)

    for name, out_name in MODELS.items():
        pkg = resources_dir / f"{name}.mlpkgdata"
        if not pkg.exists():
            print(f"[WARN] {pkg} not found — skipping {name}")
            continue

        mlmodel = find_mlmodel(pkg)
        if mlmodel is None:
            print(f"[WARN] no model.mlmodel inside {pkg} — skipping {name}")
            continue

        print(f"[INFO] Converting {name}: {mlmodel}")
        try:
            ml_model = ct.models.MLModel(str(mlmodel))
        except Exception as exc:
            # Fall back to loading the compiled ML package directly.
            try:
                ml_model = ct.models.MLModel(str(pkg))
            except Exception as exc2:
                print(f"[WARN] could not load {name}: {exc2} — skipping")
                continue

        try:
            # Convert the neural network to ONNX. coremltools exposes this
            # through the mil / converter API depending on version.
            converter = ct.converters.convert
            onnx_model = converter.convert(ml_model, target_format="onnx")
        except TypeError:
            # Older coremltools: mlmodel.convert_to('onnx')
            onnx_model = ml_model.convert_to("onnx")

        out_path = output_dir / out_name
        onnx_model.save(str(out_path))
        print(f"[OK] Wrote {out_path}")


def main(argv):
    if len(argv) != 3:
        sys.exit(f"Usage: {argv[0]} <resources-dir> <output-dir>")

    resources_dir = Path(argv[1])
    output_dir = Path(argv[2])

    if not resources_dir.is_dir():
        sys.exit(f"resources-dir not found: {resources_dir}")

    convert(resources_dir, output_dir)


if __name__ == "__main__":
    main(sys.argv)
