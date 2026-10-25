# Loadout test mode

Switch: `UnityProject/Assets/Scripts/UI/Loadout/LoadoutTestSwitch.cs`.

To disable the prototype unlock override, change this exact line to `false`:

```csharp
public const bool UnlockEverything = true;
```

If deleting the switch file instead, remove these exact test-only bypasses:

- In `LoadoutStore.CanUseHero`: `if(LoadoutTestSwitch.UnlockEverything)return true;`
- In `LoadoutStore.TryBuyHero`: `if(LoadoutTestSwitch.UnlockEverything||CanUseHero(i,rescued))return true;`
- In `LoadoutStore.CanUseWeapon`: `if(LoadoutTestSwitch.UnlockEverything)return true;`
- In `LoadoutStore.TryBuyWeapon`: `if(LoadoutTestSwitch.UnlockEverything||CanUseWeapon(k))return true;`
- In `WeaponController.ConfigureLoadout`: remove `|| LoadoutTestSwitch.UnlockEverything` from the `axeEnabledForMatch` assignment.
- In `PrototypeFlow.DrawLoadoutScreen`: remove the `if(LoadoutTestSwitch.UnlockEverything){...}` block that draws the TEST MODE badge.

The remaining normal rules are: hero 0 and Assault Rifle are free; Anahita can be rescued or bought; other heroes and weapons require their listed price, and weapons also require the corresponding real campaign stage clear. Test-run completion must not grant coins or campaign progress.
