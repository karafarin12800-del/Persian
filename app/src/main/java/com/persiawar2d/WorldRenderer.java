package com.persiawar2d;

import android.content.Context;
import android.graphics.Canvas;

/** Compatibility facade: production world rendering is owned by IsoWorldRenderer. */
public final class WorldRenderer {
    public static final float WORLD_SIZE = IsoWorldRenderer.WORLD_SIZE;
    private final IsoWorldRenderer renderer;

    public WorldRenderer(Context context) {
        renderer = new IsoWorldRenderer(context);
    }

    public boolean isBlocked(float x, float y, float radius) {
        return renderer.isBlocked(x, y, radius);
    }

    public void draw(Canvas canvas, float playerX, float playerY, float scale,
                     float viewW, float viewH, float hudH) {
        renderer.draw(canvas, playerX, playerY,
                viewW * 0.5f, hudH + (viewH - hudH) * 0.5f, scale, hudH);
    }

    public void drawForeground(Canvas canvas, float playerX, float playerY, float scale,
                               float viewW, float viewH, float hudH) {
        renderer.drawForeground(canvas, playerX, playerY, scale, viewW, viewH, hudH);
    }
}
