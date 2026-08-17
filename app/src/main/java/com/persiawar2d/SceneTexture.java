package com.persiawar2d;

import android.graphics.Bitmap;

/** CPU-side texture metadata used by the preview renderer. */
public final class SceneTexture {
    public final Bitmap bitmap;
    public final float anchorX;
    public final float anchorY;

    public SceneTexture(Bitmap bitmap, float anchorX, float anchorY) {
        this.bitmap = bitmap;
        this.anchorX = anchorX;
        this.anchorY = anchorY;
    }
}
