# PersiaWar2D Architecture

## Runtime entry
`StartActivity` is the only launcher. `START BATTLE` opens `MainActivity`.

## Gameplay layers
- `game/GameCore.java` — authoritative simulation: movement, auto-aim, line-of-sight targeting, projectiles, damage, sword, bombs, pickups, enemy patrol/attack/death, safe-zone damage and reinforcements.
- `world/WorldMap.java` — authoritative 6000x6000 world geometry used by both gameplay collision and visibility checks. Buildings, cars, fences are solid; trees, bushes and grass are decorative.
- `WorldRenderer.java` — visual map renderer only. It reads `WorldMap` and renders roads, large terrain, houses, cars, trees, bushes, grass and fences.
- `MainActivity.java` — touch controls, HUD, minimap and entity presentation. It does not own gameplay rules.
- `KingSpriteDrawable.java` — sprite sheet decoder/animation renderer for the player.

## Auto-aim rule
The core scans living enemies within range, sorts effectively by distance through a nearest-target pass, and accepts only enemies for which `WorldMap.hasLineOfSight()` is true. Buildings, parked cars and fences can therefore hide an otherwise closer enemy; the nearest visible enemy becomes the target.

## Combat rule
Gun fire creates a projectile. The projectile can hit a wall/vehicle/fence, then an enemy. Sword attacks apply damage in a short forward arc. Bombs travel toward the visible target and explode with area damage. Shield absorbs damage before HP. Ammo, medkits, bombs and shields are pickups.

## Enemy rule
Enemies patrol between random valid points when the player is not visible. With line of sight they chase, then attack at close range or fire at range. Dead enemies remain on the ground briefly before being removed and may drop loot.

## Map
The playable map is 6000x6000 world units with a connected road grid, many buildings, parked cars, fences, trees, bushes and grass. The same collision geometry drives both movement and auto-aim visibility checks.

## Future multiplayer boundary
`GameCore` deliberately has no rendering/UI dependencies, so a future network layer can feed authoritative player/enemy snapshots without rebuilding the rendering layer. Multiplayer is not enabled yet.
