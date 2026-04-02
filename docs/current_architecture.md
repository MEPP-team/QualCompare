# Current architecture

The repository is organized around four main technical blocks:

- a Windows WPF desktop application in `QualCompare/`
- a Blender + Python rendering pipeline in `obj2png/`
- a native C++ patch extraction library in `patchify/`
- a C++/CLI bridge in `PatchifyWrapper/`

The historical project name `QualCompare` is still used in code and project files, while the user-facing name is now `QualCompare`.

---

## 1. WPF desktop application

Main entry point:

- `QualCompare/Program.cs`

Main window:

- `QualCompare/MainWindow.xaml`
- `QualCompare/MainWindow.xaml.cs`

Additional UI / orchestration files:

- `QualCompare/gui.cs`
- `QualCompare/RenderQueue.cs`
- `QualCompare/RenderParametersDialog.xaml(.cs)`
- `QualCompare/PatchifyParametersDialog.xaml(.cs)`

Current responsibilities:

- main UI and user workflow
- input / output folder selection
- render job creation and queueing
- launch of Blender in background mode
- launch of Patchify through the C++/CLI wrapper
- temporary SSD cache management
- output folder auto-generation
- user settings persistence

Blender is launched from C# using `ProcessStartInfo` / `Process.Start`.

---

## 2. Configuration model

The application currently uses two persistence layers:

### Legacy UI settings

Stored through:

- `QualCompare/Properties/Settings.settings`

Used mainly for:

- last selected folders
- last selected UI options
- patchify mode

### JSON application configuration

Defined in:

- `QualCompare/MainWindow.xaml.cs`

Stored in:

- `%AppData%/QualCompare/settings.json`

Contains:

- Blender executable path
- render script path
- temp input / output roots
- default output root
- render family name
- render parameters
- patchify parameters
- up axis
- PLY rendering options

So the project is no longer fully hardcoded, even if some default values still point to local development paths.

---

## 3. Render queue and execution model

The render queue is implemented in:

- `QualCompare/RenderQueue.cs`

Architecture:

- UI creates a `RenderJob` snapshot from the current controls
- jobs are stored in an `ObservableCollection`
- jobs are pushed into a `BlockingCollection`
- a single queue worker consumes jobs sequentially
- each job renders multiple objects in parallel with `Parallel.ForEach`

Important detail:

- queue level: sequential
- object level inside one job: parallel

Parallelism is currently limited to:

- `max(1, Environment.ProcessorCount / 4)`

The code comments and docs are correct that disk I/O is treated as a major bottleneck.

---

## 4. Cache and file staging

Before rendering, each object is copied into a temporary input cache.

Implemented in:

- `QualCompare/MainWindow.xaml.cs`

Main helper:

- `PrefetchObjToSSD`

What is copied:

- the `.obj` or `.ply` file
- the referenced `.mtl` file for OBJ when present
- the texture files referenced by the material

Temporary roots come from configuration:

- `TempInputRoot`
- `TempOutputRoot`

This cache is used to reduce repeated slow reads from dataset storage and to isolate each render input/output set.

---

## 5. Blender rendering pipeline

Main script:

- `obj2png/render_single.py`

Supporting script:

- `obj2png/positions.py`

Current responsibilities of `render_single.py`:

- clean the Blender scene
- import OBJ or PLY
- normalize object size
- recenter the object
- apply up-axis correction
- create camera and sun light
- compute FOV automatically from object geometry
- generate camera positions
- render color views
- render temporary masks
- threshold masks with OpenCV

Supported object types:

- OBJ
- PLY

PLY rendering is more advanced than the old docs suggested. Current supported modes are:

- `sphere`
- `surface`
- `voxel`
- `voxel_volume`

`positions.py` currently exposes these position families:

- `fibonacci`
- `yfixed`
- `polyedric`

Note:

The CLI string uses `polyedric`, while some older docs refer to `polyhedral`. The code currently depends on the `polyedric` spelling in the Python argument parser.

---

## 6. Output structure

Rendered output is written per object using this layout:

```text
object_name/
    views/
    masks/
```

Typical files:

- `views/view_1.png`
- `masks/mask_1.png`

At the application level, the output root can also be auto-generated using a higher-level experiment layout:

```text
<DefaultOutputRoot>/<Dataset>/<RenderFamilyName>/<Method>/<FileType>/<NbViews>VP
```

This logic is implemented in `BuildSuggestedOutputPath` in `QualCompare/MainWindow.xaml.cs`.

---

## 7. Patchify integration

Native library:

- `patchify/patchify.cpp`
- `patchify/patchify.h`

Managed bridge:

- `PatchifyWrapper/PatchifyWrapper.cpp`
- `PatchifyWrapper/PatchifyWrapper.h`

Called from:

- `QualCompare/MainWindow.xaml.cs`

Exposed functions:

- `ProcessImage`
- `ProcessImageFolder`

Current behavior:

- patchify reads rendered views
- it finds masks from the expected sibling `masks/` folder
- it filters candidate patches using a mask overlap threshold
- it writes patch coordinates and per-view summaries to CSV

The patch extraction output is part of the downstream dataset preparation workflow.

---

## 8. Secondary scripts and tools

Additional repository tools include:

- `obj2png/render_with_csv_transforms.py`
- `patchVisualizer/visualise_patches.py`

`render_with_csv_transforms.py` appears to be an alternative or older rendering workflow based on external model transforms CSV data, but it is not the main rendering entry point currently used by the WPF application.

