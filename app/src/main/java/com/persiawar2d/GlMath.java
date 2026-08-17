package com.persiawar2d;

import android.opengl.Matrix;

/** Small matrix helper for the OpenGL ES 2.0 renderer. */
public final class GlMath {
    private GlMath() {}

    public static void orthoForLandscape(float[] out, float aspect, float worldSpan) {
        float halfH = worldSpan * 0.5f;
        float halfW = halfH * Math.max(1f, aspect);
        Matrix.orthoM(out, 0, -halfW, halfW, -halfH, halfH, -100f, 100f);
    }

    public static void camera(float[] out, float yawDeg, float pitchDeg, float distance) {
        Matrix.setLookAtM(out, 0,
                0f, 0f, distance,
                0f, 0f, 0f,
                0f, 1f, 0f);
        Matrix.rotateM(out, 0, pitchDeg, 1f, 0f, 0f);
        Matrix.rotateM(out, 0, yawDeg, 0f, 0f, 1f);
    }
}
