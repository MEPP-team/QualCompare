# Datasets and protocols

The repository is designed to work with research datasets of 3D objects, but there is no single strict dataset schema enforced in code.

Instead, the current application relies on a mix of:

- recursive file discovery by extension
- folder naming conventions
- output naming conventions
- source / distorted path heuristics

This means dataset compatibility is partly convention-based rather than fully formalized.

---

## Input datasets

The render queue scans the selected input directory recursively and looks for files matching the selected object format:

- `.obj`
- `.ply`

The user can then filter processing as:

- `everything`
- `source`
- `distorted`

The current source / distorted classification is heuristic.

In `RenderQueue.cs`, a file is considered source-like when one of its path segments matches or starts with keywords such as:

- `source`
- `ref`
- `reference`
- `src`

Any matching limitations in dataset naming can therefore affect experiment selection.

---

## Typical dataset families

The project documentation mentions datasets such as:

- TMQ
- TSMD
- SJTU-TMQA
- BASICS
- custom internal datasets

From the current codebase, dataset identity is mostly inferred from folder paths rather than from a dedicated metadata layer.

---

## Expected content per object

Depending on the dataset and object type, an input object may involve:

- a mesh or point cloud file
- an OBJ material file (`.mtl`)
- referenced texture files

For OBJ inputs, the SSD prefetch step attempts to preserve the local material / texture structure by copying:

- the OBJ file
- its referenced MTL file
- the texture files referenced inside that MTL

For PLY inputs, rendering depends on the geometry contained in the file and on the selected PLY visualization mode.

---

## Rendering protocol conventions

The current rendering protocol is controlled by the GUI and passed to `obj2png/render_single.py`.

Main experimental variables include:

- number of views
- view sampling method
- camera height for `yfixed`
- object format: OBJ or PLY
- render resolution
- render engine
- anti-aliasing and filter settings
- light energy and orientation
- background color
- PLY rendering mode and voxel settings

Current view sampling families are:

- `fibonacci`
- `yfixed`
- `polyedric`

---

## Output protocol

Per-object output layout is currently:

```text
object_name/
    views/
    masks/
```

Typical filenames are:

- `views/view_1.png`
- `masks/mask_1.png`

At a higher level, the application can auto-generate experiment folders with a structure similar to:

```text
<DefaultOutputRoot>/<Dataset>/<RenderFamilyName>/<Method>/<FileType>/<NbViews>VP
```

This higher-level layout is important because downstream patch extraction and experiment comparison rely on stable directory conventions.

---

## Patch extraction protocol

Patch extraction is applied after rendering, not during rendering.

Current behavior:

- Patchify reads rendered images from `views/`
- it derives the associated mask path from the sibling `masks/` folder
- it keeps patch locations whose overlap with the object mask exceeds a threshold
- it writes CSV outputs containing coordinates and per-view summaries

This protocol is used to prepare training or evaluation inputs for downstream perceptual models.

---

## Current limitations of dataset formalization

The repository does not currently provide:

- a single formal dataset manifest format
- a unified metadata file for all datasets
- a strict validation layer for source / distorted pairing
- a generalized experiment protocol description stored in machine-readable form

So in practice, successful use of the application still depends on keeping dataset folder structures and file naming conventions consistent with lab usage.
