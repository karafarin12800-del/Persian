package com.persiawar2d;

public final class RendererSelfTest {
    private RendererSelfTest() {}

    public static String runAll() {
        RendererChecks.run();
        float sx = IsometricMath.screenX(10f, 5f, 0f, 0f, 96f, 48f);
        float sy = IsometricMath.screenY(10f, 5f, 0f, 0f, 96f, 48f);
        if (!Float.isFinite(sx) || !Float.isFinite(sy)) {
            throw new IllegalStateException("non-finite projection");
        }
        return "renderer-self-test: PASS";
    }
}
