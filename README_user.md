# README_user.md

# QualCompare

QualCompare is a Windows desktop application used to render reproducible multi-view images of 3D objects and generate image patches for perceptual quality assessment workflows.

It is designed for research usage on meshes and point clouds, with a practical GUI that helps users prepare rendered datasets without launching Blender manually for every object.

---

## 5-minute quick start

Use this section if you want to check quickly that QualCompare works on your machine.

1. Install Blender 4.x on Windows. (4.4+ recommended)
2. Launch QualCompare.
3. If asked, select your `blender.exe`.
4. Choose a small folder containing one `.obj` file.
5. Choose an empty output folder.
6. Select:
   - format: `obj`
   - file selection: `everything` or `source`
   - method: `Fibonacci`
   - number of views: `4`
7. Start rendering.
8. Open the output folder and confirm you have:

```text
object_name/
    views/
    masks/
```

9. Open the Patchify area and run patch extraction on one rendered image or folder.
10. Confirm that patch CSV output is created.

If this quick test works, the application is ready for larger datasets and longer experiments.

---

## What the software does

QualCompare can:

- render multiple views of `.obj` meshes and `.ply` point clouds
- generate binary masks for each rendered view
- organize outputs in a stable folder layout
- extract image patches for training and evaluation workflows

Typical use cases:

- preparing view-based datasets for 3D quality assessment
- generating inputs for LPIPS or Graphics-LPIPS style pipelines
- building reproducible rendering protocols for research experiments

---

## System requirements

- Windows 10 or Windows 11
- Blender 4.4 or higher installed on the machine
- .NET Framework 4.8.1 available
- enough free disk space for rendered outputs and temporary files

Recommended for large datasets:

- SSD or NVMe storage
- 16 GB RAM or more
- multi-core CPU
- dedicated GPU if available

Important:

- Blender is required and is not bundled with the application.
- Some Blender Python dependencies may be needed for the render script, especially `cv2`.

---

## Installation

## Option 1 - Recommended: install with the Windows setup

If you received a release package:

1. Run the QualCompare installer.
2. Follow the installation wizard.
3. Launch QualCompare from the Start Menu or desktop shortcut.

On first launch, the application creates its configuration automatically.

If Blender is already installed, QualCompare may detect it automatically. If not, you will be asked to select `blender.exe` manually in the application settings.

## Option 2 - Run from a development build

If you are using a locally built version:

1. Build the Visual Studio solution.
2. Open the generated `QualCompare.exe`.
3. On first launch, let the application create its initial configuration.
4. Verify that Blender and the bundled render script are correctly detected.

---

## First launch and initial configuration

When QualCompare starts for the first time, it creates a configuration file in:

```text
%AppData%\QualCompare\settings.json
```

Before rendering, verify these items in the application:

- Blender executable path
- render script path
- temporary input folder
- temporary output folder
- default output folder

If Blender is not detected automatically:

1. Open the settings area in the application.
2. Browse to your Blender installation.
3. Select `blender.exe`.
4. Save the settings.

Typical Blender path:

```text
C:\Program Files\Blender Foundation\Blender 4.x\blender.exe
```

---

## If Blender Python is missing `cv2`

The rendering pipeline uses OpenCV inside Blender's Python environment for mask post-processing.

If rendering fails because `cv2` is missing, install it into Blender's Python environment. A typical Windows procedure is:

```powershell
cd "C:\Program Files\Blender Foundation\Blender 4.4\4.4\python\bin"
.\python.exe -m ensurepip
.\python.exe -m pip install --upgrade pip
.\python.exe -m pip install --force-reinstall opencv-python
```

Adjust the Blender version in the path if needed.

If this still does not work, ask the maintainer or developer team for the Blender dependency setup used in your lab environment.

---

## Supported input formats

QualCompare currently supports:

- `.obj` meshes
- `.ply` point clouds

For OBJ datasets, keep these files together when needed:

- `.obj`
- `.mtl`
- texture images referenced by the material

If the textures or material file are missing, renders may succeed but appear incorrect.

---

## Expected dataset structure

The application scans folders recursively, so the dataset does not need one single rigid schema. Still, the most reliable layout is a dataset where each object has clearly separated reference and distorted content.

Typical example:

```text
Dataset/
    object1/
        source/
            model.obj
        distorted/
            model_dist.obj
    object2/
        source/
        distorted/
```

QualCompare can filter files as:

- everything
- source only
- distorted only

Important:

The current source/distorted detection is heuristic and based on folder names such as `source`, `ref`, `reference`, or `src`. If your dataset does not follow similar names, the filtering may not match your expectations.

---

## Quick start

If you just want to confirm that the software works:

1. Install Blender.
2. Launch QualCompare.
3. Set the Blender path if needed.
4. Choose a small input dataset.
5. Choose an output folder.
6. Render a few views of one OBJ file.
7. Check that both `views` and `masks` were created.
8. Run Patchify on the result.

---

## Step-by-step rendering guide

## 1. Select the input folder

Choose the folder that contains the 3D objects you want to process.

The application will search recursively for files matching the selected format.

## 2. Select the output folder

Choose the folder where rendered images and masks should be stored.

QualCompare writes results per object, so the chosen output folder should have enough free disk space.

## 3. Choose the file format

Select the object type you want to render:

- `obj`
- `ply`

## 4. Choose which files to process

Available modes:

- `everything`
- `source`
- `distorted`

Use `everything` if you are unsure whether the dataset folder names follow the expected conventions.

## 5. Choose the view generation method

### Fibonacci

Uses a spherical distribution of views.

Best for:

- broad coverage of object appearance
- unbiased multi-view evaluation

### Y-fixed

Moves the camera around the object at a fixed height.

Best for:

- human-facing experiments
- circular observation protocols

### Polyhedral

Uses predefined camera positions on regular polyhedra.

Available view counts:

| Shape | Views |
| --- | --- |
| Tetrahedron | 4 |
| Octahedron | 6 |
| Cube | 8 |
| Icosahedron | 12 |
| Dodecahedron | 20 |

## 6. Set the number of views

Enter the number of views you want to generate.

Notes:

- Fibonacci and Y-fixed accept user-defined view counts.
- Polyhedral uses predefined counts only.
- More views mean more rendering time and more disk usage.

## 7. Start rendering

Click the render button.

The application will then:

1. copy each object to a temporary SSD-friendly cache
2. launch Blender in background mode
3. generate rendered views
4. generate binary masks
5. copy final outputs to the selected output folder

---

## Output structure

For each rendered object, QualCompare creates:

```text
object_name/
    views/
        view_1.png
        view_2.png
    masks/
        mask_1.png
        mask_2.png
```

This structure is important because patch extraction expects the `masks/` folder to match the `views/` folder.

---

## Patchify guide

Patchify extracts image patches from rendered views after rendering is complete.

Typical use cases:

- neural network training
- metric evaluation
- dataset preparation

QualCompare supports:

- single image patchification
- folder-based patchification

Basic workflow:

1. Open the Patchify area in the application.
2. Choose either one rendered image or a rendered folder.
3. Start patch extraction.
4. Wait for CSV outputs and patch data to be generated.

Patchify relies on the existing render structure, especially the sibling `views/` and `masks/` folders.

---

## Recommended usage tips

- Use SSD storage whenever possible.
- Start with a small subset of the dataset before launching a full experiment.
- Check one or two rendered objects before processing the entire dataset.
- Keep OBJ, MTL, and texture files together.
- Use a modest number of views for quick validation runs.
- If rendering is slow, reduce the number of views before changing other parameters.

---

## Troubleshooting

## Blender not found

Symptoms:

- the application warns that Blender is missing
- rendering does not start

What to do:

1. Open settings.
2. Select the correct `blender.exe`.
3. Save the path.
4. Retry the render.

## Render script not found

Symptoms:

- the application starts but rendering cannot launch

What to do:

- verify that the installed application contains `scripts\render_single.py`
- if you are using a local build, make sure the script was copied to the output folder

## `cv2` import error inside Blender

Symptoms:

- Blender starts but rendering fails with a Python import error

What to do:

- install `opencv-python` in Blender's Python environment
- restart QualCompare and try again

## Missing textures

Symptoms:

- geometry renders but textures are wrong or missing

What to do:

- keep `.obj`, `.mtl`, and textures in a consistent relative layout
- verify that the material file references the texture files correctly

## Rendering is very slow

Symptoms:

- long wait times on large datasets

What to do:

- use SSD instead of HDD
- lower the number of views
- test on a smaller subset first
- close other heavy applications

## Masks are empty or wrong

Symptoms:

- generated mask files are fully black or incomplete

What to do:

- verify object scale and orientation
- test another object from the same dataset
- confirm that Blender imported the object correctly

## Patchify does not produce usable output

Symptoms:

- no CSV file
- patch extraction fails
- masks are not matched correctly

What to do:

- verify that rendering completed successfully first
- confirm that `views/` and `masks/` exist for the object
- confirm filenames follow the expected `view_N` and `mask_N` pattern

---

## Known limits

- Blender is required and must be installed separately.
- The application is Windows-only.
- The workflow depends on stable folder naming and output structure.
- Source/distorted selection depends on naming conventions in dataset folders.
- Large datasets can generate a very large amount of output data.

---

## Citation

If you use this software in research, please cite the corresponding work:

```text
Towards Reproducible Image-based 3D Quality Assessment:
Integrated Software and New Results
```

---

## License

Research software for internal or academic use.
