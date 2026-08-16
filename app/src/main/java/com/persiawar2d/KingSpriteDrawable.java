package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Rect;
import android.graphics.drawable.Drawable;
import java.io.InputStream;
import java.util.ArrayDeque;

/** Player renderer for the new ChatGPT king artwork. */
public final class KingSpriteDrawable extends Drawable {
    private static final String ART = "ChatGPT Image ۲۶ مرداد ۱۴۰۵، ۰۲_۳۳_۴۸.png";
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private Bitmap frame;
    private int direction = 0;
    private int frameIndex = 0;

    public KingSpriteDrawable(Context context) {
        frame = loadAndClean(context);
        setAlpha(255);
    }

    private Bitmap loadAndClean(Context context) {
        try (InputStream in = context.getAssets().open(ART)) {
            BitmapFactory.Options o = new BitmapFactory.Options();
            o.inScaled = false;
            Bitmap source = BitmapFactory.decodeStream(in, null, o);
            return source == null ? null : removeOnlyOuterBlack(source);
        } catch (Exception ignored) {
            return null;
        }
    }

    /** Removes only near-black pixels connected to the image border. Internal black stays. */
    private Bitmap removeOnlyOuterBlack(Bitmap src) {
        int w = src.getWidth(), h = src.getHeight();
        if (w <= 2 || h <= 2) return src;
        int[] pixels = new int[w * h];
        src.getPixels(pixels, 0, w, 0, 0, w, h);
        boolean[] seen = new boolean[pixels.length];
        ArrayDeque<Integer> q = new ArrayDeque<>();
        for (int x = 0; x < w; x++) {
            enqueueBackground(pixels, seen, q, x, 0, w);
            enqueueBackground(pixels, seen, q, x, h - 1, w);
        }
        for (int y = 1; y < h - 1; y++) {
            enqueueBackground(pixels, seen, q, 0, y, w);
            enqueueBackground(pixels, seen, q, w - 1, y, w);
        }
        while (!q.isEmpty()) {
            int i = q.removeFirst();
            int x = i % w, y = i / w;
            pixels[i] = Color.TRANSPARENT;
            if (x > 0) enqueueBackground(pixels, seen, q, x - 1, y, w);
            if (x + 1 < w) enqueueBackground(pixels, seen, q, x + 1, y, w);
            if (y > 0) enqueueBackground(pixels, seen, q, x, y - 1, w);
            if (y + 1 < h) enqueueBackground(pixels, seen, q, x, y + 1, w);
        }
        Bitmap cleaned = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888);
        cleaned.setPixels(pixels, 0, w, 0, 0, w, h);
        if (!src.isRecycled()) src.recycle();
        return cropToOpaqueBounds(cleaned);
    }

    private void enqueueBackground(int[] pixels, boolean[] seen, ArrayDeque<Integer> q, int x, int y, int w) {
        int i = y * w + x;
        if (seen[i]) return;
        int c = pixels[i];
        if (Color.alpha(c) > 0 && Color.red(c) <= 38 && Color.green(c) <= 38 && Color.blue(c) <= 38) {
            seen[i] = true;
            q.addLast(i);
        }
    }

    private Bitmap cropToOpaqueBounds(Bitmap src) {
        int w = src.getWidth(), h = src.getHeight();
        int[] pixels = new int[w * h];
        src.getPixels(pixels, 0, w, 0, 0, w, h);
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            if (Color.alpha(pixels[y * w + x]) > 8) {
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
            }
        }
        if (maxX < minX || maxY < minY) return src;
        int padX = Math.max(4, (maxX - minX + 1) / 24);
        int padY = Math.max(4, (maxY - minY + 1) / 24);
        minX = Math.max(0, minX - padX); maxX = Math.min(w - 1, maxX + padX);
        minY = Math.max(0, minY - padY); maxY = Math.min(h - 1, maxY + padY);
        Bitmap out = Bitmap.createBitmap(src, minX, minY, maxX - minX + 1, maxY - minY + 1);
        if (!src.isRecycled()) src.recycle();
        return out;
    }

    public void setState(int direction, int frameIndex) {
        this.direction = Math.max(0, Math.min(3, direction));
        this.frameIndex = Math.max(0, Math.min(5, frameIndex));
        invalidateSelf();
    }

    @Override public void draw(Canvas canvas) {
        if (frame == null) return;
        paint.setAlpha(255);
        canvas.drawBitmap(frame, null, getBounds(), paint);
    }
    @Override public void setAlpha(int alpha) { paint.setAlpha(alpha); }
    @Override public int getAlpha() { return paint.getAlpha(); }
    @Override public void setColorFilter(android.graphics.ColorFilter filter) { paint.setColorFilter(filter); }
    @Override public int getOpacity() { return android.graphics.PixelFormat.TRANSLUCENT; }
    @Override public int getIntrinsicWidth() { return frame != null ? frame.getWidth() : 512; }
    @Override public int getIntrinsicHeight() { return frame != null ? frame.getHeight() : 512; }
}
