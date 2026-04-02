# Release Process

## Current baseline

- Product name: `QualCompare`
- First installable version: `1.0.0`
- Installer script: `installer/qualcompare.iss`
- Assembly version source: `QualCompare/Properties/AssemblyInfo.cs`
- Release notes: `RELEASE_NOTES.md`

## Release checklist

1. Build the `Release` configuration of `QualCompare`.
2. Verify that `bin/Release` contains:
   - `QualCompare.exe`
   - `PatchifyWrapper.dll`
   - `scripts/render_single.py`
   - `scripts/positions.py`
   - `resources/Models_characteristics_and_settings.csv`
3. Compile `installer/qualcompare.iss`.
4. Run the validation protocol in `docs/installer_validation.md`.
5. Update `RELEASE_NOTES.md` with the final release date and notable changes.
6. Tag or archive the installer artifact with the same semantic version as the assembly and installer.

## Versioning rule

- Use semantic versioning at the product level.
- Keep `AssemblyVersion`, `AssemblyFileVersion`, and installer `AppVersion` aligned for now.
- Use patch releases for installer, bootstrap, and documentation fixes that do not change the core pipeline contract.
- Use minor releases when the UI or runtime capabilities expand without breaking existing dataset and output conventions.
- Reserve major releases for changes that break configuration, CLI, output layout, or protocol assumptions.
