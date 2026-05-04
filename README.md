# QualCompare

QualCompare is a Windows desktop application for reproducible multi-view rendering of 3D objects and patch extraction workflows used in perceptual quality assessment research.

## Start here

**For Desktop Application (Windows WPF GUI):**
- Installation and usage: [QualCompare/README.md](QualCompare/README.md)
- Quick start, troubleshooting, and workflow guide

**For Command-Line Tool (cross-platform):**
- Setup and usage: [QualCompareCLI/README.md](QualCompareCLI/README.md)
- Includes platform-specific instructions for Windows, Linux, and macOS

**For Developers:**
- Architecture, dependencies, and contribution guide: [README_dev.md](README_dev.md)
- Build system, project constraints, and future work

## Platform support

**Windows (GUI & CLI):**
- Native Windows 10/11 with .NET 8+
- Desktop WPF application: [QualCompare/README.md](QualCompare/README.md)
- Command-line tool: [QualCompareCLI/README.md](QualCompareCLI/README.md)

**Linux & macOS (CLI only):**
- Cross-platform .NET 8+ runtime
- Command-line tool: [QualCompareCLI/README.md](QualCompareCLI/README.md) with platform-specific setup

**WSL (Windows Subsystem for Linux):**
- Supported for CLI usage; follow the Linux setup in [QualCompareCLI/README.md](QualCompareCLI/README.md)
- Note: Some WSL environments may require software-rendering fallbacks for Blender, which can reduce performance compared to native GPU-accelerated rendering

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
