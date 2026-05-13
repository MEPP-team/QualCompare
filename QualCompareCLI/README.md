# QualCompare CLI

Cross-platform command-line interface for batch rendering of 3D objects with Blender.

## Overview

**QualCompareCLI** is a .NET 8 console application that executes rendering jobs defined in JSON configuration files and can also run native patch extraction through the portable `patchify_c` API. It enables:

- **Cross-platform rendering**: Windows, Linux, macOS (Blender and Python dependencies required)
- **Batch processing**: Render multiple objects from a single configuration
- **Reproducible workflows**: Configuration as code (JSON) instead of UI-based workflows
- **Integration-friendly**: Can be scripted, automated, or called from other applications
- **Native patch extraction**: Patchify runs automatically after rendering by default; use `--no-patchify` to skip it, or `--patchify` to process an existing rendered image/object folder directly

## Building

### System Requirements

- .NET 8 SDK or later
- Blender 4.x installed separately (not bundled)
- Python with `cv2` and `numpy` in Blender's Python environment
- For the default render pipeline, and for `--patchify`, the native `patchify_c` library must be available on the system library search path next to the CLI or installed in a standard location

### Quick prerequisite checklist

Before the first test run, make sure you have:

1. `.NET 8`
2. `Blender 4.x`
3. `opencv-python` and `numpy` inside Blender's Python environment
4. `patchify_c` available if you want the default render pipeline to patchify outputs or if you want to use `--patchify`

For a quick first render-only test, you only need items 1 to 3 and you should pass `--no-patchify`. If you want the default full pipeline, install item 4 too.

### Platform-Specific Setup

#### Windows

1. **Install Blender 4.4+**
   - Download from [blender.org](https://www.blender.org)
   - Use default installation path or note your custom path

2. **Install OpenCV in Blender's Python**
   ```powershell
   cd "C:\Program Files\Blender Foundation\Blender 4.4\4.4\python\bin"
   .\python.exe -m ensurepip
   .\python.exe -m pip install --upgrade pip
   .\python.exe -m pip install opencv-python numpy
   ```
   (Adjust version number as needed)

3. **Build QualCompareCLI**
   ```powershell
   dotnet build
   dotnet publish -c Release -o ./bin/release
   ```
   Output: `bin\release\qualcompare-cli.exe`

#### Linux

1. **Install .NET 8, Blender, and dependencies**
  ```bash
  # Debian/Ubuntu example (after adding Microsoft's apt feed)
  sudo apt-get update
  sudo apt-get install dotnet-sdk-8.0 blender python3 python3-pip
  ```

2. **Install Blender and dependencies**
   
   ```bash
   # Ubuntu/Debian
   sudo apt-get update
   sudo apt-get install blender python3 python3-pip
   
   # Or use snap
   sudo snap install blender --classic
   ```
Be careful to get a version of Blender recommended above 4.4.

3. **Install Python packages in Blender's environment**
   
   ```bash
   # Get Blender Python executable
   BLENDER_PY=$(blender --background --python-expr "import sys; print(sys.executable)" 2>/dev/null | head -n 1)
   
   # Bootstrap pip and install packages
   "$BLENDER_PY" -m ensurepip --upgrade
   "$BLENDER_PY" -m pip install --upgrade pip
   "$BLENDER_PY" -m pip install opencv-python numpy
   ```

4. **Build QualCompareCLI**
   ```bash
   dotnet build
   dotnet publish -c Release -o ./bin/Release
   ```
   Output: `bin/release/qualcompare-cli`

#### macOS

1. **Install Blender and dependencies**
   ```bash
   # Using Homebrew
   brew install blender python
   
   # Or download from blender.org
   ```

2. **Install Python packages in Blender's environment**
   ```bash
   # Get Blender's Python executable
   BLENDER_PY=$(/Applications/Blender.app/Contents/MacOS/Blender --background --python-expr "import sys; print(sys.executable)" 2>/dev/null | head -n 1)

   # Bootstrap pip and install packages
   "$BLENDER_PY" -m ensurepip --upgrade
   "$BLENDER_PY" -m pip install --upgrade pip
   "$BLENDER_PY" -m pip install opencv-python numpy
   ```

3. **Build QualCompareCLI**
   ```bash
   dotnet build
   dotnet publish -c Release -o ./bin/release
   ```
   Output: `bin/release/qualcompare-cli`

#### WSL (Windows Subsystem for Linux)

If using WSL on Windows, follow the **Linux** setup above. Note that Blender must be available inside WSL (not just on Windows). For WSL2, you may also need to configure display forwarding if running with GUI. For headless rendering (recommended), WSL works seamlessly with the CLI. However, by testing we noticed slowness related to WSL and not the application itself.

The output binary will be:
- `qualcompare-cli` (Linux/macOS)
- `qualcompare-cli.exe` (Windows)

### Quick JSON bootstrap

The JSON files in `examples/` are templates. They still need machine-specific paths before you can run them directly. To fill one quickly, use the helper script below:

```bash
python3 scripts/fill_render_config_paths.py \
  --template examples/render_fibonacci_12views.json \
  --output /tmp/render.local.json \
  --input-dir /data/models \
  --output-dir /data/output
```

The script sets `blenderPath` from `blender` in `PATH` and resolves `renderScriptPath` from the repository layout. If Blender is not in `PATH`, pass `--blender-path /usr/bin/blender` (or your local executable path).

## Usage

### Basic usage

```bash
qualcompare-cli --config my_render_job.json
```

### With verbose logging

```bash
qualcompare-cli --config my_render_job.json --verbose
```

### Patchify control

Rendering runs patchify automatically by default. Use `--no-patchify` when you want to render only, or `--patchify` when you want to process an already-rendered image or folder.

```bash
qualcompare-cli --config my_render_job.json

qualcompare-cli --config my_render_job.json --no-patchify

qualcompare-cli --patchify /path/to/rendered/object
```

`--patchify` runs native patch extraction on rendered images or folders. It follows the same logic as the WPF desktop application but uses the portable C API instead of the C++/CLI bridge. `--no-patchify` skips the automatic post-render patch step.

**Prerequisites:**
- `patchify_c` library must be discoverable (see "Native library deployment" below)
- The native `patchify` components are built by default when configuring the repository with CMake. If you configured the native build with `-DBUILD_PATCHIFY=OFF`, the shared `patchify_c` library and `patchify_cli` will not be produced.
- Input folder must follow the standard structure:
  ```
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

**Output:**
- CSV files with patch coordinates and summaries written to the object folder
- Return code: 0 on success, 1 on error

**Example:**
```bash
# Single render job
qualcompare-cli --config my_render.json

# Then extract patches
qualcompare-cli --patchify ./output/my_object
```

### Native library deployment

The `--patchify` mode requires the `patchify_c` library at runtime. Ensure it is available in one of these ways:

**Option 1: Next to the CLI executable (recommended for development)**
```
bin/
  qualcompare-cli.exe      (or qualcompare-cli on Linux/macOS)
  patchify_c.dll           (Windows)
  libpatchify_c.so         (Linux)
  libpatchify_c.dylib      (macOS)
```

**Option 2: System library path**
- Windows: Add to `PATH`
- Linux: Add to `LD_LIBRARY_PATH` or use `ldconfig`
- macOS: Add to `DYLD_LIBRARY_PATH` or use `/usr/local/lib`

**Option 3: Custom location (for advanced deployments)**
Set environment variable before running:
```bash
# Windows
$env:QUALCOMPARE_PATCHIFY_PATH = "C:\path\to\patchify_c.dll"

# Linux/macOS
export QUALCOMPARE_PATCHIFY_PATH=/opt/patchify_c.so
qualcompare-cli --patchify ...
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
  "engine": "BLENDER_EEVEE",
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
| `engine` | string | "BLENDER_EEVEE" | Render engine |
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

# Render only, without automatic patch extraction
qualcompare-cli --config examples/render_fibonacci_12views.json --no-patchify
```

### Example 2: PLY with voxel rendering (Windows)

See `examples/render_ply_voxel_windows.json`

```bash
qualcompare-cli --config examples/render_ply_voxel_windows.json
```

### Test assets

The `sample_data/quick_test/source/` tree includes small fixtures that are useful for smoke tests:

- `sample_data/quick_test/source/Bread/source/` contains texture/material assets for a Bread sample.
- `sample_data/quick_test/source/baluster-vase-one-of-three-in-a-five-piece/source/` contains texture/material assets for a vase sample.

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

The CLI can also call the native patch extraction library directly through `--patchify`, which avoids the Windows-only C++/CLI wrapper for cross-platform consumers.

## Development notes

- **Configuration**: See `RenderConfig.cs` for the JSON schema mapping
- **Service**: See `BlenderRenderService.cs` for Blender process execution logic
- **Logging**: See `Logger.cs` for console output implementation

## Future enhancements

- richer configuration validation and diagnostics
- schema evolution/version migration support
