package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.view.MotionEvent;
import android.view.View;

import java.io.InputStream;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * Software 2.5D scene renderer. Objects are transformed individually into an
 * isometric world; the screen itself is never rotated.
 */
public final class TwoPointFiveDView extends View {
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final Paint shadowPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final ArrayList<WorldSprite> buildings = new ArrayList<>();
    private Bitmap characterSheet;
    private Bitmap characterFrame;
    private float cameraX = 8f;
    private float cameraY = 8f;
    private float time;
    private long lastNanos;

    public TwoPointFiveDView(Context context) {
        super(context);
        setLayerType(View.LAYER_TYPE_HARDWARE, null);
        shadowPaint.setColor(0x55000000);
        loadCharacter();
        createCityLayout();
    }

    private void loadCharacter() {
        try (InputStream in = getContext().getAssets().open("player/king_sprite_sheet.png")) {
            characterSheet = BitmapFactory.decodeStream(in);
            characterFrame = KingFrameLayout.frame(characterSheet, 1, 0, 1);
        } catch (Exception ignored) {
            characterSheet = null;
        }
    }

    private void createCityLayout() {
        // Structured blocks, not random floating roofs. Every building has a
        // ground anchor and a stable footprint.
        float[][] p = {
                {2f, 2f, 2.0f, 2.0f}, {6f, 2f, 2.2f, 2.0f}, {10f, 2f, 2.0f, 2.0f},
                {2f, 7f, 2.0f, 2.2f}, {10f, 7f, 2.4f, 2.0f},
                {2f, 12f, 2.0f, 2.0f}, {6f, 12f, 2.4f, 2.0f}, {10f, 12f, 2.0f, 2.2f}
        };
        for (float[] v : p) {
            buildings.add(new WorldSprite(v[0], v[1], v[2], v[3] * 34f, 0f,
                    SceneLayer.STRUCTURE.order, true));
        }
    }

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        long now = System.nanoTime();
        if (lastNanos != 0L) time += (now - lastNanos) / 1_000_000_000f;
        lastNanos = now;

        canvas.drawColor(0xff817354);
        drawGround(canvas);
        drawRoads(canvas);

        ArrayList<WorldSprite> sorted = new ArrayList<>(buildings);
        Collections.sort(sorted, (a, b) -> {
            int c = Float.compare(IsometricMath.depth(a.x, a.y, a.height),
                    IsometricMath.depth(b.x, b.y, b.height));
            if (c != 0) return c;
            return Integer.compare(a.layer, b.layer);
        });

        for (WorldSprite b : sorted) drawBuildingPlaceholder(canvas, b);
        drawFoliage(canvas);
        drawCharacter(canvas, 8.0f + (float)Math.sin(time * 0.8f) * 0.7f, 8.0f);
        drawHud(canvas);
        postInvalidateOnAnimation();
    }

    private float sx(float x, float y) {
        return getWidth() * 0.5f + (x - cameraX - y + cameraY) * 46f;
    }

    private float sy(float x, float y, float h) {
        return getHeight() * 0.48f + (x - cameraX + y - cameraY) * 24f - h;
    }

    private void drawGround(Canvas c) {
        Paint p = paint;
        p.setStyle(Paint.Style.FILL);
        p.setColor(0xff8a7957);
        c.drawRect(0, 0, getWidth(), getHeight(), p);
        p.setColor(0x1a5f5138);
        for (int x = 0; x < 18; x++) {
            for (int y = 0; y < 18; y++) {
                float px = sx(x, y), py = sy(x, y, 0);
                c.drawCircle(px, py, 2.2f, p);
            }
        }
    }

    private void drawRoads(Canvas c) {
        Paint p = paint;
        p.setColor(0xff3f3d38);
        p.setStrokeWidth(34f);
        p.setStrokeCap(Paint.Cap.BUTT);
        for (int y = 4; y <= 14; y += 5) {
            c.drawLine(sx(0, y), sy(0, y, 0), sx(16, y), sy(16, y, 0), p);
        }
        p.setColor(0xffb6a979);
        p.setStrokeWidth(3f);
        for (int y = 4; y <= 14; y += 5) {
            for (int x = 1; x < 16; x += 2) {
                c.drawLine(sx(x, y), sy(x, y, 0), sx(x + 0.7f, y), sy(x + 0.7f, y, 0), p);
            }
        }
    }

    private void drawBuildingPlaceholder(Canvas c, WorldSprite b) {
        float x = sx(b.x, b.y);
        float y = sy(b.x, b.y, 0);
        float w = b.width;
        float h = Math.max(62f, b.visualHeight);
        paint.setColor(0x22000000);
        c.drawOval(new RectF(x - w * .48f, y - 5, x + w * .48f, y + 12), paint);
        paint.setColor(0xffd7cdb0);
        c.drawRect(x - w * .42f, y - h, x + w * .42f, y, paint);
        paint.setColor(0xffb85b52);
        c.drawRect(x - w * .45f, y - h - 10, x + w * .45f, y - h + 5, paint);
        paint.setColor(0xff8f8a78);
        c.drawRect(x - w * .32f, y - h * .55f, x - w * .18f, y - h * .32f, paint);
        c.drawRect(x + w * .18f, y - h * .55f, x + w * .32f, y - h * .32f, paint);
    }

    private void drawFoliage(Canvas c) {
        float[][] trees = {{4f, 5.5f}, {8.2f, 5.6f}, {13f, 6f}, {5f, 10f}, {13f, 10.5f}};
        for (float[] t : trees) {
            float x = sx(t[0], t[1]), y = sy(t[0], t[1], 0);
            paint.setColor(0x30000000);
            c.drawOval(new RectF(x - 18, y - 3, x + 18, y + 10), paint);
            paint.setColor(0xff6d472e);
            c.drawRect(x - 5, y - 34, x + 5, y, paint);
            paint.setColor(0xff4c7d3f);
            c.drawCircle(x, y - 46, 24, paint);
            paint.setColor(0xff65994c);
            c.drawCircle(x + 10, y - 50, 16, paint);
        }
    }

    private void drawCharacter(Canvas c, float xw, float yw) {
        float x = sx(xw, yw);
        float y = sy(xw, yw, 0);
        paint.setColor(0x44000000);
        c.drawOval(new RectF(x - 20, y - 4, x + 20, y + 8), paint);
        if (characterFrame != null) {
            float h = 86f;
            float w = h * characterFrame.getWidth() / (float) characterFrame.getHeight();
            RectF dst = new RectF(x - w * .5f, y - h, x + w * .5f, y);
            c.drawBitmap(characterFrame, null, dst, paint);
        } else {
            paint.setColor(0xffd6b28d);
            c.drawCircle(x, y - 62, 14, paint);
            paint.setColor(0xff6a3434);
            c.drawRect(x - 14, y - 50, x + 14, y - 16, paint);
        }
    }

    private void drawHud(Canvas c) {
        paint.setColor(0xaa111512);
        c.drawRect(0, 0, getWidth(), 58, paint);
        paint.setColor(Color.WHITE);
        paint.setTextSize(18f);
        c.drawText("PERSIA WAR 2.5D", 18, 24, paint);
        paint.setTextSize(13f);
        c.drawText("2.5D RENDER PREVIEW", 18, 45, paint);
    }

    @Override
    public boolean onTouchEvent(MotionEvent event) {
        if (event.getActionMasked() == MotionEvent.ACTION_MOVE ||
                event.getActionMasked() == MotionEvent.ACTION_DOWN) {
            float dx = event.getX() - getWidth() * .5f;
            float dy = event.getY() - getHeight() * .5f;
            cameraX = 8f - dx / 300f;
            cameraY = 8f - dy / 300f;
            return true;
        }
        return true;
    }
}
