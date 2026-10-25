# Loadout test mode

Switch: `UnityProject/Assets/Scripts/UI/Loadout/LoadoutTestSwitch.cs`.

To disable, set `UnlockEverything = false` or delete the switch and remove the bypass references in `LoadoutStore.CanUseHero`, `TryBuyHero`, `CanUseWeapon`, `TryBuyWeapon`, and `WeaponController.ConfigureLoadout`, plus the TEST MODE badge in `PrototypeFlow.DrawLoadoutScreen`. Test-run completion must not grant campaign rewards.
