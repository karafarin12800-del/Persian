package com.persiawar2d;

/** Deterministic painter-order key. Lower keys are drawn first. */
public final class SceneDepth {
    private SceneDepth() {}

    public static long key(float x, float y, int layer) {
        long gx = Math.round(x * 1000f);
        long gy = Math.round(y * 1000f);
        return ((gx + gy) << 16) ^ (layer & 0xffffL);
    }
}
