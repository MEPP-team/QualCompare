# QOMEX Reproduction Guide

This document records the step-by-step protocol for reproducing the paper workflow that depends on QualCompare and Graphics-LPIPS-QualCompare.

It is written as a practical checklist rather than as a theory note. The key idea is to freeze every input that can change the result: code revision, dataset revision, render settings, and metric settings.

---

## Scope

Use this guide when you want to reproduce the results of the submitted paper workflow, including the render stage and any downstream metric or patch-based stage.

The guide assumes two repositories are available side by side:

- QualCompare-public
- Graphics-LPIPS-QualCompare

If the paper also depends on a dataset or internal helper scripts, keep those in a separate archived location and note the exact revision.

---

## Freeze the environment first

Before running anything, record:

- the QualCompare commit or release tag
- the Graphics-LPIPS-QualCompare commit or release tag
- the dataset snapshot or checksum
- the Blender version
- the Windows version
- the Python environment used by Blender
- the exact date and machine context

If any of those change later, the run is no longer the same run.

---

## Configure QualCompare

Set the runtime configuration in QualCompare so that the render side is explicit and repeatable.

At minimum, confirm:

- Blender executable path
- render script path
- temporary input root
- temporary output root
- default output root
- model CSV path, if the workflow still uses it

Then pin the protocol values used by the paper:

- input format: OBJ or PLY
- file selection mode: everything, source, or distorted
- render family name
- number of views
- view sampling method
- up-axis
- render resolution
- render engine
- TAA samples
- filter size
- sun theta
- sun phi
- background color
- PLY render mode, if applicable

For the current repository baseline, the code defaults currently visible in `QualCompare/MainWindow.xaml.cs` are:

- resolution: 650 x 550
- render engine: `BLENDER_EEVEE_NEXT`
- TAA samples: 64
- filter size: 1.5
- sun theta: 30
- sun phi: 50
- background color: `#34322C`

If the paper used different values, do not rely on defaults. Write the explicit values into the experiment log.

---

## Step-by-step render protocol

1. Select the dataset root.
2. Confirm whether the run is for all files, source only, or distorted only.
3. Choose the object format and the correct view sampling method.
4. Set the number of views required by the paper.
5. Verify that the output folder is empty or versioned for this run.
6. Run a small smoke test on one object first.
7. Confirm that the object output contains both `views/` and `masks/`.
8. Check that file names follow the expected `view_N.png` and `mask_N.png` pattern.
9. If the smoke test is correct, run the full dataset.
10. Archive the final render tree without renaming files.

If the paper uses paired reference and distorted content, render both sides with the same protocol and keep the same experiment metadata in the folder names.

---

## Step-by-step metric stage

1. Open Graphics-LPIPS-QualCompare.
2. Point it to the rendered views produced by QualCompare.
3. Make sure it sees the same reference / distorted pairing that was used during rendering.
4. Run the metric stage on the archived render tree.
5. Save the score outputs and any logs produced by the metric runner.
6. Verify that the score files correspond to the intended dataset snapshot and render configuration.

If Graphics-LPIPS-QualCompare expects a wrapper script or a different directory convention, adapt only the wrapper layer. Keep the QualCompare output contract stable.

---

## What must be documented for reproduction

For the final report or experiment note, include:

- the exact dataset name and version
- the exact QualCompare configuration
- the exact Graphics-LPIPS-QualCompare configuration
- the exact Blender version
- any dependency installation done inside Blender Python
- the render output root
- the metric output root
- the patch extraction settings if patches were part of the pipeline
- any legacy script used during the run

This makes the run auditable later, which matters more than a perfectly short protocol.

---

## Notes on legacy paths

If the workflow still references `obj2png/render_with_csv_transforms.py`, treat that as a compatibility path and document why it was needed.

If the paper reproduction can be done with `obj2png/render_single.py`, prefer that path instead. It is the active render entry point in the current repository.

---

## Failure recovery

If a run fails, capture the following before changing anything:

- the failing command or UI action
- the exact error message
- the object or dataset entry that failed
- whether Blender could start
- whether the output tree was created partially
- whether the failure came from rendering, patchify, or the metric stage

This prevents silent protocol drift between reruns.