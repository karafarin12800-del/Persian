package com.persiawar2d;

import java.util.Comparator;

/** Stable ordering for world-space sprites. */
public final class DepthSort {
    private DepthSort() {}

    public static final Comparator<WorldSprite> BY_GROUND_ANCHOR =
            Comparator.comparingDouble((WorldSprite s) -> IsometricMath.depth(s.x, s.y, s.height))
                    .thenComparingInt(s -> s.layer);

    public static final class WorldSprite {
        public final float x;
        public final float y;
        public final float height;
        public final int layer;

        public WorldSprite(float x, float y, float height, int layer) {
            this.x = x;
            this.y = y;
            this.height = height;
            this.layer = layer;
        }
    }
}
