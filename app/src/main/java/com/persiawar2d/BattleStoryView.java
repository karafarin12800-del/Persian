package com.persiawar2d;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.view.View;

/** Short in-game mission briefing, shown without interrupting the playable 3D scene. */
public final class BattleStoryView extends View {
    private final Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final long started = System.currentTimeMillis();

    public BattleStoryView(Context c) {
        super(c);
        setWillNotDraw(false);
        setClickable(false);
        setFocusable(false);
    }

    @Override protected void onDraw(Canvas c) {
        super.onDraw(c);
        long age = System.currentTimeMillis() - started;
        if (age > 9000L) return;
        float alpha = age < 900L ? age / 900f : age > 7600L ? (9000L - age) / 1400f : 1f;
        alpha = Math.max(0f, Math.min(1f, alpha));
        float w = Math.min(760f, getWidth() * .82f);
        float h = 150f;
        float l = (getWidth() - w) / 2f;
        float t = getHeight() * .18f;
        p.setStyle(Paint.Style.FILL);
        p.setColor(Color.argb((int)(190 * alpha), 8, 12, 10));
        c.drawRoundRect(new RectF(l, t, l + w, t + h), 22f, 22f, p);

        p.setTextAlign(Paint.Align.CENTER);
        p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        p.setColor(Color.argb((int)(255 * alpha), 236, 205, 126));
        p.setTextSize(25f);
        c.drawText("PERSIA WAR • LEVEL 12", getWidth() / 2f, t + 36f, p);

        p.setColor(Color.argb((int)(245 * alpha), 255, 255, 255));
        p.setTextSize(17f);
        if (age < 3500L) {
            c.drawText("شهر قدیمی پس از یک رویداد ناشناخته تخلیه شده است.", getWidth()/2f, t + 72f, p);
            c.drawText("وارد منطقه شو، زنده بمان و منبع اختلال را پیدا کن.", getWidth()/2f, t + 101f, p);
        } else if (age < 6200L) {
            c.drawText("ردی که پیدا کرده‌ای به میدان مرکزی می‌رسد...", getWidth()/2f, t + 72f, p);
            c.drawText("اما نیروهای دشمن قبل از تو به شهر رسیده‌اند.", getWidth()/2f, t + 101f, p);
        } else {
            c.drawText("هدف: رسیدن به محدوده طلایی روی نقشه راهنما.", getWidth()/2f, t + 72f, p);
            c.drawText("حرکت با جوی‌استیک چپ • نشانه‌گیری و شلیک با سمت راست", getWidth()/2f, t + 101f, p);
        }
        p.setTextSize(12f);
        p.setColor(Color.argb((int)(190 * alpha), 205, 215, 205));
        c.drawText("BRIEFING", getWidth()/2f, t + 132f, p);
        postInvalidateOnAnimation();
    }
}
