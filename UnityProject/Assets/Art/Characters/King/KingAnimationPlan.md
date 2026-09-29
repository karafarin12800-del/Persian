# King animation mapping

The source sheet is 6144×4096. It is treated as 6 columns × 4 rows of 1024×1024 frames.

Direction rows are kept isolated in the Animator so the player can select the closest cardinal direction from movement/aim input.

Animation states:
- Idle_Down
- Idle_Left
- Idle_Right
- Idle_Up
- Walk_Down
- Walk_Left
- Walk_Right
- Walk_Up

The first implementation should prefer the movement direction for facing, while weapon aim can later override the upper-body direction when layered animation is introduced.
