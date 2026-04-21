# QualCompare

QualCompare is a Windows desktop application for reproducible multi-view rendering of 3D objects and patch extraction workflows used in perceptual quality assessment research.

## Start here

- Users: see [README_user.md](README_user.md) for installation, first launch, quick start, rendering steps, and troubleshooting.
- Developers: see [README_dev.md](README_dev.md) for architecture, dependencies, contributor setup, and project constraints.

## WSL note

The primary supported runtime for the GUI is native Windows.

The CLI can run on Linux/WSL, but some WSL environments may require OpenGL/EGL software-rendering fallbacks to run Blender reliably. In those cases, rendering can be significantly slower than native Windows or native Linux GPU-accelerated runs.

For Linux/WSL setup and troubleshooting details, see:

- [QualCompareCLI/README.md](QualCompareCLI/README.md)
- [docs/installer_validation.md](docs/installer_validation.md)

## Main features

- Multi-view rendering with Blender
- Support for OBJ meshes and PLY point clouds
- Automatic mask generation
- Patch extraction for downstream evaluation and training workflows
- Reproducible output folder organization

## Supported formats

- `.obj` with associated `.mtl` and textures when applicable
- `.ply`

## License

This project is distributed under the GNU General Public License v3.0.
See the `LICENSE` file for details.

## Status

This repository contains research software. The priority is reproducibility and practical usability for experiments rather than production-level packaging or API stability.
