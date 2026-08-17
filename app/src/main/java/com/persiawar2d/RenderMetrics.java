package com.persiawar2d;

/** Runtime metrics kept separate from gameplay. */
public final class RenderMetrics {
    public int frames;
    public long lastFrameNanos;
    public float fps;
    public int visibleObjects;

    public void onFrame(long nowNanos, int visibleObjects) {
        frames++;
        this.visibleObjects = visibleObjects;
        if (lastFrameNanos != 0L) {
            long dt = nowNanos - lastFrameNanos;
            if (dt > 0L) fps = 1_000_000_000f / dt;
        }
        lastFrameNanos = nowNanos;
    }
}
