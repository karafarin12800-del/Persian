package com.persiawar2d;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * Render-only scene state. Gameplay can populate this model without knowing
 * whether the backend is Canvas or OpenGL ES.
 */
public final class WorldRenderModel {
    private final ArrayList<WorldSprite> objects = new ArrayList<>();

    public void clear() {
        objects.clear();
    }

    public void add(WorldSprite sprite) {
        objects.add(sprite);
    }

    public List<WorldSprite> sorted() {
        objects.sort((a, b) -> {
            int c = Float.compare(IsometricMath.depth(a.x, a.y, a.height),
                    IsometricMath.depth(b.x, b.y, b.height));
            if (c != 0) return c;
            return Integer.compare(a.layer, b.layer);
        });
        return Collections.unmodifiableList(objects);
    }
}
