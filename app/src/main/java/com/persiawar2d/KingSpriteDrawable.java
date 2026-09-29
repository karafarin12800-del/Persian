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

/**
 * Safe player renderer for the supplied king artwork.
 *
 * The artwork in player/king_sprite_sheet.png is a vertical strip: the
 * complete character occupies the width of the image and animation frames
 * are stacked vertically. The previous implementation treated the image as
 * a 6x4 action/direction atlas, which cut one character into narrow slices
 * and caused several copies/parts of the king to appear together.
 *
 * For stability we deliberately render one complete frame at a time. The
 * three vertical frames are used for idle/walk/attack timing, while the
 * gameplay state still controls the animation speed. This keeps the player
 * visually correct even though the source artwork does not contain the
 * assumed 6x4 atlas.
 */
public final class KingSpriteDrawable extends Drawable {
    public static final int ACTION_IDLE=0, ACTION_WALK=1, ACTION_RUN=2,
            ACTION_ATTACK=3, ACTION_HURT=4, ACTION_DIE=5;
    public static final int FRAME_COUNT=3;

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final BitmapRegionDecoder decoder;
    private final Bitmap[] frames = new Bitmap[FRAME_COUNT];
    private Bitmap frame;

    public KingSpriteDrawable(Context context) {
        BitmapRegionDecoder d = null;
        try (InputStream in = context.getAssets().open("player/king_sprite_sheet.png")) {
            d = BitmapRegionDecoder.newInstance(in, false);
        } catch (Exception ignored) {}
        decoder = d;
        setAlpha(255);
        setState(0, ACTION_IDLE, 0);
    }

    public void setState(int direction, int action, int frameIndex) {
        frameIndex = clamp(frameIndex, 0, FRAME_COUNT - 1);
        if (decoder == null) return;
        if (frames[frameIndex] == null) decodeFrame(frameIndex);
        frame = frames[frameIndex];
        invalidateSelf();
    }

    public void setState(int direction, int frameIndex) {
        setState(direction, ACTION_WALK, frameIndex);
    }

    private void decodeFrame(int frameIndex) {
        int sw = decoder.getWidth();
        int sh = decoder.getHeight();

        // The supplied asset is a tall strip. Split it into three complete
        // character frames. Do not divide the character horizontally.
        int frameH = sh / FRAME_COUNT;
        int top = frameIndex * frameH;
        int bottom = (frameIndex == FRAME_COUNT - 1) ? sh : top + frameH;
        Rect region = new Rect(0, top, sw, bottom);

        BitmapFactory.Options o = new BitmapFactory.Options();
        o.inScaled = false;
        o.inPreferredConfig = Bitmap.Config.ARGB_8888;
        Bitmap raw = decoder.decodeRegion(region, o);
        if (raw == null) return;

        Bitmap clean = removeEdgeBlackMatte(raw);
        if (clean != raw && !raw.isRecycled()) raw.recycle();

        Rect b = foregroundBounds(clean);
        if (b == null || b.width() < 12 || b.height() < 20) {
            frames[frameIndex] = clean;
            return;
        }

        int pad = Math.max(8, Math.min(clean.getWidth(), clean.getHeight()) / 24);
        int l = Math.max(0, b.left - pad);
        int t = Math.max(0, b.top - pad);
        int r = Math.min(clean.getWidth(), b.right + pad);
        int bot = Math.min(clean.getHeight(), b.bottom + pad);
        Bitmap cropped = Bitmap.createBitmap(clean, l, t, r - l, bot - t);
        if (clean != cropped && !clean.isRecycled()) clean.recycle();
        frames[frameIndex] = cropped;
    }

    private Bitmap removeEdgeBlackMatte(Bitmap src) {
        Bitmap b = src.copy(Bitmap.Config.ARGB_8888, true);
        if (b == null) return src;
        int w = b.getWidth(), h = b.getHeight();
        int[] px = new int[w * h];
        b.getPixels(px, 0, w, 0, 0, w, h);
        boolean[] cut = new boolean[px.length];
        int[] q = new int[px.length];
        int head = 0, tail = 0;

        for (int x = 0; x < w; x++) {
            int a = x, z = (h - 1) * w + x;
            if (isMatte(px[a])) { cut[a] = true; q[tail++] = a; }
            if (isMatte(px[z]) && !cut[z]) { cut[z] = true; q[tail++] = z; }
        }
        for (int y = 1; y < h - 1; y++) {
            int a = y * w, z = a + w - 1;
            if (isMatte(px[a]) && !cut[a]) { cut[a] = true; q[tail++] = a; }
            if (isMatte(px[z]) && !cut[z]) { cut[z] = true; q[tail++] = z; }
        }
        while (head < tail) {
            int i = q[head++], x = i % w, y = i / w;
            if (x > 0) { int n=i-1; if (!cut[n] && isMatte(px[n])) { cut[n]=true; q[tail++]=n; } }
            if (x + 1 < w) { int n=i+1; if (!cut[n] && isMatte(px[n])) { cut[n]=true; q[tail++]=n; } }
            if (y > 0) { int n=i-w; if (!cut[n] && isMatte(px[n])) { cut[n]=true; q[tail++]=n; } }
            if (y + 1 < h) { int n=i+w; if (!cut[n] && isMatte(px[n])) { cut[n]=true; q[tail++]=n; } }
        }
        for (int i=0; i<px.length; i++) if (cut[i]) px[i] = Color.TRANSPARENT;
        b.setPixels(px, 0, w, 0, 0, w, h);
        return b;
    }

    private Rect foregroundBounds(Bitmap src) {
        int w=src.getWidth(), h=src.getHeight();
        int[] px=new int[w*h]; src.getPixels(px,0,w,0,0,w,h);
        int minX=w, minY=h, maxX=-1, maxY=-1;
        for (int y=0;y<h;y++) for (int x=0;x<w;x++) {
            if (isForeground(px[y*w+x])) {
                if (x<minX) minX=x; if (x>maxX) maxX=x;
                if (y<minY) minY=y; if (y>maxY) maxY=y;
            }
        }
        return maxX<0 ? null : new Rect(minX,minY,maxX+1,maxY+1);
    }

    private boolean isMatte(int c) {
        return Color.alpha(c)>0 && Color.red(c)<28 && Color.green(c)<28 && Color.blue(c)<28;
    }
    private boolean isForeground(int c) { return Color.alpha(c)>0 && !isMatte(c); }
    private int clamp(int v,int a,int b) { return Math.max(a,Math.min(b,v)); }

    @Override public void draw(Canvas c) {
        if (frame != null) { paint.setAlpha(255); c.drawBitmap(frame, null, getBounds(), paint); }
    }
    @Override public void setAlpha(int a) { paint.setAlpha(a); }
    @Override public int getAlpha() { return paint.getAlpha(); }
    @Override public void setColorFilter(android.graphics.ColorFilter f) { paint.setColorFilter(f); }
    @Override public int getOpacity() { return android.graphics.PixelFormat.TRANSLUCENT; }
    @Override public int getIntrinsicWidth() { return 342; }
    @Override public int getIntrinsicHeight() { return 342; }
}
