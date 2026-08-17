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
 * Player sprite renderer.
 * Sheet layout: 4 directions x 6 actions, with 3 real frames per action.
 * The source PNG is 6144x4096, therefore it is treated as 18 frame-columns x 4 rows.
 */
public final class KingSpriteDrawable extends Drawable {
    public static final int ACTION_IDLE = 0;
    public static final int ACTION_WALK = 1;
    public static final int ACTION_RUN = 2;
    public static final int ACTION_ATTACK = 3;
    public static final int ACTION_HURT = 4;
    public static final int ACTION_DIE = 5;
    public static final int FRAME_COUNT = 3;
    private static final int ACTION_COUNT = 6;
    private static final int DIRECTION_COUNT = 4;
    private static final int TOTAL_COLUMNS = ACTION_COUNT * FRAME_COUNT;

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final BitmapRegionDecoder decoder;
    private final Rect source = new Rect();
    private Bitmap frame;
    private int direction = -1, action = -1, frameIndex = -1;

    public KingSpriteDrawable(Context context) {
        BitmapRegionDecoder d = null;
        try (InputStream in = context.getAssets().open("player/king_sprite_sheet.png")) {
            d = BitmapRegionDecoder.newInstance(in, false);
        } catch (Exception ignored) { }
        decoder = d;
        setAlpha(255);
        setState(0, ACTION_IDLE, 0);
    }

    public void setState(int direction, int action, int frameIndex) {
        direction = Math.max(0, Math.min(DIRECTION_COUNT - 1, direction));
        action = Math.max(0, Math.min(ACTION_COUNT - 1, action));
        frameIndex = Math.max(0, Math.min(FRAME_COUNT - 1, frameIndex));
        if (decoder == null) return;
        if (this.direction == direction && this.action == action && this.frameIndex == frameIndex && frame != null) return;
        this.direction = direction;
        this.action = action;
        this.frameIndex = frameIndex;

        final int sheetW = decoder.getWidth();
        final int sheetH = decoder.getHeight();
        // 6 actions x 3 real frames = 18 columns, 4 directions = 4 rows.
        // Use proportional integer boundaries so the 6144px sheet is never rounded
        // as a simple 6x4 grid and no neighboring pose can enter the source rectangle.
        final int frameColumn = action * FRAME_COUNT + frameIndex;
        final int left = Math.round(frameColumn * sheetW / (float) TOTAL_COLUMNS);
        final int right = Math.round((frameColumn + 1) * sheetW / (float) TOTAL_COLUMNS);
        final int top = Math.round(direction * sheetH / (float) DIRECTION_COUNT);
        final int bottom = Math.round((direction + 1) * sheetH / (float) DIRECTION_COUNT);
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

    /** Compatibility helper for callers that only need idle/walk-style frame selection. */
    public void setState(int direction, int frameIndex) {
        setState(direction, ACTION_WALK, frameIndex);
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
    @Override public int getIntrinsicWidth() { return 342; }
    @Override public int getIntrinsicHeight() { return 1024; }
}
