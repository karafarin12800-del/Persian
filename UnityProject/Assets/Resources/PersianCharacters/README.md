# Persian Character Art

This folder contains the first visual pass for the Persian 2.5D game.

- Hero.png: player character presentation asset.
- Enemy_01.png: standard enemy.
- Enemy_02.png: ranged/scout enemy.
- Enemy_03.png: elite enemy.

The four PNGs are original project artwork generated for this repository. No third-party asset package is embedded here.

Runtime loading is handled by `StylizedCharacterVisual` through Unity Resources:
`Assets/Resources/PersianCharacters/*.png`

Gameplay logic remains in the existing Player, Enemy, Health, Weapon and Projectile scripts.
