package com.persiawar2d;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.view.View;

/** Compact guide map for the Level 12 3D city. It is intentionally readable during combat. */
public final class BattleMiniMapView extends View {
    private final Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint s = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final CityKit city = new CityKit(20261201L);
    private final float world = 54f;
    private float pulse;

    public BattleMiniMapView(Context c) {
        super(c);
        setWillNotDraw(false);
        s.setStyle(Paint.Style.STROKE);
    }

    @Override protected void onDraw(Canvas c) {
        super.onDraw(c);
        float size = Math.min(300f, Math.min(getWidth() * .28f, getHeight() * .34f));
        if (size < 120f) return;
        float left = getWidth() - size - 18f;
        float top = 18f;
        RectF box = new RectF(left, top, left + size, top + size);

        p.setStyle(Paint.Style.FILL);
        p.setColor(Color.argb(220, 18, 24, 20));
        c.drawRoundRect(box, 18f, 18f, p);
        s.setStrokeWidth(3f);
        s.setColor(Color.argb(225, 224, 193, 108));
        c.drawRoundRect(box, 18f, 18f, s);

        float pad = 18f, x0 = box.left + pad, y0 = box.top + pad;
        float map = size - pad * 2f;
        p.setColor(Color.rgb(67, 78, 62));
        c.drawRect(x0, y0, x0 + map, y0 + map, p);

        float scale = map / (world * 2f);
        for (CityKit.Piece q : city.pieces()) {
            float x = x0 + (q.x + world) * scale;
            float y = y0 + (q.z + world) * scale;
            float w = Math.max(2f, q.w * scale);
            float d = Math.max(2f, q.d * scale);
            switch (q.type) {
                case ROAD: p.setColor(Color.rgb(112, 108, 94)); break;
                case SIDEWALK: p.setColor(Color.rgb(154, 146, 120)); break;
                case HOUSE_1: case HOUSE_2: case WAREHOUSE: case SHOP: p.setColor(Color.rgb(179, 151, 105)); break;
                case WALL: p.setColor(Color.rgb(93, 82, 66)); break;
                case TREE: p.setColor(Color.rgb(42, 111, 54)); w = d = Math.max(4f, 2.2f * scale); break;
                case CAR: p.setColor(Color.rgb(58, 75, 82)); break;
                case RUBBLE: p.setColor(Color.rgb(120, 103, 83)); break;
                default: continue;
            }
            c.drawRect(x - w / 2f, y - d / 2f, x + w / 2f, y + d / 2f, p);
        }

        // Central objective / unknown anomaly zone.
        float cx = x0 + world * scale, cy = y0 + world * scale;
        p.setStyle(Paint.Style.STROKE);
        p.setStrokeWidth(2.5f);
        p.setColor(Color.argb(220, 255, 196, 64));
        c.drawCircle(cx, cy, 18f, p);
        p.setStyle(Paint.Style.FILL);
        p.setColor(Color.argb(70, 255, 196, 64));
        c.drawCircle(cx, cy, 18f, p);
        pulse += .08f;
        float pr = 22f + (float)Math.sin(pulse) * 3f;
        p.setStyle(Paint.Style.STROKE);
        p.setStrokeWidth(1.5f);
        p.setColor(Color.argb(100, 255, 196, 64));
        c.drawCircle(cx, cy, pr, p);

        // Start marker: the battle begins in the southern approach.
        float sx = cx, sy = y0 + (31f + world) * scale;
        p.setStyle(Paint.Style.FILL);
        p.setColor(Color.rgb(72, 235, 112));
        c.drawCircle(sx, sy, 5.5f, p);

        p.setTextSize(Math.max(11f, size * .055f));
        p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        p.setColor(Color.WHITE);
        c.drawText("MAP / GUIDE", box.left + 14f, box.top + size - 9f, p);
        p.setTextSize(Math.max(9f, size * .042f));
        p.setColor(Color.rgb(255, 205, 80));
        c.drawText("OBJECTIVE", box.left + size - 78f, box.top + 20f, p);
        p.setStyle(Paint.Style.FILL);
        postInvalidateOnAnimation();
    }
}
