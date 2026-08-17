package com.persiawar2d;

/** Ground footprint used to decide when a world sprite can overlap another object. */
public final class VisualOccluder {
    public final float minX;
    public final float maxX;
    public final float minY;
    public final float maxY;
    public final float top;

    public VisualOccluder(float minX, float maxX, float minY, float maxY, float top) {
        this.minX = minX;
        this.maxX = maxX;
        this.minY = minY;
        this.maxY = maxY;
        this.top = top;
    }

    public boolean contains(float x, float y) {
        return x >= minX && x <= maxX && y >= minY && y <= maxY;
    }
}
