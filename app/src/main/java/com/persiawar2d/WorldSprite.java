package com.persiawar2d;

/** A renderable world object with an explicit ground anchor and visual footprint. */
public final class WorldSprite {
    public float x;
    public float y;
    public float height;
    public float width;
    public float visualHeight;
    public int layer;
    public boolean occluder;

    public WorldSprite(float x, float y, float width, float visualHeight, float height,
                       int layer, boolean occluder) {
        this.x = x;
        this.y = y;
        this.width = width;
        this.visualHeight = visualHeight;
        this.height = height;
        this.layer = layer;
        this.occluder = occluder;
    }
}
