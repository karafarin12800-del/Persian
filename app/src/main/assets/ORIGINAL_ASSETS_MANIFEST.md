# Persia War 2.5D — Original visual assets

This directory is the canonical source list for the visual references used by the current game build.

## Reference package recovered from the project workspace

Source archive: `original_refs.zip`

Files:

- `world_map_square_2048.png` — 2048×2048 — SHA-256 `ae58a34403b1ab77ab90381a5d1cfbdf9251e8b8a6fae9103b990344d6f8643f`
- `references/1000121782.jpg` — SHA-256 `75acf99d18404b2a5c80f1581f5215504150344ca412f86d38caf1a5ad9d77da`
- `references/1000121907.png` — SHA-256 `14560f5b4199e33ecb77c4198437b2e4ed9f0ef5a92889e4e53883b56505acd8`
- `references/1000121839.jpg` — SHA-256 `67e908fd417098985d003df07a44a3b4b2c4d10212600a18ef55bcb080edd9d7`
- `references/1000121769.jpg` — SHA-256 `73b13de89f98b62e7935e3a16a0030557962a4b1bb50fb56270013c16d7f1872`
- `references/1000121909.png` — SHA-256 `c6db431adb626e44aad0825f703182f33c69d2c6a8e09cc063a5628fb4347a28`
- `references/1000121841.jpg` — SHA-256 `0ec18ad12cb91b72f1422290c94bb6b2713b3dd5e35ec08639aaacbffaf0a92c`
- `references/1000121867.png` — SHA-256 `23c70975ba8df1d82a068bf0908bffd7e21c38d4b94eabf3e4bcc99f59dc2988`
- `references/1000121842.jpg` — SHA-256 `a3d211127967da92b84d04a101d3b2eca4dd5b17c3810fbe7859f5a180139362`
- `references/1000121779.jpg` — SHA-256 `5b28dc6e5dff5063df5f6b9f4cdcf8d095b698536174910909840af9627ca114`
- `references/1000121836.jpg` — SHA-256 `4859f6ca9c71c40b3428c9309c3b21bf84dafedd19c49a191691b5ea9cc14754`

## Renderer contract

`WorldRenderer` first loads `world_map_square_2048.png` from `app/src/main/assets`. If that raster is unavailable, it falls back to the existing `references/world_texture_ref.jpg` asset before generating the procedural fallback.

The player/control code is intentionally not changed by the asset integration step.
