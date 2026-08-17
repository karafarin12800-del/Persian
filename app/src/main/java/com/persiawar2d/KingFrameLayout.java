package com.persiawar2d;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Rect;

/** Extracts one of the 3 real poses inside a 1024x1024 action/direction cell. */
public final class KingFrameLayout {
    private KingFrameLayout() {}

    public static final int CELL_SIZE = 1024;
    public static final int ACTIONS = 6;
    public static final int DIRECTIONS = 4;
    public static final int FRAMES = 3;

    public static Bitmap frame(Bitmap sheet, int action, int direction, int frame) {
        if (sheet == null) throw new IllegalArgumentException("sheet == null");
        action = Math.max(0, Math.min(ACTIONS - 1, action));
        direction = Math.max(0, Math.min(DIRECTIONS - 1, direction));
        frame = Math.max(0, Math.min(FRAMES - 1, frame));

        int left = action * CELL_SIZE;
        int top = direction * CELL_SIZE;
        int frameW = CELL_SIZE / FRAMES;
        int frameLeft = left + frame * frameW;
        int frameRight = frame == FRAMES - 1 ? left + CELL_SIZE : frameLeft + frameW;
        Bitmap out = Bitmap.createBitmap(frameRight - frameLeft, CELL_SIZE, Bitmap.Config.ARGB_8888);
        Canvas canvas = new Canvas(out);
        Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
        canvas.drawBitmap(sheet,
                new Rect(frameLeft, top, frameRight, top + CELL_SIZE),
                new Rect(0, 0, out.getWidth(), out.getHeight()), paint);
        return out;
    }
}
