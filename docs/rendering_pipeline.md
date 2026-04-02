# Rendering pipeline

Rendering is executed through Blender in background mode from the WPF application.

Current command shape:

```text
blender --background --python render_single.py -- <arguments>
```

The command line is assembled in `QualCompare/RenderQueue.cs` and executed with the configured Blender executable and render script path.

---

## High-level workflow

For each selected 3D file:

1. discover the input object from the dataset folder
2. copy the object to the temporary SSD input cache
3. copy related material / texture files when needed
4. launch Blender in background mode
5. import and normalize the object in `render_single.py`
6. generate camera positions
7. render color views
8. render temporary masks
9. threshold masks with OpenCV
10. copy the cached render output back to the final output folder

The application processes one render job at a time, but multiple objects inside a job can be rendered in parallel.

---

## Input parameters passed from the GUI

The current Blender script receives more parameters than the old documentation suggested.

Core inputs:

- `--obj`
- `--out`
- `--nb_views`
- `--positions_type`
- `--ext`
- `--file_type`
- `--obj_type`
- `--ypos`
- `--up_axis`

Rendering parameters:

- `--resx`
- `--resy`
- `--engine`
- `--taa`
- `--filter_size`
- `--mask_threshold`
- `--sun_energy`
- `--sun_theta`
- `--sun_phi`
- `--bg_color`

PLY-specific parameters:

- `--point_radius_fraction`
- `--ply_render`
- `--ply_voxel_bits`
- `--voxel_radius_multiplier`

These arguments must remain stable because the GUI builds the command line explicitly.

---

## Scene preparation

In `obj2png/render_single.py`, the pipeline starts by:

1. clearing the current Blender scene
2. importing the target OBJ or PLY
3. setting render resolution and engine
4. configuring color management
5. setting Eevee sampling / filter options
6. setting the world background color
7. moving the object origin to geometry bounds

After import, the object is:

- scaled to a normalized size
- centered
- optionally rotated according to the selected up-axis

Supported up-axis values:

- `X`
- `Y`
- `Z`

---

## Camera and light setup

The script creates:

- one tracked camera
- one sun light

The light orientation is controlled by:

- `sun_theta`
- `sun_phi`
- `sun_energy`

The camera is placed at a fixed radius relative to the normalized object and its FOV is then adjusted automatically.

---

## View sampling

Camera positions are generated in `obj2png/positions.py`.

Current supported position families are:

- `fibonacci`
- `yfixed`
- `polyedric`

Important naming note:

- the Python CLI expects `polyedric`
- some UI labels and older docs refer to `Polyhedral`

The render script converts sampled virtual camera positions into object rotations while keeping the actual Blender camera fixed relative to the object.

---

## FOV computation

FOV is not simply based on a raw bounding box anymore.

Current behavior:

- for standard rendering, FOV is fitted from a bounding sphere estimate
- for PLY, the script may recompute the radius using a bounding sphere variant to reduce missing parts / holes

The key helper is `set_camera_fov_to_fit_sphere`.

This is a more accurate description than the old "bounding box only" summary.

---

## Rendering of views

For each sampled view:

1. the object rotation is updated
2. a color render is produced with the selected render engine
3. output is written to `views/view_N.<ext>`

Typical defaults currently defined in code are:

- resolution: `650 x 550`
- engine: `BLENDER_EEVEE_NEXT`
- TAA samples: `64`
- filter size: `1.0` in Python defaults, `1.5` in the current C# config defaults
- sun theta: `30`
- sun phi: `50`
- sun energy: `5.0`
- background color: `#34322C`

So the practical defaults depend on whether the script is run directly or launched through the current GUI configuration layer.

---

## Mask generation

Masks are generated for every rendered view using a second render pass.

Current process:

1. switch engine to `BLENDER_WORKBENCH`
2. use flat lighting
3. render the object in white
4. render the world background in black
5. save a temporary grayscale mask image
6. threshold it with OpenCV
7. save the final binary mask to `masks/mask_N.<ext>`
8. delete the temporary mask file

So the OpenCV post-processing step is part of the official pipeline, not an external post-pass.

---

## Supported formats

Currently supported object formats:

- `.obj`
- `.ply`

OBJ notes:

- material and texture dependencies are preserved during SSD prefetch when possible

PLY notes:

- geometry nodes are used for rendering
- the current script supports multiple visualization modes, not only a single point mode

Current PLY modes:

- `sphere`
- `surface`
- `voxel`
- `voxel_volume`

---

## Output layout

Per-object output layout:

```text
object_name/
    views/
    masks/
```

Typical file naming:

- `view_1.png`
- `mask_1.png`

The parent output folder is determined by the GUI job configuration and can be auto-generated from dataset / method / file-type / number-of-views metadata.

