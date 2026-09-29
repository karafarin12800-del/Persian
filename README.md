# PersiaWar2D

A 2.5D top-down Persian/Achaemenid Android action game.

## Current gameplay
- Responsive landscape touch controls
- Left thumb joystick for movement
- Right-side drag for manual aiming
- Auto-aim fallback when no manual aim is active
- Player shooting with visible projectiles
- Ranged enemy shooting with line-of-sight and multiple enemy types
- Melee enemies at close range
- Sword, grenade and reload controls
- Health, shield, ammunition, score and kill HUD
- Tactical minimap and shrinking danger zone
- Persian-styled procedural city with roads, alleys, buildings, vehicles, fences and vegetation
- Character selection with four gameplay profiles
- Restartable Game Over flow

## Build
The project uses Android SDK 35, Java 17, Android Gradle Plugin 8.6.1 and Gradle 8.7.
GitHub Actions builds a debug APK on pushes to `main` and via manual dispatch.
