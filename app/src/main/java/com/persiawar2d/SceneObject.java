package com.persiawar2d;

/** Small immutable scene description used by the render pass. */
public final class SceneObject {
    public final float x;
    public final float y;
    public final float z;
    public final float width;
    public final float height;
    public final int layer;

    public SceneObject(float x, float y, float z, float width, float height, int layer) {
        this.x = x;
        this.y = y;
        this.z = z;
        this.width = width;
        this.height = height;
        this.layer = layer;
    }
}
