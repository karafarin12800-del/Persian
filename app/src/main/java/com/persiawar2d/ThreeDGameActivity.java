package com.persiawar2d;

import android.app.Activity;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.opengl.GLSurfaceView;
import android.os.Bundle;
import android.view.MotionEvent;
import android.view.Gravity;
import android.view.View;
import android.widget.FrameLayout;

/** Main playable 2.5D battle screen. */
public final class ThreeDGameActivity extends Activity {
    private AdvancedThreeDRenderer renderer;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().setFlags(1024,1024);
        FrameLayout root=new FrameLayout(this);
        renderer=new AdvancedThreeDRenderer(this);
        GLSurfaceView surface=new GLSurfaceView(this);
        surface.setEGLContextClientVersion(2);
        surface.setRenderer(renderer);
        surface.setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);
        surface.setOnTouchListener((v,e)->renderer.onTouch(e));
        root.addView(surface,new FrameLayout.LayoutParams(-1,-1));
        root.addView(new HudView(),new FrameLayout.LayoutParams(-1,-1,Gravity.TOP|Gravity.START));
        setContentView(root);
    }

    @Override protected void onPause(){super.onPause();if(renderer!=null)renderer.pause();}
    @Override protected void onResume(){super.onResume();if(renderer!=null)renderer.resume();}

    private final class HudView extends View {
        private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Runnable refresh=new Runnable(){@Override public void run(){invalidate();postDelayed(this,120);}};
        HudView(){super(ThreeDGameActivity.this);setWillNotDraw(false);setClickable(false);post(refresh);}

        @Override protected void onDraw(Canvas c){
            if(renderer==null)return;
            float w=getWidth(),h=getHeight(),pad=Math.max(18f,w*.018f);
            p.setStyle(Paint.Style.FILL);p.setColor(0xC918211D);c.drawRoundRect(pad,pad,w-pad,88,18,18,p);
            p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextSize(19);p.setColor(Color.rgb(244,211,126));p.setTextAlign(Paint.Align.LEFT);c.drawText("PERSIA WAR  •  2.5D",pad+18,43,p);
            p.setTypeface(Typeface.DEFAULT);p.setTextSize(15);p.setColor(Color.WHITE);c.drawText("KILLS  "+renderer.getKills(),pad+18,67,p);
            float barX=w*.39f,barW=w*.27f;p.setColor(0xFF29332E);c.drawRoundRect(barX,30,barX+barW,54,12,12,p);p.setColor(renderer.getHp()>35?Color.rgb(73,178,92):Color.rgb(205,73,61));c.drawRoundRect(barX+3,33,barX+Math.max(6,barW*renderer.getHp()/100f-3),51,9,9,p);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(14);p.setColor(Color.WHITE);c.drawText("HP "+renderer.getHp(),barX+barW/2f,47,p);
            p.setTextAlign(Paint.Align.RIGHT);p.setTextSize(16);c.drawText("AMMO "+renderer.getAmmo()+"/"+renderer.getReserve()+"   G "+renderer.getGrenades(),w-pad-18,47,p);
            float joyX=w*.15f,joyY=h*.80f,fireX=w*.84f,fireY=h*.78f,r=Math.min(76f,h*.105f);
            p.setColor(0x4A405149);c.drawCircle(joyX,joyY,r+16,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xC8D7C58E);c.drawCircle(joyX,joyY,r+16,p);p.setStyle(Paint.Style.FILL);p.setColor(0xBFC1A85F);c.drawCircle(joyX,joyY,r*.42f,p);
            p.setColor(0xA54E433C);c.drawCircle(fireX,fireY,r*1.18f,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xD7E6D39B);c.drawCircle(fireX,fireY,r*1.18f,p);p.setStyle(Paint.Style.FILL);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextSize(24);p.setColor(Color.WHITE);p.setTextAlign(Paint.Align.CENTER);c.drawText("FIRE",fireX,fireY+8,p);
            p.setTypeface(Typeface.DEFAULT);p.setTextSize(14);p.setColor(0xDDEEE8D8);c.drawText("MOVE",joyX,joyY+r+34,p);c.drawText("AIM / FIRE",fireX,fireY+r*1.18f+30,p);
        }
        @Override public boolean onTouchEvent(MotionEvent event){return false;}
    }
}
