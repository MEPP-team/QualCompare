# Graphics-LPIPS Bridge

This note describes the practical bridge between QualCompare and Graphics-LPIPS-QualCompare.

The goal is not to redefine either project. The goal is to keep the render side and the metric side aligned so that the same dataset, the same render settings, and the same output layout can be reused without ad hoc conversions.

---

## What QualCompare provides

QualCompare is responsible for the render-side contract:

- selecting the input dataset
- applying the source / distorted filter when needed
- rendering multi-view images with Blender
- generating sibling mask folders
- keeping a stable per-object output layout

The expected output shape remains:

```text
object_name/
    views/
    masks/
```

Typical files are:

- `views/view_1.png`
- `masks/mask_1.png`

This layout should be treated as the handoff format for downstream metric scripts.

---

## What Graphics-LPIPS-QualCompare should consume

Graphics-LPIPS-QualCompare should receive a frozen render set, not a moving target.

At minimum, keep the following stable across the renders that you want to compare:

- Blender version
- render script revision
- view count
- view sampling method
- render resolution
- camera and light settings
- object up-axis handling
- dataset revision
- source / distorted selection rule

If the metric-side script expects a different folder wrapper, adapt the wrapper only. Do not rename the per-object `views/` and `masks/` folders unless the downstream code has been updated and revalidated.

---

## Minimal example workflow

1. Prepare one dataset folder that contains both reference and distorted content.
2. In QualCompare, select the dataset root and choose the same render settings you intend to use for all runs.
3. Render the reference subset first.
4. Render the distorted subset with the same protocol.
5. Keep the resulting output tree untouched so that the downstream metric stage sees a consistent structure.
6. In Graphics-LPIPS-QualCompare, point the metric runner to the rendered views that correspond to the same reference / distorted pair.
7. Record the resulting scores together with the exact QualCompare settings used for the render.

The important part is not the UI path itself. The important part is that both repos operate on the same frozen render protocol.

---

## Recommended protocol controls

When the goal is reproducibility, pin these values rather than leaving them implicit:

- render family name
- number of views
- view sampling method
- resolution
- render engine
- TAA samples
- filter size
- background color
- sun theta and sun phi
- object up-axis
- point-cloud render mode, if relevant

For the current codebase, the defaults that matter most are already visible in `QualCompare/MainWindow.xaml.cs`:

- resolution: 650 x 550
- render engine: `BLENDER_EEVEE_NEXT`
- TAA samples: 64
- sun theta: 30
- sun phi: 50
- background color: `#34322C`

If the paper or lab protocol used a different baseline, override these settings explicitly and keep them written down in the experiment log.

---

## Legacy script note

The repository still contains `obj2png/render_with_csv_transforms.py`, which was historically tied to a Yana / TMQ-style workflow.

Treat it as a legacy or compatibility path unless the Graphics-LPIPS-QualCompare workflow explicitly depends on it. For the current application, the active render entry point is `obj2png/render_single.py`.

---

## What to archive with a run

For each reproduced run, keep the following together:

- QualCompare commit or release tag
- Graphics-LPIPS-QualCompare commit or tag
- Blender version
- the exact QualCompare settings file
- the render command arguments if you bypass the UI
- the rendered output tree
- the metric output files
- the dataset snapshot used for the run

This is the smallest set of artifacts that makes a later rerun defensible.