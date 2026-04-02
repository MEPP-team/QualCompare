# Todo

This todo list reflects the current repository state and focuses on low-risk, high-value progress for a research workflow.

---

## High priority

- execute and record the fresh-machine installer validation protocol in `docs/installer_validation.md`
- publish the first installable `1.0.0` artifact from `installer/qualcompare.iss`
- tag the repository state associated with the `1.0.0` release
- document the exact Blender installation and Python dependency prerequisites for target users
- preserve and document the current output folder conventions used by rendering and Patchify
- reduce documentation drift by keeping docs aligned with code after each substantial change
- clarify the expected dataset folder conventions for `source` and `distorted` content

---

## Medium priority

- move more rendering and orchestration logic out of `MainWindow.xaml.cs` without changing behavior
- improve validation and error messages when Blender path or render script path is invalid
- make source / distorted selection more robust than the current path-keyword heuristic
- define a clearer installation and deployment workflow for lab machines
- review whether `ModelCsvFilePath` is still required in the active workflow
- document the exact role of legacy scripts such as `render_with_csv_transforms.py`
- improve logging around cache prefetch, Blender failures, and output copy-back
- decide whether user-config storage under `%AppData%\QualCompare` needs an explicit migration/versioning policy

---

## Backlog

- add a proper settings window / onboarding flow for first launch
- design a more formal dataset manifest or metadata layer
- reduce coupling between folder naming conventions and downstream processing
- evaluate whether render queue parallelism should become configurable from the active execution path
- improve packaging / installer support for non-developer users
- add clearer developer documentation for architecture, rendering, and patch extraction
- review the long-term place of older experimental scripts and archive unused variants
- consider testable seams around command construction and path generation logic
- automate part of the installer smoke test workflow
