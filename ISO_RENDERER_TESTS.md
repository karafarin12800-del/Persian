# 2.5D renderer validation

Implemented validation targets:

1. **Projection** — world X/Y maps to a diamond grid and the inverse mapping is used for touch aiming.
2. **Depth** — buildings, trees and actors are ordered by their world ground anchor.
3. **Buildings** — the bundled Kenney building artwork is loaded from `kenney_isometric-buildings.zip`; no white placeholder building tiles are used when the package loads successfully.
4. **Character** — the existing 4-direction × 6-action × 3-frame sheet remains the animation source; the character is drawn upright at its projected ground anchor.
5. **Integration** — `ControlActivity` now instantiates `IsoGameView`, so the new renderer is on the real battle path rather than only in a preview Activity.

The Android workflow builds `assembleDebug` on every push to this branch. A local device render pass is still required before calling an APK visually verified.
