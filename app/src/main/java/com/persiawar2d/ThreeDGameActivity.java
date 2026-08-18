package com.persiawar2d;

import android.app.Activity;
import android.graphics.Color;
import android.opengl.GLSurfaceView;
import android.os.Bundle;
import android.view.Gravity;
import android.widget.FrameLayout;
import android.widget.TextView;

/** Final playable 3D battle screen. */
public final class ThreeDGameActivity extends Activity {
    private AdvancedThreeDRenderer renderer;
    private TextView hud;
    private final Runnable hudLoop=new Runnable(){@Override public void run(){if(renderer!=null&&hud!=null){hud.setText("HP "+renderer.getHp()+"   •   AMMO "+renderer.getAmmo()+"   •   KILLS "+renderer.getKills()+"\nLEFT: MOVE     RIGHT: AIM / FIRE");hud.postDelayed(this,120);}}};
    @Override public void onCreate(Bundle state){
        super.onCreate(state);getWindow().setFlags(1024,1024);
        FrameLayout root=new FrameLayout(this);renderer=new AdvancedThreeDRenderer(this);
        GLSurfaceView surface=new GLSurfaceView(this);surface.setEGLContextClientVersion(2);surface.setRenderer(renderer);surface.setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);surface.setOnTouchListener((v,e)->renderer.onTouch(e));
        root.addView(surface,new FrameLayout.LayoutParams(-1,-1));
        hud=new TextView(this);hud.setTextColor(Color.WHITE);hud.setTextSize(15);hud.setGravity(Gravity.RIGHT);hud.setPadding(12,10,18,10);
        FrameLayout.LayoutParams hp=new FrameLayout.LayoutParams(-2,-2,Gravity.TOP|Gravity.RIGHT);root.addView(hud,hp);setContentView(root);hud.post(hudLoop);
    }
    @Override protected void onPause(){super.onPause();if(renderer!=null)renderer.pause();if(hud!=null)hud.removeCallbacks(hudLoop);}
    @Override protected void onResume(){super.onResume();if(renderer!=null)renderer.resume();if(hud!=null)hud.post(hudLoop);}
}
