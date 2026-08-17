# 2.5D rendering architecture

The renderer uses a world-space ground plane, orthographic projection, explicit depth ordering, and billboard sprites. HUD elements remain in screen space so their positions do not drift when the camera moves.

The renderer must never rotate the entire screen canvas to simulate camera yaw. World geometry is transformed in world space instead.
