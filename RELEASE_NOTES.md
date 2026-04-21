# QualCompare 1.0.0

Release date: 2026-03-26

## Highlights

- First installable release of QualCompare for Windows.
- Bundles the active Blender rendering scripts required by the current pipeline.
- Bundles the patch extraction runtime used by the WPF application.
- Preserves the current rendering and patchify behavior while removing development-path assumptions.

## Included in this release

- Renamed the desktop application from `CompareMetrics` to `QualCompare`.
- Added install-aware path resolution for bundled scripts, resources, temp folders, and default output paths.
- Added first-run configuration bootstrap with automatic `settings.json` creation.
- Added an `Inno Setup` installer for x64 Windows deployment.
- Reduced packaged Python content to the active runtime subset:
  - `render_single.py`
  - `positions.py`
  - `Models_characteristics_and_settings.csv`
- Updated project documentation to match the current codebase and installer flow.

## Known limitations

- Blender is not bundled and must be installed separately.
- The application still depends on the current WPF desktop stack and native wrapper layout.
- The codebase remains partly centralized around the main WPF window and can be further modularized later.

## Validation status

- Build and installer compilation were validated manually in Visual Studio/MSVC.
- Fresh-machine validation protocol is documented in `docs/installer_validation.md`.

---

# QualCompare 1.0.1

Release date: 2026-04-21

## Highlights

- Fixed first-run GUI behavior for method-specific controls (`Circle`/camera height visibility).
- Improved CLI reliability and throughput:
  - locale-safe float formatting for Blender arguments (`--ypos`)
  - parallel rendering support via `maxParallelism` (`0` => `CPU/4`)
  - total render generation time logged at pipeline end
- Improved Blender compatibility:
  - default engine changed to `BLENDER_EEVEE`
  - automatic fallback to `BLENDER_EEVEE` when requested engine is unavailable
  - OBJ/PLY import fallback for Blender 3.x and 4.x operators
- Improved installation and troubleshooting documentation for first-time users and Linux/WSL setups.

## Included in this release

- GUI/Render fixes:
  - method panel visibility now updates consistently on first launch and selection changes
  - clearer patchify option label: `Folder patchification`
- CLI fixes and updates:
  - fixed culture-specific `--ypos` formatting issue (comma vs dot decimal)
  - object-level parallel execution aligned with configured `maxParallelism`
  - updated schema/docs to reflect active parallel behavior
- Documentation updates:
  - clearer installer location/build instructions
  - explicit admin note for Windows `cv2` installation in Blender Python
  - Linux/WSL first-run checklist for Blender Python dependencies
  - troubleshooting guidance for `PatchifyWrapper.dll` load errors
- Repository hygiene:
  - removed tracked Python bytecode artifacts
  - added ignore rules for `__pycache__/` and `*.pyc`

## Known limitations

- Blender is not bundled and must be installed separately.
- On WSL, OpenGL/EGL context issues may require software-rendering fallback wrappers.
  - This can be significantly slower than native Windows/Linux GPU-accelerated rendering.
- The application still depends on the current WPF desktop stack and native wrapper layout.

## Validation status

- CLI Release build validated (`dotnet build QualCompareCLI -c Release`).
- GUI runtime behavior validated for method switching and config export flow.
- Fresh-machine and Linux/WSL setup validation steps are documented in:
  - `README_user.md`
  - `QualCompareCLI/README.md`
  - `docs/installer_validation.md`
