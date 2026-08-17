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

/** Stable 6x4 player sprite renderer. Keeps every frame on the same canvas size. */
public final class KingSpriteDrawable extends Drawable {
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final BitmapRegionDecoder decoder;
    private final Rect source = new Rect();
    private Bitmap frame;
    private int direction = -1, frameIndex = -1;

    public KingSpriteDrawable(Context context) {
        BitmapRegionDecoder d = null;
        try (InputStream in = context.getAssets().open("player/king_sprite_sheet.png")) {
            d = BitmapRegionDecoder.newInstance(in, false);
        } catch (Exception ignored) { }
        decoder = d;
        setAlpha(255);
        setState(0, 0);
    }

    public void setState(int direction, int frameIndex) {
        direction = Math.max(0, Math.min(3, direction));
        frameIndex = Math.max(0, Math.min(5, frameIndex));
        if (decoder == null) return;
        if (this.direction == direction && this.frameIndex == frameIndex && frame != null) return;
        this.direction = direction;
        this.frameIndex = frameIndex;

        final int sheetW = decoder.getWidth();
        final int sheetH = decoder.getHeight();
        final int fw = sheetW / 6;
        final int fh = sheetH / 4;
        // Never crop individual frames: changing the crop rectangle between animation
        // frames makes the character jump inside its box and can expose neighboring poses.
        final int left = frameIndex * fw;
        final int top = direction * fh;
        final int right = (frameIndex == 5) ? sheetW : (frameIndex + 1) * fw;
        final int bottom = (direction == 3) ? sheetH : (direction + 1) * fh;
        source.set(left, top, right, bottom);

        BitmapFactory.Options o = new BitmapFactory.Options();
        o.inScaled = false;
        o.inPreferredConfig = Bitmap.Config.ARGB_8888;
        Bitmap decoded = decoder.decodeRegion(source, o);
        Bitmap cleaned = removeEdgeBlackMatte(decoded);
        if (decoded != cleaned && decoded != null && !decoded.isRecycled()) decoded.recycle();

        Bitmap old = frame;
        frame = cleaned;
        if (old != null && !old.isRecycled()) old.recycle();
        invalidateSelf();
    }

    /** Removes only black pixels connected to the frame edge; dark costume pixels are preserved. */
    private Bitmap removeEdgeBlackMatte(Bitmap src) {
        if (src == null) return null;
        Bitmap b = src.copy(Bitmap.Config.ARGB_8888, true);
        if (b == null) return src;
        int w = b.getWidth(), h = b.getHeight();
        int[] px = new int[w * h];
        b.getPixels(px, 0, w, 0, 0, w, h);
        boolean[] transparent = new boolean[w * h];
        int[] queue = new int[w * h];
        int head = 0, tail = 0;
        for (int x = 0; x < w; x++) {
            if (isMatte(px[x])) { transparent[x] = true; queue[tail++] = x; }
            int i = (h - 1) * w + x;
            if (!transparent[i] && isMatte(px[i])) { transparent[i] = true; queue[tail++] = i; }
        }
        for (int y = 1; y < h - 1; y++) {
            int a = y * w, z = a + w - 1;
            if (isMatte(px[a]) && !transparent[a]) { transparent[a] = true; queue[tail++] = a; }
            if (isMatte(px[z]) && !transparent[z]) { transparent[z] = true; queue[tail++] = z; }
        }
        while (head < tail) {
            int i = queue[head++];
            int x = i % w, y = i / w;
            if (x > 0) { int n=i-1; if(!transparent[n] && isMatte(px[n])){transparent[n]=true;queue[tail++]=n;} }
            if (x + 1 < w) { int n=i+1; if(!transparent[n] && isMatte(px[n])){transparent[n]=true;queue[tail++]=n;} }
            if (y > 0) { int n=i-w; if(!transparent[n] && isMatte(px[n])){transparent[n]=true;queue[tail++]=n;} }
            if (y + 1 < h) { int n=i+w; if(!transparent[n] && isMatte(px[n])){transparent[n]=true;queue[tail++]=n;} }
        }
        for (int i = 0; i < px.length; i++) if (transparent[i]) px[i] = Color.TRANSPARENT;
        b.setPixels(px, 0, w, 0, 0, w, h);
        return b;
    }

    private boolean isMatte(int c) {
        return Color.alpha(c) > 0 && Color.red(c) < 18 && Color.green(c) < 18 && Color.blue(c) < 18;
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
    @Override public int getIntrinsicWidth() { return 512; }
    @Override public int getIntrinsicHeight() { return 768; }
}
