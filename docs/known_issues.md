# Known issues

This file lists issues and risks that are directly visible in the current repository state.

---

## Configuration and path issues

The application now supports a JSON configuration file stored in `%AppData%/QualCompare/settings.json`, so the project is no longer fully hardcoded.

However, the default values embedded in `AppConfig` still point to machine-specific development paths for items such as:

- Blender executable
- render script path
- temporary roots
- default output root
- model CSV path

This means first-run portability is still limited until those values are reviewed on a new machine.

---

## Historical naming inconsistency

The project has been renamed from `QualCompare` to `QualCompare`, but the old name is still present in many places:

- solution and project names
- namespaces
- settings storage path
- temp folder naming
- code comments and documentation fragments

This is not a runtime blocker, but it increases confusion when documenting or packaging the tool.

---

## Source / distorted classification is heuristic

The render queue does not use a formal dataset manifest to distinguish reference objects from distorted ones.

It currently relies on path keywords such as:

- `source`
- `ref`
- `reference`
- `src`

Datasets that do not follow these conventions may be misclassified by the `source` / `distorted` filter.

---

## Output and workflow conventions are tightly coupled

Several parts of the workflow depend on current folder conventions remaining stable:

- render output is expected under `views/` and `masks/`
- Patchify derives mask paths from rendered image paths
- patch CSV generation assumes the current render naming logic
- output folder auto-generation encodes experiment metadata in the directory tree

As a result, path or naming changes can easily break downstream steps even if rendering itself still works.

---

## Main window is highly centralized

`QualCompare/MainWindow.xaml.cs` currently contains a large amount of application logic, including:

- configuration handling
- path normalization helpers
- output naming logic
- cache staging helpers
- patchify integration
- logging helpers
- UI event handlers

This concentration of responsibilities increases the risk of regressions when modifying the application.

---

## Parallelism is constrained by I/O

The code already treats disk access as a bottleneck.

Current symptoms and constraints:

- object discovery is broad and recursive
- SSD staging is used to reduce dataset storage latency
- rendering within a job is parallelized, but HDD reads are serialized through a gate during prefetch
- output copy-back can also become expensive on large experiments

Performance therefore depends heavily on storage characteristics, not only on CPU or GPU.

---

## OBJ dependency sensitivity

OBJ rendering works best when the dataset preserves valid material references.

Potential failure points include:

- missing MTL files
- incorrect relative texture paths inside the MTL
- textures not copied with the mesh

Because the cache stage copies dependencies by parsing the OBJ and MTL structure, malformed material references can degrade rendered appearance or break texture loading.

---

## Blender and Python dependency assumptions

The render pipeline depends on the Blender Python environment being able to import required modules used by `render_single.py`, including:

- `cv2`
- `numpy`

The repository contains installation notes in `QualCompare/README.md`, but the application itself does not fully manage these dependencies automatically.

So environment setup remains an installation risk on a fresh workstation.

---

## Encoding and text quality issues in some files

Some source files and older UI strings show encoding artifacts or mixed French / English text.

This does not necessarily break execution, but it affects:

- maintainability
- documentation quality
- packaging polish
- confidence when tracing logs and messages

---

## Documentation lag versus code

Part of the internal documentation had drifted away from the current implementation before the recent updates.

This is a recurring risk in the repository because:

- the code evolves experimentally
- some older scripts remain in the tree
- behavior is sometimes encoded through conventions rather than explicit contracts

Documentation should therefore be treated as helpful context, but the code remains the primary source of truth.

