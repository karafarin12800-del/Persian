package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.BitmapRegionDecoder;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Rect;
import android.graphics.drawable.Drawable;
import java.io.InputStream;

/** Player sprite-sheet renderer: 6 frames x 4 directions, with black background removed. */
public final class KingSpriteDrawable extends Drawable {
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final BitmapRegionDecoder decoder;
    private final Rect source = new Rect();
    private Bitmap frame;
    private int direction = 0, frameIndex = 0;

    public KingSpriteDrawable(Context context) {
        BitmapRegionDecoder d = null;
        try (InputStream in = context.getAssets().open("player/king_sprite_sheet.png")) {
            d = BitmapRegionDecoder.newInstance(in, false);
        } catch (Exception ignored) { }
        decoder = d;
        setState(0, 0);
        setAlpha(255);
    }

    public void setState(int direction, int frameIndex) {
        direction = Math.max(0, Math.min(3, direction));
        frameIndex = Math.max(0, Math.min(5, frameIndex));
        if (decoder == null) return;
        if (this.direction == direction && this.frameIndex == frameIndex && frame != null) return;
        this.direction = direction;
        this.frameIndex = frameIndex;

        int fw = decoder.getWidth() / 6;
        int fh = decoder.getHeight() / 4;
        source.set(frameIndex * fw, direction * fh,
                (frameIndex + 1) * fw, (direction + 1) * fh);

        BitmapFactory.Options o = new BitmapFactory.Options();
        o.inScaled = false;
        o.inPreferredConfig = Bitmap.Config.ARGB_8888;
        Bitmap decoded = decoder.decodeRegion(source, o);
        Bitmap cleaned = removeBlackBackgroundAndCrop(decoded);
        if (decoded != cleaned && decoded != null && !decoded.isRecycled()) decoded.recycle();

        Bitmap old = frame;
        frame = cleaned;
        if (old != null && !old.isRecycled()) old.recycle();
        invalidateSelf();
    }

    private Bitmap removeBlackBackgroundAndCrop(Bitmap src) {
        if (src == null) return null;
        Bitmap b = src.copy(Bitmap.Config.ARGB_8888, true);
        if (b == null) return src;

        int w = b.getWidth(), h = b.getHeight();
        int[] px = new int[w * h];
        b.getPixels(px, 0, w, 0, 0, w, h);

        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                int i = y * w + x;
                int c = px[i];
                int a = Color.alpha(c);
                int r = Color.red(c), g = Color.green(c), bl = Color.blue(c);
                // The source artwork has a solid black matte. Remove only near-black pixels;
                // this keeps dark Persian costume details intact.
                if (a > 0 && r < 24 && g < 24 && bl < 24) {
                    px[i] = Color.TRANSPARENT;
                } else if (a > 0) {
                    minX = Math.min(minX, x);
                    minY = Math.min(minY, y);
                    maxX = Math.max(maxX, x);
                    maxY = Math.max(maxY, y);
                }
            }
        }
        b.setPixels(px, 0, w, 0, 0, w, h);
        if (maxX < minX || maxY < minY) return b;

        // Remove empty matte around the character so the player is no longer perceived as a square.
        int padX = Math.max(4, (maxX - minX) / 12);
        int padY = Math.max(4, (maxY - minY) / 12);
        minX = Math.max(0, minX - padX);
        minY = Math.max(0, minY - padY);
        maxX = Math.min(w - 1, maxX + padX);
        maxY = Math.min(h - 1, maxY + padY);
        return Bitmap.createBitmap(b, minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    @Override public void draw(Canvas canvas) {
        if (frame == null) return;
        Rect bounds = getBounds();
        paint.setAlpha(255);
        canvas.drawBitmap(frame, null, bounds, paint);
    }

    @Override public void setAlpha(int alpha) { paint.setAlpha(alpha); }
    @Override public int getAlpha() { return paint.getAlpha(); }
    @Override public void setColorFilter(android.graphics.ColorFilter filter) { paint.setColorFilter(filter); }
    @Override public int getOpacity() { return android.graphics.PixelFormat.TRANSLUCENT; }
    @Override public int getIntrinsicWidth() { return 512; }
    @Override public int getIntrinsicHeight() { return 768; }
}
