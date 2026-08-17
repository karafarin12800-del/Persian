package com.persiawar2d;

/** Camera helper that keeps UI coordinates independent from world projection. */
public final class SceneCamera {
    private float worldX;
    private float worldY;
    private float zoom = 1f;

    public void set(float worldX, float worldY, float zoom) {
        this.worldX = worldX;
        this.worldY = worldY;
        this.zoom = Math.max(0.65f, Math.min(1.5f, zoom));
    }

    public float worldX() { return worldX; }
    public float worldY() { return worldY; }
    public float zoom() { return zoom; }
}
