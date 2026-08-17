# Persia War 2.5D Renderer v1

This branch upgrades the presentation layer toward a true 2.5D/OpenGL ES pipeline while preserving existing gameplay logic.

## Goals
- Orthographic camera with real model/view/projection transforms.
- Depth-sorted world objects using a stable ground anchor.
- Billboard character sprites inside the 3D scene.
- Ground, roads, buildings, foliage and shadows rendered as world objects.
- UI/controls remain screen-space.
- Reuse existing character and environment assets before introducing new artwork.

## Validation
1. Compile the Android project.
2. Run static source checks for renderer invariants.
3. Verify asset paths and dimensions.
4. Verify camera projection and touch-coordinate conversion are inverse-compatible.
5. Verify draw ordering is deterministic for equal depth.

## Reference
Android documents OpenGL ES projection and camera transforms using model/view/projection matrices, which is the approach used by this renderer.
