package com.persiawar2d;

/** Pure math helpers for the 2.5D world projection. */
public final class IsometricMath {
    private IsometricMath() {}

    public static float screenX(float worldX, float worldY, float originX, float originY,
                                float tileW, float tileH) {
        return originX + (worldX - worldY) * tileW * 0.5f;
    }

    public static float screenY(float worldX, float worldY, float height,
                                float originX, float originY, float tileW, float tileH) {
        return originY + (worldX + worldY) * tileH * 0.5f - height;
    }

    public static float depth(float worldX, float worldY, float height) {
        // Ground anchor is dominant; height only slightly affects ordering so tall
        // objects remain behind objects whose feet are in front of them.
        return worldX + worldY + height * 0.001f;
    }

    public static float[] screenToWorld(float sx, float sy, float originX, float originY,
                                        float tileW, float tileH) {
        float a = (sx - originX) / (tileW * 0.5f);
        float b = (sy - originY) / (tileH * 0.5f);
        return new float[]{(a + b) * 0.5f, (b - a) * 0.5f};
    }
}
