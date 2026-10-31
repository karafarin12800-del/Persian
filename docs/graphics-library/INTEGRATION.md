# PersiaWar graphics library - integration guide

Copy the `Assets` folder of this package into `UnityProject/Assets` (keep the `.meta` files).
Nothing here changes existing behaviour until the call sites below are wired in.

NOT compiled or run on a device. It was only linted (balanced braces, C# tokens, call arity).
Compile errors, if any, will show in the CI build or Unity console and should be small.

## What is in the package

| File | Purpose |
|---|---|
| `Resources/PersiaWarVertexLit.shader` | One cheap lit shader: half-lambert, hemisphere ambient, rim, specular, glow. Reads vertex colour (alpha = surface: 0 matte, 64 plastic, 128 metal, 255 glow). |
| `Resources/PersiaWarDecal.shader` | Unlit alpha shader for blood decals and droplets. |
| `Scripts/Art/MeshKit.cs` | Procedural low-poly mesh builder (boxes, tapers, cylinders, ellipsoids, rings). |
| `Scripts/Art/ModelLibrary.cs` | Builds each model ONCE and caches it: Pistol, AssaultRifle, HeavyMachineGun, Axe, VoidRifle (alien), AlienSword (alien), ShieldPickup, Grenade, AmmoCrate, Medkit, QuickDash. One mesh + one shared material = one draw call per model. |
| `Scripts/Art/PickupVisual.cs` | Puts the real model + coloured ground ring + spin/bob on an existing pickup object. |
| `Scripts/Art/ModelPreview.cs` | Renders the same models into small RenderTextures for the store/menus. |
| `Scripts/Combat/BloodFX.cs` | Persistent blood / alien goo / scorch decals (single dynamic mesh, 32/64/96 decals by quality tier) + flying droplets. |
| `Scripts/Combat/HitReactor.cs` | Hit stagger + knockback and launched, tumbling death (kinematic, wall-safe). |

All weapon models point along +Z, grip near the origin, +Y up. If an existing weapon root points along +X,
pass `Quaternion.Euler(0, -90, 0)` as localRotation.

## Call sites to wire in

1. **Weapon in the player's hand** (`WeaponController`, where the gun/axe parts are built / `RefreshWeaponVisual`):
   ```csharp
   ModelLibrary.AttachWeapon(gunRoot, ModelLibrary.KindFromName(weaponKind.ToString()),
       true, Vector3.zero, Quaternion.identity, 1f);
   ```
   `hideOldVisuals = true` only disables the old primitive renderers (nothing is destroyed).
   Do it once per weapon change; reuse the returned object instead of spawning a new one every time.

2. **Enemy weapon** (`EnemyChase.EnsureEnemyWeaponVisual`): humans -> Pistol / AssaultRifle / HeavyMachineGun by
   archetype; aliens (names containing "Alien", `S3_`, `S4_`) -> VoidRifle or AlienSword. Same `AttachWeapon` call.

3. **Enemy weapon drops** (`TargetHealth.SpawnDroppedWeapon`): choose the model from the SAME kind that the pickup
   gives (aliens drop VoidRifle / AlienSword) and call `PickupVisual.Attach(dropRoot, kind, true)`.
   Make sure the pickup's weapon kind equals the model shown. `SelectWeapon(VoidRifle/AlienSword)` falls back to
   the assault rifle when `axeEnabledForMatch` is false, so check that it is enabled in Stage 3/4.

4. **Ground loot** (`GameBootstrap`, Stage 1 `SpawnRandomHouseLoot` and the Stage 2 loot builders):
   after creating each pickup object call `PickupVisual.Attach(pickupTransform, kind, true)`, where kind comes from
   `ModelLibrary.KindFromName(pickupName)` or the weapon kind. This replaces the gold cube / blue sphere / red sphere /
   magenta triangle with the real weapon, shield, grenade, ammo crate, medkit and dash models.

5. **Store and hero screens** (`PrototypeFlow.DrawStoreScreen`): replace the flat `Fill(emblem, color)` of the weapon
   items with `ModelPreview.Draw(emblemRect, ModelKind.VoidRifle)` (or `AlienSword`, `ShieldPickup`, ...).
   Call `ModelPreview.ReleaseAll()` when leaving the store.

6. **Blood and hit reaction** (`TargetHealth.ApplyDamage` and its death path):
   ```csharp
   // every non-lethal hit
   HitReactor.NotifyDamaged(gameObject, attackerPosition, amount, weaponName);
   // on death: replaces the 88-degree tip-over in FallAndRemoveEnemy
   float settle = HitReactor.NotifyKilled(gameObject, attackerPosition, amount, weaponName, explosive);
   Destroy(gameObject, settle + yourExistingDelay);
   ```
   `attackerPosition` = attacker.transform.position (Vector3.zero if unknown), `weaponName` = `WeaponKind.ToString()`.
   Grenades pass `explosive = true`. Optional: in `EnemyChase.Update` skip movement while
   `GetComponent<HitReactor>().IsStaggered`.

7. **Stage transitions**: call `BloodFX.ClearAll()` and `ModelPreview.ReleaseAll()` when a stage ends.
   (`BloodFX` also clears itself on every scene load.) `ModelLibrary` rebuilds meshes/material on demand, so it is
   safe even if a world clean-up destroyed the shared material (as `GameBootstrap.ReleaseWorldForStageTransition` does).

## Quality tiers

`GraphicsQualityProfile.CurrentTier` is respected: Low = no rim/specular, 32 decals, fewer droplets;
Medium = 64 decals; High = 96 decals.

## CI

Every new file under `Assets` has a `.meta`. Re-run the "Validate Unity project" job and read the Python assertions in
`.github/workflows/unity-android-build.yml` first; if a check enumerates shaders or scripts it may need the new names.

## Not included yet

Character bodies (procedural limbs and animation), environment lighting / props, and aliens' body meshes.
They should reuse `MeshKit` and the same vertex-lit shader.
