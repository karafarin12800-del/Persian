# Persia War 2.5D — Original visual assets

This is the canonical list of the original visual files supplied for the project. The current renderer is now **real-asset-only**: it will never silently generate the old green/vector map.

## Original bundle

The recovered source bundle contains:

- `world_map_square_2048.png` — 2048×2048
- `references/1000121782.jpg`
- `references/1000121907.png`
- `references/1000121839.jpg`
- `references/1000121769.jpg`
- `references/1000121909.png`
- `references/1000121841.jpg`
- `references/1000121867.png`
- `references/1000121842.jpg`
- `references/1000121779.jpg`
- `references/1000121836.jpg`
- `player/king_sprite_sheet.png` — 6144×4096, 6 frames × 4 directions
- `kenney_isometric_buildings/` — building/tree asset package already present in the repository

## Renderer contract

`WorldRenderer` now checks these real world textures in order:

1. `world_texture_real_square.png`
2. `world_map_square_2048.png`
3. `references/1000121867.png`
4. `references/1000121907.png`
5. `references/world_texture_ref.jpg`

If none is present, it renders a dark empty background rather than an artificial green map.

The gameplay collision geometry is kept independent from the visual world, so roads/walls can remain usable for gameplay without being painted as fake buildings over the real artwork.

The existing controls are intentionally not replaced by this asset-rendering change.
