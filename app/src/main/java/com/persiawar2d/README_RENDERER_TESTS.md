# Renderer validation checklist

The renderer branch is validated in five independent passes before release:

1. **Math pass** — world-to-screen and screen-to-world are inverse within tolerance.
2. **Ordering pass** — ground anchors produce deterministic depth ordering.
3. **Asset pass** — renderer only accepts existing asset paths and does not invent files.
4. **Build pass** — Android debug build must complete with no compilation errors.
5. **Visual pass** — inspect screenshots for flat/white building tiles, rotated map geometry, detached roofs, sprite bleed, and HUD drift.

A release build is not described as tested until the build workflow and these checks succeed.
