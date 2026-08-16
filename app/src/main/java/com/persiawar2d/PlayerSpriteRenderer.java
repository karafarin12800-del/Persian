package com.persiawar2d;

import android.content.Context;
import android.content.res.AssetManager;
import android.graphics.*;
import java.io.InputStream;

/**
 * Renders the supplied king sprite sheet directly from assets/player_king_sheet.png.
 * The sheet layout is: Front(3), Right(3), Back(2), Left(3) columns and
 * rows Idle, Walk, Run, Attack, Hurt, Die.
 * If the asset has not been copied into the repository yet, callers can use
 * the returned false value and keep the existing fallback artwork.
 */
public final class PlayerSpriteRenderer {
    private Bitmap sheet;
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private long stateStarted;
    private int lastState = -1;

    private static final int IDLE=0, WALK=1, RUN=2, ATTACK=3, HURT=4, DIE=5;
    private static final int[] COUNTS = {3,3,3,3};

    public boolean load(Context context) {
        if (sheet != null) return true;
        try {
            AssetManager am = context.getAssets();
            InputStream in = am.open("player_king_sheet.png");
            sheet = BitmapFactory.decodeStream(in);
            in.close();
            return sheet != null;
        } catch (Exception ignored) {
            return false;
        }
    }

    public void resetAnimation() { lastState=-1; stateStarted=0; }

    public boolean draw(Canvas c, float x, float y, float size, int direction, int state, float speed) {
        if (sheet == null || sheet.isRecycled()) return false;
        if (state != lastState) { lastState=state; stateStarted=System.currentTimeMillis(); }
        int col = directionColumn(direction);
        int count = directionFrameCount(direction);
        int frame = state == DIE ? Math.min(count-1, (int)((System.currentTimeMillis()-stateStarted)/150)) :
                (int)(((System.currentTimeMillis()-stateStarted)/Math.max(70, (int)(130/speed))) % count);
        Rect src = sourceRect(col, frame, state);
        float h = size * 1.42f;
        RectF dst = new RectF(x-size*.5f, y-h*.72f, x+size*.5f, y+h*.28f);
        paint.setAlpha(255);
        c.drawBitmap(sheet, src, dst, paint);
        return true;
    }

    private int directionColumn(int direction) {
        // 0 front, 1 right, 2 back, 3 left
        if (direction <= 0) return 0;
        if (direction == 1) return 3;
        if (direction == 2) return 6;
        return 8;
    }

    private int directionFrameCount(int direction) { return direction == 2 ? 2 : 3; }

    private Rect sourceRect(int groupStart, int frame, int row) {
        // Coordinates follow the original 1536x1024 sheet supplied by the user.
        int[][] xs = {
                {90,212,335,461},
                {487,604,724,844},
                {866,1004,1148},
                {1172,1291,1409,1525}
        };
        int gi = groupStart==0?0:groupStart==3?1:groupStart==6?2:3;
        int x0=xs[gi][frame], x1=xs[gi][frame+1];
        int[] y0={43,199,355,508,662,819};
        int[] y1={190,348,501,655,811,967};
        return new Rect(x0+2,y0[row]+2,x1-2,y1[row]-2);
    }

    public static int stateFor(boolean moving, boolean running, boolean attacking, boolean hurt, boolean dead) {
        if (dead) return DIE;
        if (hurt) return HURT;
        if (attacking) return ATTACK;
        if (running) return RUN;
        if (moving) return WALK;
        return IDLE;
    }
}
