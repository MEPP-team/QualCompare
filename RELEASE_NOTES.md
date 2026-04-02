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
