# Installer Validation

This document defines the validation protocol for a fresh-machine installation of QualCompare.

The goal is to confirm that the installer is usable without manual file edits and that the installed application preserves the current rendering and patchify workflow.

---

## Test goals

The installer is considered valid if all of the following are true:

- QualCompare installs successfully through the setup executable
- the installed application launches from the Start Menu or desktop shortcut
- the application creates its initial configuration automatically
- the bundled render scripts are found without manual path editing
- the bundled resources are found without manual path editing
- Patchify loads correctly from the installed application
- Blender can be configured through the UI if it is not auto-detected
- rendering still produces the expected output layout
- patch extraction still works on installed outputs

---

## Test environments

Run the validation on at least these machine states:

### 1. Clean machine with Blender installed

Expected result:

- installer succeeds
- first launch succeeds
- Blender is auto-detected or requires only UI selection
- rendering works
- Patchify works

### 2. Clean machine without Blender installed

Expected result:

- installer succeeds
- first launch succeeds
- application explains that Blender still needs to be configured
- no manual config file edit is required
- after selecting Blender later through the UI, rendering works

### 3. Machine with a previous QualCompare installation

Expected result:

- reinstall or upgrade succeeds
- application still launches
- existing user config is preserved or safely refreshed
- no regression in render and patchify behavior

---

## Pre-test checklist

Before running the installer test:

- build the `Release` output of `QualCompare`
- compile the Inno Setup script `installer/qualcompare.iss`
- verify the setup includes:
  - `QualCompare.exe`
  - `PatchifyWrapper.dll`
  - `scripts\render_single.py`
  - `scripts\positions.py`
  - `resources\Models_characteristics_and_settings.csv`
- prepare a known Blender installation path for validation

---

## Installation checks

After launching the installer:

1. confirm the wizard starts normally
2. confirm installation path defaults to `Program Files\QualCompare`
3. confirm shortcuts are created as expected
4. confirm installed directory contains:
   - `QualCompare.exe`
   - required managed DLLs
   - `scripts\render_single.py`
   - `scripts\positions.py`
   - `resources\Models_characteristics_and_settings.csv`
5. confirm the application launches from the installer finish page if selected

---

## First-run checks

On the first launch of the installed application:

1. confirm the application opens without crashing
2. confirm `%AppData%\QualCompare\settings.json` is created automatically
3. confirm the generated config contains resolved defaults for:
   - render script path
   - temp input path
   - temp output path
   - default output root
4. if Blender is missing, confirm the warning message is understandable and actionable
5. confirm the user can open Settings and select Blender through the UI
6. confirm no manual text editing of `settings.json` is required

---

## Rendering checks

Using a small validation dataset:

1. launch the installed application
2. select an input dataset folder
3. select an output folder
4. render at least one OBJ
5. if relevant, render at least one PLY
6. confirm Blender is launched correctly from the installed application
7. confirm outputs are written with the expected structure:

```text
object_name/
    views/
    masks/
```

8. confirm view and mask naming are unchanged:
   - `view_1.png`
   - `mask_1.png`
9. compare installed-app outputs against a known-good development run when possible

---

## Patchify checks

After rendering:

1. launch Patchify from the installed application
2. run patch extraction on a rendered folder or image
3. confirm Patchify starts without DLL loading errors
4. confirm CSV outputs are generated
5. confirm mask-based filtering still behaves as expected
6. optionally inspect results with the patch visualization workflow

---

## Regression checks

During validation, explicitly watch for these regressions:

- render script path still pointing to a development machine path
- model CSV path still pointing to a development machine path
- missing bundled Python scripts in the installed directory
- missing bundled resources in the installed directory
- Patchify DLL load failure
- Blender not detected and UI path selection not persisted
- output directory layout changed unexpectedly
- first-run messages appearing repeatedly when config is already valid

---

## Pass criteria

The installer validation passes if:

- installation completes successfully
- first launch is successful without manual file editing
- Blender configuration is either automatic or UI-guided
- one full render completes successfully
- one patchify run completes successfully
- output structure and naming remain backward compatible

---

## Follow-up when a test fails

When a validation step fails, record:

- machine context
- installer version
- whether Blender was preinstalled
- exact failing step
- error message shown to the user
- whether the issue is packaging, bootstrap, dependency, or workflow related

This should be logged before changing code so that installer regressions remain reproducible.


