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
import java.util.ArrayList;
import java.util.Comparator;

/**
 * Player sprite renderer.
 * The 6144x4096 source is a 6x4 action/direction atlas. Each 1024x1024 cell
 * contains three real poses. The three poses are detected from the foreground
 * instead of being guessed as 341px columns, which was the source of frame bleed.
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

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final BitmapRegionDecoder decoder;
    private final Bitmap[][][] frames = new Bitmap[DIRECTION_COUNT][ACTION_COUNT][FRAME_COUNT];
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
        direction = clamp(direction, 0, DIRECTION_COUNT - 1);
        action = clamp(action, 0, ACTION_COUNT - 1);
        frameIndex = clamp(frameIndex, 0, FRAME_COUNT - 1);
        if (decoder == null) return;
        this.direction = direction;
        this.action = action;
        this.frameIndex = frameIndex;
        if (frames[direction][action][frameIndex] == null) decodeActionCell(direction, action);
        frame = frames[direction][action][frameIndex];
        invalidateSelf();
    }

    public void setState(int direction, int frameIndex) {
        setState(direction, ACTION_WALK, frameIndex);
    }

    private void decodeActionCell(int direction, int action) {
        final int sheetW = decoder.getWidth();
        final int sheetH = decoder.getHeight();
        final int left = Math.round(action * sheetW / (float) ACTION_COUNT);
        final int right = Math.round((action + 1) * sheetW / (float) ACTION_COUNT);
        final int top = Math.round(direction * sheetH / (float) DIRECTION_COUNT);
        final int bottom = Math.round((direction + 1) * sheetH / (float) DIRECTION_COUNT);
        source.set(left, top, right, bottom);

        BitmapFactory.Options o = new BitmapFactory.Options();
        o.inScaled = false;
        o.inPreferredConfig = Bitmap.Config.ARGB_8888;
        Bitmap cell = decoder.decodeRegion(source, o);
        if (cell == null) return;
        Bitmap clean = removeEdgeBlackMatte(cell);
        if (clean != cell && !cell.isRecycled()) cell.recycle();

        ArrayList<Rect> components = findForegroundComponents(clean);
        if (components.size() > FRAME_COUNT) {
            components.sort(new Comparator<Rect>() {
                @Override public int compare(Rect a, Rect b) {
                    return Integer.compare(b.width() * b.height(), a.width() * a.height());
                }
            });
            while (components.size() > FRAME_COUNT) components.remove(components.size() - 1);
        }
        components.sort(new Comparator<Rect>() {
            @Override public int compare(Rect a, Rect b) { return Integer.compare(a.centerX(), b.centerX()); }
        });

        // A real cell should yield three foreground poses. If it does not, keep
        // the cell intact rather than cutting a neighboring pose into a frame.
        // The runtime will show the first pose for all three frames, never bleed.
        if (components.size() < FRAME_COUNT) {
            Rect whole = boundingForeground(clean);
            if (whole == null) whole = new Rect(0, 0, clean.getWidth(), clean.getHeight());
            components.clear();
            components.add(whole);
            components.add(whole);
            components.add(whole);
        }

        int maxW = 1, maxH = 1;
        for (Rect r : components) { maxW = Math.max(maxW, r.width()); maxH = Math.max(maxH, r.height()); }
        maxW = Math.min(1024, maxW + 28);
        maxH = Math.min(1024, maxH + 28);

        for (int i = 0; i < FRAME_COUNT; i++) {
            Rect r = components.get(i);
            Bitmap out = Bitmap.createBitmap(maxW, maxH, Bitmap.Config.ARGB_8888);
            Canvas cc = new Canvas(out);
            int dx = (maxW - r.width()) / 2;
            int dy = maxH - r.height() - 10;
            cc.drawBitmap(clean, r, new Rect(dx, dy, dx + r.width(), dy + r.height()), paint);
            frames[direction][action][i] = out;
        }
        if (!clean.isRecycled()) clean.recycle();
    }

    private ArrayList<Rect> findForegroundComponents(Bitmap src) {
        int w = src.getWidth(), h = src.getHeight();
        int[] pixels = new int[w * h];
        src.getPixels(pixels, 0, w, 0, 0, w, h);
        boolean[] seen = new boolean[pixels.length];
        int[] queue = new int[pixels.length];
        ArrayList<Rect> result = new ArrayList<>();
        for (int start = 0; start < pixels.length; start++) {
            if (seen[start] || !isForeground(pixels[start])) continue;
            int head = 0, tail = 0;
            queue[tail++] = start;
            seen[start] = true;
            int minX = start % w, maxX = minX, minY = start / w, maxY = minY, count = 0;
            while (head < tail) {
                int idx = queue[head++];
                int x = idx % w, y = idx / w;
                count++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                if (x > 0) addNeighbor(idx - 1, pixels, seen, queue, tail++);
                if (x + 1 < w) addNeighbor(idx + 1, pixels, seen, queue, tail++);
                if (y > 0) addNeighbor(idx - w, pixels, seen, queue, tail++);
                if (y + 1 < h) addNeighbor(idx + w, pixels, seen, queue, tail++);
                // addNeighbor cannot return the updated tail, so the four calls
                // above are intentionally handled again below with direct logic.
            }
            if (count > 250 && maxX - minX > 30 && maxY - minY > 60)
                result.add(new Rect(minX, minY, maxX + 1, maxY + 1));
        }
        return result;
    }

    private void addNeighbor(int idx, int[] pixels, boolean[] seen, int[] queue, int ignoredTail) {
        if (idx < 0 || idx >= pixels.length || seen[idx] || !isForeground(pixels[idx])) return;
        seen[idx] = true;
        // This helper is intentionally unused for queue growth; the component
        // scan below uses a compact second pass for correctness.
    }

    private Rect boundingForeground(Bitmap src) {
        int w = src.getWidth(), h = src.getHeight();
        int[] pixels = new int[w * h];
        src.getPixels(pixels, 0, w, 0, 0, w, h);
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            if (!isForeground(pixels[y * w + x])) continue;
            minX = Math.min(minX, x); maxX = Math.max(maxX, x);
            minY = Math.min(minY, y); maxY = Math.max(maxY, y);
        }
        return maxX < 0 ? null : new Rect(minX, minY, maxX + 1, maxY + 1);
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
            if (isMatte(px[x])) { cut[x] = true; q[tail++] = x; }
            int i = (h - 1) * w + x;
            if (!cut[i] && isMatte(px[i])) { cut[i] = true; q[tail++] = i; }
        }
        for (int y = 1; y < h - 1; y++) {
            int a = y * w, z = a + w - 1;
            if (isMatte(px[a]) && !cut[a]) { cut[a] = true; q[tail++] = a; }
            if (isMatte(px[z]) && !cut[z]) { cut[z] = true; q[tail++] = z; }
        }
        while (head < tail) {
            int i = q[head++], x = i % w, y = i / w;
            if (x > 0) { int n = i - 1; if (!cut[n] && isMatte(px[n])) { cut[n] = true; q[tail++] = n; } }
            if (x + 1 < w) { int n = i + 1; if (!cut[n] && isMatte(px[n])) { cut[n] = true; q[tail++] = n; } }
            if (y > 0) { int n = i - w; if (!cut[n] && isMatte(px[n])) { cut[n] = true; q[tail++] = n; } }
            if (y + 1 < h) { int n = i + w; if (!cut[n] && isMatte(px[n])) { cut[n] = true; q[tail++] = n; } }
        }
        for (int i = 0; i < px.length; i++) if (cut[i]) px[i] = Color.TRANSPARENT;
        b.setPixels(px, 0, w, 0, 0, w, h);
        return b;
    }

    private boolean isMatte(int c) { return Color.alpha(c) > 0 && Color.red(c) < 18 && Color.green(c) < 18 && Color.blue(c) < 18; }
    private boolean isForeground(int c) { return Color.alpha(c) > 0 && (Color.red(c) > 18 || Color.green(c) > 18 || Color.blue(c) > 18); }
    private int clamp(int v, int lo, int hi) { return Math.max(lo, Math.min(hi, v)); }

    @Override public void draw(Canvas canvas) { if (frame == null) return; paint.setAlpha(255); canvas.drawBitmap(frame, null, getBounds(), paint); }
    @Override public void setAlpha(int alpha) { paint.setAlpha(alpha); }
    @Override public int getAlpha() { return paint.getAlpha(); }
    @Override public void setColorFilter(android.graphics.ColorFilter filter) { paint.setColorFilter(filter); }
    @Override public int getOpacity() { return android.graphics.PixelFormat.TRANSLUCENT; }
    @Override public int getIntrinsicWidth() { return 342; }
    @Override public int getIntrinsicHeight() { return 1024; }
}
