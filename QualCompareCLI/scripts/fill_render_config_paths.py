#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path


def discover_blender_python(blender_executable: str = "blender") -> str:
    command = [
        blender_executable,
        "--background",
        "--python-expr",
        "import sys; print(sys.executable)",
    ]
    completed = subprocess.run(command, capture_output=True, text=True, check=True)
    for line in completed.stdout.splitlines():
        line = line.strip()
        if line:
            return line
    raise RuntimeError("Unable to determine Blender's Python executable")


def resolve_repo_root(script_path: Path) -> Path:
    return script_path.resolve().parents[2]


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Fill a QualCompareCLI JSON template with local paths."
    )
    parser.add_argument("--template", required=True, help="Input JSON template")
    parser.add_argument("--output", help="Output JSON file. Defaults to stdout.")
    parser.add_argument("--blender-path", help="Absolute path to Blender. If omitted, discover it from PATH.")
    parser.add_argument("--render-script-path", help="Override render_single.py path.")
    parser.add_argument("--input-dir", help="Override inputDir.")
    parser.add_argument("--output-dir", help="Override outputDir.")
    parser.add_argument("--repo-root", help="Override repository root used to resolve render_single.py.")
    args = parser.parse_args()

    template_path = Path(args.template)
    data = json.loads(template_path.read_text(encoding="utf-8"))

    if args.blender_path:
        blender_path = args.blender_path
    else:
        blender_path = discover_blender_python()

    repo_root = Path(args.repo_root) if args.repo_root else resolve_repo_root(Path(__file__))
    render_script_path = args.render_script_path or str(repo_root / "obj2png" / "render_single.py")

    data["blenderPath"] = blender_path
    data["renderScriptPath"] = render_script_path
    if args.input_dir:
        data["inputDir"] = args.input_dir
    if args.output_dir:
        data["outputDir"] = args.output_dir

    rendered = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        Path(args.output).write_text(rendered, encoding="utf-8")
    else:
        sys.stdout.write(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
