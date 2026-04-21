# QualCompareCLI JSON Configuration Schema

Complete reference for render job configuration in JSON format.

## File structure

```json
{
  "schemaVersion": "1.0",
  "blenderPath": "...",
  "renderScriptPath": "...",
  "inputDir": "...",
  "outputDir": "...",
  "objType": "...",
  "fileType": "...",
  "positionsType": "...",
  "nbViews": 0,
  "yPos": 0.0,
  "upAxis": "...",
  "ext": "...",
  "tempInputRoot": "...",
  "tempOutputRoot": "...",
  "render": { ... },
  "ply": { ... },
  "maxParallelism": 0,
  "prefetchToSSD": true
}
```

## Field reference

### Required fields

#### `blenderPath`
- **Type:** string
- **Description:** Full path to Blender executable
- **Examples:**
  - Linux: `/usr/bin/blender` or `/snap/bin/blender`
  - macOS: `/Applications/Blender.app/Contents/MacOS/blender`
  - Windows: `C:\Program Files\Blender Foundation\Blender 4.4\blender.exe`

#### `renderScriptPath`
- **Type:** string
- **Description:** Full path to `render_single.py` from QualCompare repository
- **Example:** `/path/to/QualCompare-public/obj2png/render_single.py`

#### `inputDir`
- **Type:** string
- **Description:** Directory containing 3D objects to render (searched recursively)
- **Must contain:** Files matching `objType` extension (`.obj` or `.ply`)

#### `outputDir`
- **Type:** string
- **Description:** Directory where rendered images and masks will be stored
- **Output layout:** `<outputDir>/<object_name>/views/view_N.png` and `<outputDir>/<object_name>/masks/mask_N.png`

### Optional fields (with defaults)

#### `objType`
- **Type:** string
- **Default:** `"obj"`
- **Allowed values:** `"obj"`, `"ply"`
- **Description:** 3D object format to render

#### `fileType`
- **Type:** string
- **Default:** `"everything"`
- **Allowed values:** `"everything"`, `"source"`, `"distorted"`
- **Description:** File filtering strategy based on folder names
  - `"everything"`: Render all files
  - `"source"`: Only files in paths containing `source`, `ref`, `reference`, or `src`
  - `"distorted"`: Only files NOT in source paths

#### `positionsType`
- **Type:** string
- **Default:** `"fibonacci"`
- **Allowed values:** `"fibonacci"`, `"yfixed"`, `"polyedric"`
- **Description:** Camera position sampling strategy
  - `"fibonacci"`: Spherical Fibonacci distribution (good coverage)
  - `"yfixed"`: Circular orbit at fixed height (human-level views)
  - `"polyedric"`: Regular polyhedron vertices (4/6/8/12/20 views only)

#### `nbViews`
- **Type:** number (integer)
- **Default:** `12`
- **Range:** 1-∞ (recommended: 4-20)
- **Description:** Number of viewpoints to render
- **Note:** For `polyedric`, must match one of: 4, 6, 8, 12, 20

#### `yPos`
- **Type:** number (float)
- **Default:** `0.0`
- **Description:** Y-axis offset for camera in `yfixed` mode
- **Only used when:** `positionsType` is `"yfixed"`

#### `upAxis`
- **Type:** string
- **Default:** `"Y"`
- **Allowed values:** `"X"`, `"Y"`, `"Z"`
- **Description:** Object up-axis before normalization

#### `ext`
- **Type:** string
- **Default:** `"png"`
- **Allowed values:** `"png"`, `"jpg"`
- **Description:** Output image format

#### `tempInputRoot` and `tempOutputRoot`
- **Type:** string
- **Default:** (system temp directory, e.g., `/tmp/QualCompare/in` or `C:\Users\...\AppData\Local\Temp\QualCompare\out`)
- **Description:** Temporary staging directories (Phase 1 does not use these; Phase 2 will)

#### `maxParallelism`
- **Type:** number (integer)
- **Default:** `0` (means CPU count / 4)
- **Description:** Maximum parallel jobs (Phase 1 is always sequential)

#### `prefetchToSSD`
- **Type:** boolean
- **Default:** `true`
- **Description:** Copy objects to SSD before rendering (Phase 1 does not use this; Phase 2 will)

### Render parameters object

#### `render`
Nested object controlling Blender rendering settings.

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

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `resX` | number | 650 | Horizontal resolution (pixels) |
| `resY` | number | 550 | Vertical resolution (pixels) |
| `engine` | string | `"BLENDER_EEVEE_NEXT"` | Render engine name |
| `taa` | number | 64 | Temporal anti-aliasing samples |
| `filterSize` | number | 1.5 | Pixel filter size |
| `maskThreshold` | number | 10 | Binary mask threshold (0-255) |
| `sunEnergy` | number | 5.0 | Main directional light intensity |
| `sunTheta` | number | 30.0 | Light azimuth angle (degrees, 0-360) |
| `sunPhi` | number | 50.0 | Light elevation angle (degrees, -90 to 90) |
| `bgColor` | string | `"#34322C"` | Background color (hex `#RRGGBB`) |

### PLY-specific parameters object

#### `ply`
Nested object for point cloud rendering options.

```json
"ply": {
  "mode": "sphere",
  "pointRadiusFraction": 0.003,
  "voxelBits": 10,
  "voxelRadiusMultiplier": 1.0
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `mode` | string | `"sphere"` | PLY visualization: `"sphere"`, `"surface"`, `"voxel"`, `"voxel_volume"` |
| `pointRadiusFraction` | number | 0.003 | Point radius as fraction of bounding box diagonal |
| `voxelBits` | number | 10 | Voxel quantization bits (5-20 recommended) |
| `voxelRadiusMultiplier` | number | 1.0 | Multiplier for voxel radius in volume mode |

## Examples

### Minimal configuration (OBJ, Linux)

```json
{
  "schemaVersion": "1.0",
  "blenderPath": "/usr/bin/blender",
  "renderScriptPath": "/home/user/QualCompare-public/obj2png/render_single.py",
  "inputDir": "/data/models",
  "outputDir": "/data/output"
}
```

All other fields use defaults. Renders OBJ files with Fibonacci 12 views.

### PLY voxel (Windows)

```json
{
  "schemaVersion": "1.0",
  "blenderPath": "C:\\Program Files\\Blender Foundation\\Blender 4.4\\blender.exe",
  "renderScriptPath": "C:\\Users\\myuser\\QualCompare-public\\obj2png\\render_single.py",
  "inputDir": "D:\\Datasets\\PointClouds",
  "outputDir": "D:\\Outputs\\Voxels",
  "objType": "ply",
  "positionsType": "fibonacci",
  "nbViews": 20,
  "ply": {
    "mode": "voxel",
    "voxelBits": 12,
    "voxelRadiusMultiplier": 1.2
  }
}
```

### Source/distorted paired rendering

```json
{
  "schemaVersion": "1.0",
  "blenderPath": "/usr/bin/blender",
  "renderScriptPath": "/path/to/render_single.py",
  "inputDir": "/data/DATASET_NAME",
  "outputDir": "/data/output/dataset_source",
  "fileType": "source",
  "positionsType": "fibonacci",
  "nbViews": 16
}
```

Run this twice with `"fileType": "distorted"` to render distorted variants separately.

## Tips and best practices

1. **Use absolute paths.** Relative paths can be confusing on different platforms.
2. **Path separators:** Use `/` on all platforms (even Windows). JSON handles this correctly.
3. **Test with verbose flag:** `qualcompare-cli --config config.json --verbose`
4. **Keep defaults unless needed.** Only override values you need to change.
5. **Validate before large runs.** Use a small test dataset first.
6. **Record your configuration.** Archive the JSON along with rendered outputs for reproducibility.

## Version history

- **1.0**: Initial schema, Phase 1 (rendering only)
- Future: Phase 2/3 will extend this schema (patchify params, parallel configs, etc.)

## Backward compatibility

When schema is extended, old configurations should continue to work with default values for new fields.
