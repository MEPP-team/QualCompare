# QualCompare Developer Guide

## Purpose and research constraints

QualCompare is a Windows-only research application built during a PhD project on perceptual quality assessment of 3D content. It is designed to generate reproducible multi-view renderings of meshes and point clouds, produce binary masks, and prepare patch-based image datasets for downstream quality metric experiments.

This repository is not production software. The main priority is to preserve reproducibility, backward compatibility, and the existing experimental workflow. Small safe changes are preferred over broad refactors.

The current end-to-end workflow is:

1. Select a dataset folder.
2. Discover supported 3D files recursively.
3. Copy each object and its dependencies to an SSD-backed temporary input cache.
4. Launch Blender in background mode with the current render arguments.
5. Generate views and masks.
6. Copy rendered outputs back to the final output folder.
7. Run patch extraction on the rendered images.

When editing the project, assume that output layout, Blender command-line arguments, and patchify conventions are already consumed by existing datasets, papers, and downstream scripts.

## Repository map

- `QualCompare/`
  WPF desktop application. It contains the UI, render queue, configuration bootstrap, path handling, output path generation, and the managed entry point.

- `obj2png/`
  Blender-side Python pipeline. The active script is `render_single.py`, supported by `positions.py` for camera sampling.

- `patchify/`
  Native C++ patch extraction logic. It reads rendered images and sibling masks, then writes CSV outputs used downstream.

- `PatchifyWrapper/`
  C++/CLI bridge used by the WPF application to call the native patchify code from C#.

- `installer/`
  Inno Setup packaging for the installable Windows release. It packages the application output, bundled scripts, and resources, but not Blender itself.

- `docs/`
  Maintained technical documentation covering architecture, datasets, rendering, installer validation, known issues, release process, and short-term priorities.

- `QualCompareCLI/`
  **NEW**: Cross-platform .NET 8 console application for batch rendering without UI. Reads JSON configuration files and executes rendering on Windows, Linux, or macOS. Enables automation and scripting workflows. See `QualCompareCLI/README.md` for usage.

Historical naming still exists internally. The product name is now `QualCompare`, but older names remain in some solution-level identifiers, namespaces, paths, comments, and documentation fragments. Treat those as technical debt rather than a reason to rename things aggressively.

## Architecture and design choices

### Why Blender is launched externally

Rendering is delegated to Blender and Python instead of being reimplemented in C#. This keeps the GUI focused on orchestration while the rendering logic stays close to Blender's API. The current WPF application constructs a Blender command line in `QualCompare/RenderQueue.cs` and launches Blender in background mode with:

```text
blender --background --python render_single.py -- <arguments>
```

That split is intentional:

- WPF handles datasets, queueing, settings, and user workflow.
- Blender handles import, normalization, camera placement, lighting, rendering, and mask generation.
- Python remains the source of truth for rendering semantics.

### Why SSD temp input and output roots exist

The application stages data into temporary input and output folders before copying results back to the final destination. This is done to reduce repeated reads from slower dataset storage, keep per-object processing isolated, and reduce I/O bottlenecks during rendering.

The relevant runtime paths are stored in JSON config as:

- `TempInputRoot`
- `TempOutputRoot`

### Why the queue is sequential but rendering inside a job is parallel

The render queue processes one job at a time, but objects inside that job are rendered in parallel. This is the current compromise between throughput and disk pressure:

- queue level: sequential
- object level inside one job: parallel

The code currently treats storage I/O as the main bottleneck. HDD reads are gated during prefetch, SSD staging is used deliberately, and parallelism defaults to a fraction of CPU count rather than full saturation.

### Why output layout is a contract

Rendered output layout is part of the workflow contract, not just a cosmetic convention. Current per-object output is:

```text
object_name/
    views/
    masks/
```

Expected filenames are:

```text
views/view_1.png
masks/mask_1.png
```

This must stay stable because:

- patchify derives mask paths from rendered image paths
- CSV outputs assume the current naming scheme
- downstream experiments rely on stable folder conventions
- the WPF app can also build higher-level experiment directories from dataset metadata

### Why there are two persistence layers

The application currently uses two persistence systems:

- legacy UI state in `QualCompare/Properties/Settings.settings`
- runtime JSON config in `%AppData%/QualCompare/settings.json`

The legacy settings mainly preserve last-used UI values such as folders, selected method, or patchify mode. The JSON config stores runtime-critical settings such as:

- `BlenderPath`
- `RenderScriptPath`
- `TempInputRoot`
- `TempOutputRoot`
- `DefaultOutputRoot`
- render parameters
- patchify parameters
- up-axis and PLY render options

This split is real and contributors should preserve it unless they are intentionally consolidating configuration with a migration plan.

## Development environment and dependencies

## Required environment

- Windows 10 or Windows 11
- Visual Studio 2022
- Visual Studio workloads for:
  - .NET desktop development
  - Desktop development with C++
- .NET Framework 4.8.1 targeting pack
- MSVC v143 toolset
- Blender 4.x installed separately
- OpenCV native installation for `patchify/` and `PatchifyWrapper/`

## Managed dependencies

The C# project restores packages from `QualCompare/packages.config`. Current managed dependencies include:

- `Newtonsoft.Json`
- `System.Reactive`
- `System.Reactive.Windows.Forms`
- `System.Runtime.CompilerServices.Unsafe`
- `System.Threading.Tasks.Extensions`

## Native dependencies

Both `patchify/patchify.vcxproj` and `PatchifyWrapper/PatchifyWrapper.vcxproj` expect OpenCV headers and libraries to be discoverable through either:

- `OPENCV_DIR`
- standard installation paths such as `C:\Program Files\opencv\build`

The current project files look for OpenCV include and lib folders and attempt to resolve `opencv_world` automatically.

## Blender Python dependencies

`obj2png/render_single.py` imports:

- `bpy`
- `cv2`
- `numpy`
- `mathutils`

`bpy` and `mathutils` come from Blender. `cv2` and `numpy` must be available in Blender's Python environment for the render script to work reliably. The existing installation notes in `QualCompare/README.md` should be treated as the current baseline, even though they still need cleanup.

## Platform note

The solution is effectively x64 in practice. The C# project uses `Any CPU` solution entries, but the project itself targets x64 output, and both native projects are configured around x64 builds for the active workflow. Treat x64 as the expected developer target unless you are explicitly working on platform support.

## Starting a new development session

Use this checklist when starting on a fresh workstation or after a long pause.

1. Open `QualCompare/QualCompare.sln` in Visual Studio 2022.
2. Restore NuGet packages for the C# project if Visual Studio did not do it automatically.
3. Confirm that OpenCV is installed and that the native projects can resolve headers and libraries.
4. Select `Debug|x64` for normal development, or `Release|x64` when validating packaging.
5. Build the full solution so `QualCompare`, `PatchifyWrapper`, and `patchify` stay aligned.
6. Set `QualCompare` as the startup project and launch it from Visual Studio.
7. On first run, let the application create `%AppData%/QualCompare/settings.json`.
8. In the application settings, verify or update:
   - `BlenderPath`
   - `RenderScriptPath`
   - `TempInputRoot`
   - `TempOutputRoot`
   - `DefaultOutputRoot`
9. Confirm that the bundled script path resolves to `scripts/render_single.py` from the application output.
10. Run a small OBJ render smoke test.
11. Run a patchify smoke test on a rendered result.

### Suggested smoke test

For a minimal validation pass:

1. Pick a small dataset folder with a known-good OBJ.
2. Render a low number of views using the default method.
3. Confirm output folders contain both `views/` and `masks/`.
4. Run patchify on one rendered image or on the rendered folder.
5. Confirm that CSV outputs are generated without wrapper or native DLL errors.

### What to check when startup fails

- `blender.exe` is missing or the configured `BlenderPath` is invalid.
- `scripts/render_single.py` is missing from the application output or the configured `RenderScriptPath` is stale.
- OpenCV headers or libs are not being resolved for `patchify` or `PatchifyWrapper`.
- Blender's Python environment cannot import `cv2`.
- A stale `%AppData%/QualCompare/settings.json` points to old development paths.

## Runtime contracts contributors must preserve

These points are part of the active runtime contract and should not be changed casually.

- Preserve the Blender CLI argument names and semantics assembled in `QualCompare/RenderQueue.cs` and parsed by `obj2png/render_single.py`.
- Preserve per-object output layout:

```text
object_name/views/view_N.png
object_name/masks/mask_N.png
```

- Preserve patchify's assumption that masks live in the sibling `masks/` folder with matching `mask_N` names.
- Preserve dataset compatibility and the current source/distorted behavior unless you are intentionally redesigning that logic.
- Preserve the current Python CLI spelling `polyedric`. Older docs may say `polyhedral`, but the active parser and WPF command building depend on `polyedric`.
- Preserve current Blender invocation style and output copy-back behavior unless the full downstream workflow is revalidated.

## Cross-platform CLI (QualCompareCLI)

The `QualCompareCLI` project provides cross-platform batch rendering without UI and reuses the same Blender argument contract as the WPF app.

For usage, schema, Linux/WSL setup, and troubleshooting, use the canonical documentation:

- `QualCompareCLI/README.md`
- `QualCompareCLI/CONFIG_SCHEMA.md`

## Practical contributor workflows

### Working on CLI

Primary area:

- `QualCompareCLI/`

Main files:

- `Program.cs` - CLI entry point and argument parsing
- `RenderConfig.cs` - JSON configuration schema
- `BlenderRenderService.cs` - Blender process execution

Main tasks:

- Adding configuration options
- Improving error messages
- Adding validation logic
- Handling edge cases

**Important:** Keep argument construction in sync with `RenderQueue.cs`. If you change Blender arguments, update both locations.

### Working on WPF and UI behavior

Primary area:

- `QualCompare/`

Main responsibilities in the current app:

- UI event handling
- render job creation
- queue orchestration
- settings bootstrap
- output path generation
- patchify launch

Main risk:

`QualCompare/MainWindow.xaml.cs` is highly centralized. Small changes can affect configuration, rendering, patchify, and logging at once. Prefer focused edits and verify the full user workflow after touching this area.

### Working on rendering and Blender pipeline

Primary area:

- `obj2png/`

Main files:

- `obj2png/render_single.py`
- `obj2png/positions.py`

Main regression risks:

- breaking CLI argument compatibility
- changing output naming
- changing mask generation behavior
- changing supported position names
- changing PLY rendering semantics without updating the GUI assumptions

When changing rendering behavior, validate at least one OBJ workflow and one mask generation pass.

### Working on patchify and native code

Primary areas:

- `patchify/`
- `PatchifyWrapper/`

Main regression risks:

- OpenCV linkage failures
- C++/CLI wrapper load failures
- CSV format drift
- mask path derivation breakage
- path separator assumptions

When changing patch extraction behavior, validate both wrapper loading and a real patchify run from the WPF application, not just native compilation.

### Validating installer changes

Primary area:

- `installer/`

Reference documentation:

- `docs/installer_validation.md`
- `docs/release_process.md`

Current installer scope:

- packages the built application output
- includes bundled scripts and resources
- does not bundle Blender

Main regression risks:

- packaged script paths still pointing to development locations
- missing Python scripts or CSV resources in the install directory
- first-run config bootstrap not creating usable defaults
- `PatchifyWrapper.dll` failing after installation

Use the documented installer validation protocol when touching packaging or path resolution.

## Near-term directives

The following items are strong candidates for the next development iterations:

- Execute and record the fresh-machine installer validation workflow from `docs/installer_validation.md`.
- Clean up and clarify the exact Blender Python dependency installation process, especially for `cv2`.
- Keep docs aligned with code after every substantial change to reduce documentation drift.
- Replace or harden the current source/distorted heuristic so it is less dependent on path keywords.
- Gradually move orchestration logic out of `QualCompare/MainWindow.xaml.cs` without changing behavior.
- Review whether `ModelCsvFilePath` is still required by the active runtime workflow.
- Improve logging around SSD prefetch, Blender failures, and output copy-back.

## References for contributors

Useful repo documents for future sessions:

- `README_user.md`
- `RELEASE_NOTES.md`
- `docs/project_overview.md`
- `docs/current_architecture.md`
- `docs/rendering_pipeline.md`
- `docs/datasets_and_protocols.md`
- `docs/known_issues.md`
- `docs/todo.md`
- `docs/installer_validation.md`
- `docs/release_process.md`

The code remains the final source of truth when documentation and implementation diverge.
