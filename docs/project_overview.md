# Project overview

QualCompare is a research-oriented Windows application developed during a PhD on perceptual quality assessment of 3D content.

Its purpose is to build reproducible view-based pipelines for 3D quality experiments by turning meshes or point clouds into controlled rendered images, binary masks, and patch datasets.

The project was previously named `QualCompare`, and that historical name is still present in the solution, project files, namespaces, and some configuration paths.

---

## Research context

The central idea of the project is to evaluate 3D content through 2D rendered views rather than through direct 3D descriptors only.

This makes it possible to:

- reuse image-based perceptual metrics
- build controlled rendering protocols
- compare reference and distorted 3D objects through consistent multi-view image sets
- prepare datasets for learned metrics such as LPIPS-style pipelines

The repository is research code, not production software. Reproducibility and backward compatibility matter more than architectural elegance.

---

## Main use cases

The current application is used to:

- scan datasets of 3D objects
- select subsets of files such as source or distorted models
- render multiple views of OBJ or PLY objects with Blender
- generate corresponding binary masks
- cache inputs and outputs in temporary SSD folders
- extract image patch coordinates from rendered views using masks
- prepare structured outputs for downstream training and evaluation workflows

Neural network training itself is not implemented in this repository. The training stage is downstream and external to the GUI application.

---

## Typical workflow

A typical experiment flow is:

1. choose a dataset folder containing 3D objects
2. select the file family to process: everything, source, or distorted
3. choose the number of views and the view sampling strategy
4. render images and masks with Blender in background mode
5. collect outputs in the experiment folder structure
6. run Patchify on the rendered views
7. use the generated patch metadata in external training or evaluation code

---

## Main goals

The codebase is optimized for these goals:

- reproducibility of view generation
- stable output folder conventions
- automation of repetitive rendering tasks
- compatibility with multiple research datasets
- support for both meshes and point clouds
- patch-based preparation for perceptual quality pipelines

---

## Main technical components

Current project components are:

- `QualCompare/`: WPF desktop application and orchestration logic
- `obj2png/`: Blender rendering scripts and camera position generation
- `patchify/`: native C++ patch extraction logic
- `PatchifyWrapper/`: C++/CLI wrapper used by the C# application
- `patchVisualizer/`: patch CSV inspection utility

---

## Current scope boundaries

Included in the repository:

- GUI-driven render orchestration
- Blender command construction
- multi-view rendering and mask generation
- patch extraction from rendered images
- experiment-oriented output organization

Outside the current repository scope:

- model training pipelines
- MOS correlation evaluation scripts
- production packaging and deployment polish
- generalized dataset ingestion standards across all datasets

