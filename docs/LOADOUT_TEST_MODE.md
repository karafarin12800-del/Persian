# Loadout test mode

Switch: `UnityProject/Assets/Scripts/UI/Loadout/LoadoutTestSwitch.cs`.

To disable, set `UnlockEverything = false` or delete the switch and remove the bypass references in `LoadoutStore.CanUseHero`, `TryBuyHero`, `CanUseWeapon`, and `TryBuyWeapon`, plus the TEST MODE badge in the loadout UI. Test-run completion must not grant campaign rewards.
