# QualCompare CLI

Cross-platform command-line interface for batch rendering of 3D objects with Blender.

## Overview

**QualCompareCLI** is a .NET 8 console application that executes rendering jobs defined in JSON configuration files. It enables:

- **Cross-platform rendering**: Windows, Linux, macOS (Blender and Python dependencies required)
- **Batch processing**: Render multiple objects from a single configuration
- **Reproducible workflows**: Configuration as code (JSON) instead of UI-based workflows
- **Integration-friendly**: Can be scripted, automated, or called from other applications

## Building

### Requirements

- .NET 8 SDK or later
- Blender 4.x installed separately (not bundled)
- Python with `cv2` and `numpy` in Blender's Python environment

### First-time Linux/WSL setup (recommended)

On a fresh Linux/WSL environment, install OpenCV and NumPy in Blender's Python before the first render.

```bash
# 1) Get Blender's Python executable path
BLENDER_PY=$(blender --background --python-expr "import sys; print('PY_EXE=' + sys.executable)" 2>/dev/null | sed -n 's/^PY_EXE=//p' | head -n 1)

# 2) Bootstrap pip and install required packages
"$BLENDER_PY" -m ensurepip --upgrade
"$BLENDER_PY" -m pip install --upgrade pip
"$BLENDER_PY" -m pip install opencv-python numpy

# 3) Verify imports from Blender runtime
blender --background --python-expr "import cv2, numpy; print('cv2', cv2.__version__)"
```

### Build commands

```bash
# Restore packages and build
dotnet build

# Build release binary
dotnet publish -c Release -o ./bin/release
```

The output binary will be:
- `qualcompare-cli` (Linux/macOS)
- `qualcompare-cli.exe` (Windows)

## Usage

### Basic usage

```bash
qualcompare-cli --config my_render_job.json
```

### With verbose logging

```bash
qualcompare-cli --config my_render_job.json --verbose
```

### Help

```bash
qualcompare-cli --help
```

## Configuration (JSON Schema)

Create a JSON configuration file describing your render job. See `examples/` for templates.

### Top-level structure

```json
{
  "schemaVersion": "1.0",
  "blenderPath": "/path/to/blender",
  "renderScriptPath": "/path/to/render_single.py",
  "inputDir": "/data/models",
  "outputDir": "/data/output",
  "objType": "obj",
  "fileType": "everything",
  "positionsType": "fibonacci",
  "nbViews": 12,
  "upAxis": "Y",
  "ext": "png",
  "render": { ... },
  "ply": { ... }
}
```

### Key properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `blenderPath` | string | required | Full path to `blender` or `blender.exe` |
| `renderScriptPath` | string | required | Full path to `render_single.py` |
| `inputDir` | string | required | Directory containing 3D objects |
| `outputDir` | string | required | Directory where renders will be saved |
| `objType` | string | "obj" | Object format: `"obj"` or `"ply"` |
| `fileType` | string | "everything" | File selection: `"everything"`, `"source"`, or `"distorted"` |
| `positionsType` | string | "fibonacci" | Camera sampling: `"fibonacci"`, `"yfixed"`, or `"polyedric"` |
| `nbViews` | number | 12 | Number of viewpoints to render |
| `maxParallelism` | number | 0 | Max concurrent Blender jobs (`0` => `CPU/4`) |
| `yPos` | number | 0.0 | Y-axis offset for `yfixed` mode |
| `upAxis` | string | "Y" | Object up-axis: `"X"`, `"Y"`, or `"Z"` |
| `ext` | string | "png" | Output format: `"png"` or `"jpg"` |

### Render parameters

The `render` object controls Blender rendering:

```json
"render": {
  "resX": 650,
  "resY": 550,
  "engine": "BLENDER_EEVEE_NEXT",
  "taa": 64,
  "filterSize": 1.5,
  "maskThreshold": 10,
  "sunEnergy": 5.0,
  "sunTheta": 30.0,
  "sunPhi": 50.0,
  "bgColor": "#34322C"
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `resX` | number | 650 | Horizontal resolution (pixels) |
| `resY` | number | 550 | Vertical resolution (pixels) |
| `engine` | string | "BLENDER_EEVEE_NEXT" | Render engine |
| `taa` | number | 64 | Temporal anti-aliasing samples |
| `filterSize` | number | 1.5 | Pixel filter size |
| `maskThreshold` | number | 10 | Binary mask threshold (0-255) |
| `sunEnergy` | number | 5.0 | Main light intensity |
| `sunTheta` | number | 30.0 | Light azimuth angle (degrees) |
| `sunPhi` | number | 50.0 | Light elevation angle (degrees) |
| `bgColor` | string | "#34322C" | Background color (hex) |

### PLY-specific parameters

The `ply` object controls point cloud rendering:

```json
"ply": {
  "mode": "sphere",
  "pointRadiusFraction": 0.003,
  "voxelBits": 10,
  "voxelRadiusMultiplier": 1.0
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `mode` | string | "sphere" | PLY rendering mode: `"sphere"`, `"surface"`, `"voxel"`, or `"voxel_volume"` |
| `pointRadiusFraction` | number | 0.003 | Sphere radius as fraction of object size |
| `voxelBits` | number | 10 | Voxel quantization bits (for voxel modes) |
| `voxelRadiusMultiplier` | number | 1.0 | Voxel radius multiplier |

## Examples

### Example 1: OBJ with Fibonacci sampling (Linux/macOS)

See `examples/render_fibonacci_12views.json`

```bash
qualcompare-cli --config examples/render_fibonacci_12views.json
```

### Example 2: PLY with voxel rendering (Windows)

See `examples/render_ply_voxel_windows.json`

```bash
qualcompare-cli --config examples/render_ply_voxel_windows.json
```

## Output structure

For each rendered object, the CLI creates:

```
<outputDir>/
  object_name/
    views/
      view_1.png
      view_2.png
      ...
    masks/
      mask_1.png
      mask_2.png
      ...
```

This structure is **stable** and matches the GUI output. Downstream tools (like patchify) depend on this layout.

## Troubleshooting

### "Blender executable not found"

Check the `blenderPath` in your JSON configuration. Examples:

- **Linux**: `/usr/bin/blender` or `/snap/bin/blender`
- **macOS**: `/Applications/Blender.app/Contents/MacOS/blender`
- **Windows**: `C:\Program Files\Blender Foundation\Blender 4.4\blender.exe`

### "Render script not found"

Ensure `renderScriptPath` points to the correct location. It should be:
```
/path/to/QualCompare-public/obj2png/render_single.py
```

### "No objects found matching the configured criteria"

Check that:
1. `inputDir` contains files matching the extension in `objType` (`.obj` or `.ply`)
2. If `fileType` is `"source"` or `"distorted"`, the folder names contain keywords like `source`, `ref`, `reference`, or `src`

### Blender Python `cv2` import error

Install OpenCV in Blender's Python:

```bash
# Linux/macOS
/path/to/blender/python/bin/python -m pip install opencv-python

# Windows
"C:\Program Files\Blender Foundation\Blender 4.4\4.4\python\bin\python.exe" -m pip install opencv-python
```

## Integration with GUI

The GUI (Windows WPF) can export its current settings as a JSON configuration file, which the CLI can then consume. This ensures:
- Single source of truth for rendering parameters
- Reproducibility across platforms
- Easier batch automation

## Development notes

- **Configuration**: See `RenderConfig.cs` for the JSON schema mapping
- **Service**: See `BlenderRenderService.cs` for Blender process execution logic
- **Logging**: See `Logger.cs` for console output implementation

## Future enhancements (Phase 2 & 3)

- GUI export to JSON
- Configuration validation with detailed error messages
- Parallel object rendering with SSD staging
- Patchify integration (currently Phase 1 focuses on rendering only)
- Configuration schema versioning and migration
