# PersiaWar 3D Architecture

## Runtime entry
`StartActivity` is the launcher. `START BATTLE` opens `MainActivity`.

## Gameplay layers
- `game/GameCore.java` — authoritative simulation: movement, auto-aim, line-of-sight targeting, projectiles, damage, sword, bombs, pickups, enemy patrol/attack/death, safe-zone damage and reinforcements.
- `world/WorldMap.java` — authoritative 6000x6000 world geometry used by both gameplay collision and visibility checks. Buildings, cars and fences are solid; trees, bushes and grass are decorative.
- `World3DRenderer.java` — real OpenGL ES 2.0 perspective renderer. It reads the same `WorldMap` and builds a depth-tested 3D terrain, roads, raised buildings/roofs, vehicles, fences, trees, bushes, enemies, projectiles, grenades and pickups.
- `MainActivity.java` — hosts the OpenGL surface plus a transparent Android overlay for responsive HUD, minimap, floating joystick, action buttons, pause and game-over UI. It does not own gameplay rules.
- `KingSpriteDrawable.java` — sprite-sheet decoder/animation renderer for the player, used as the 2.5D character presentation over the 3D world.

## Input
The joystick is genuinely floating: a touch on the left gameplay area creates its center at the initial finger position, the knob follows within a bounded radius, and release removes the joystick and zeroes movement. The right-side fire/sword/bomb/reload controls use density-scaled hit areas and multi-touch pointer tracking.

## Auto-aim rule
The core scans living enemies within range and accepts only enemies for which `WorldMap.hasLineOfSight()` is true. Buildings, parked cars and fences can therefore hide an otherwise closer enemy; the nearest visible enemy becomes the target.

## Combat rule
Gun fire creates a projectile. The projectile can hit a wall/vehicle/fence, then an enemy. Sword attacks apply damage in a short forward arc. Bombs travel toward the visible target and explode with area damage. Shield absorbs damage before HP. Ammo, medkits, bombs and shields are pickups.

## Enemy rule
Enemies patrol between random valid points when the player is not visible. With line of sight they chase, then attack at close range or fire at range. Dead enemies remain on the ground briefly before being removed and may drop loot.

## Map
The playable map is 6000x6000 world units with a connected road grid, many buildings, parked cars, fences, trees, bushes and grass. The same collision geometry drives both movement and auto-aim visibility checks.

## UI scaling
HUD, controls and minimap use Android display density rather than fixed raw pixel sizes. The tactical minimap scales up to a large readable footprint while remaining bounded to the right side of the landscape display.

## Future multiplayer boundary
`GameCore` deliberately has no rendering/UI dependencies, so a future network layer can feed authoritative player/enemy snapshots without rebuilding the rendering layer. Multiplayer is not enabled yet.
