package com.persiawar2d;

/** Lightweight self-checks that can be called by a debug build without JUnit. */
public final class RendererChecks {
    private RendererChecks() {}

    public static void run() {
        float[] p = IsometricMath.screenToWorld(
                IsometricMath.screenX(12f, 7f, 100f, 80f, 96f, 48f),
                IsometricMath.screenY(12f, 7f, 0f, 100f, 80f, 96f, 48f),
                100f, 80f, 96f, 48f);
        require(Math.abs(p[0] - 12f) < 0.001f, "x inverse projection failed");
        require(Math.abs(p[1] - 7f) < 0.001f, "y inverse projection failed");

        DepthSort.WorldSprite a = new DepthSort.WorldSprite(2f, 4f, 0f, 0);
        DepthSort.WorldSprite b = new DepthSort.WorldSprite(3f, 4f, 0f, 0);
        require(DepthSort.BY_GROUND_ANCHOR.compare(a, b) < 0, "depth ordering failed");
    }

    private static void require(boolean ok, String message) {
        if (!ok) throw new IllegalStateException(message);
    }
}
