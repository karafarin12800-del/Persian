package com.persiawar2d;

import android.app.Activity;
import android.graphics.Color;
import android.opengl.GLSurfaceView;
import android.os.Bundle;
import android.view.Gravity;
import android.widget.FrameLayout;
import android.widget.TextView;

/** Main playable Level 12 3D battle screen. */
public final class ThreeDGameActivity extends Activity {
    private Battle3DRenderer renderer;
    private TextView hud;
    @Override public void onCreate(Bundle state){
        super.onCreate(state); getWindow().setFlags(1024,1024);
        FrameLayout root=new FrameLayout(this);
        renderer=new Battle3DRenderer(this);
        GLSurfaceView surface=new GLSurfaceView(this);
        surface.setEGLContextClientVersion(2);
        surface.setRenderer(renderer);
        surface.setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);
        surface.setOnTouchListener((v,e)->renderer.onTouch(e));
        root.addView(surface,new FrameLayout.LayoutParams(-1,-1));
        hud=new TextView(this);
        hud.setTextColor(Color.WHITE); hud.setTextSize(16); hud.setGravity(Gravity.CENTER); hud.setPadding(18,12,18,12);
        FrameLayout.LayoutParams hp=new FrameLayout.LayoutParams(-2,-2,Gravity.TOP|Gravity.START); root.addView(hud,hp); setContentView(root);
        hud.post(new Runnable(){public void run(){
            hud.setText("HP "+renderer.getHp()+"   •   AMMO "+renderer.getAmmo()+"   •   KILLS "+renderer.getKills()+"\nLEFT: MOVE   •   RIGHT: AIM / FIRE");
            hud.postDelayed(this,200);
        }});
    }
    @Override protected void onPause(){super.onPause();if(renderer!=null)renderer.pause();}
    @Override protected void onResume(){super.onResume();if(renderer!=null)renderer.resume();}
}
