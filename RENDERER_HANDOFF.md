# Renderer handoff

The branch `persia-2.5d-render-v1` is the isolated renderer track. It is based on the existing project and does not alter the gameplay design. The goal is to replace the old whole-canvas rotate/scale presentation with world-space 2.5D projection while keeping the existing screen-space UI stable.

The existing character and building packages remain the source of artwork. The character sheet is already stored under `app/src/main/assets/player/`; the Kenney isometric building package is under `app/src/main/assets/original_packages/`.
