package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.BitmapRegionDecoder;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Rect;
import android.graphics.drawable.Drawable;

import java.io.InputStream;

/**
 * Renders one frame from the user's 6144x4096 king sprite sheet without decoding
 * the entire sheet into a second large bitmap. The sheet is treated as 6 columns
 * x 4 rows, with a 1024x1024 frame size.
 */
public final class KingSpriteDrawable extends Drawable {
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final BitmapRegionDecoder decoder;
    private final Rect source = new Rect();
    private Bitmap frame;

    public KingSpriteDrawable(Context context) {
        BitmapRegionDecoder d = null;
        try (InputStream in = context.getAssets().open("player/king_sprite_sheet.png")) {
            d = BitmapRegionDecoder.newInstance(in, false);
        } catch (Exception ignored) {
        }
        decoder = d;
        if (decoder != null) {
            int frameW = decoder.getWidth() / 6;
            int frameH = decoder.getHeight() / 4;
            // Front/idle frame from the first row. Keep the source fixed so the
            // original artwork is shown instead of the replacement vector art.
            source.set(0, 0, frameW, frameH);
            frame = decoder.decodeRegion(source, new BitmapFactory.Options());
        }
        setAlpha(255);
    }

    @Override public void draw(Canvas canvas) {
        if (frame == null) return;
        Rect b = getBounds();
        paint.setAlpha(getAlpha());
        canvas.drawBitmap(frame, null, b, paint);
    }

    @Override public void setAlpha(int alpha) { paint.setAlpha(alpha); }
    @Override public int getAlpha() { return paint.getAlpha(); }
    @Override public void setColorFilter(android.graphics.ColorFilter filter) { paint.setColorFilter(filter); }
    @Override public int getOpacity() { return android.graphics.PixelFormat.TRANSLUCENT; }
    @Override public int getIntrinsicWidth() { return 1024; }
    @Override public int getIntrinsicHeight() { return 1024; }
}
