package com.persiawar2d;

import android.app.Activity;
import android.graphics.Color;
import android.opengl.GLSurfaceView;
import android.os.Bundle;
import android.widget.FrameLayout;
import android.widget.TextView;

/** First native 3D gameplay runtime. Existing 2D systems remain intact while the 3D path is built out. */
public final class ThreeDGameActivity extends Activity {
    private ThreeDRenderer renderer;
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().setFlags(1024, 1024);
        FrameLayout root = new FrameLayout(this);
        renderer = new ThreeDRenderer(this);
        GLSurfaceView surface = new GLSurfaceView(this);
        surface.setEGLContextClientVersion(2);
        surface.setRenderer(renderer);
        surface.setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);
        surface.setOnTouchListener((v, e) -> renderer.onTouch(e));
        root.addView(surface, new FrameLayout.LayoutParams(-1, -1));
        TextView hint = new TextView(this);
        hint.setText("3D TEST  •  LEFT: MOVE   RIGHT: AIM");
        hint.setTextColor(Color.WHITE);
        hint.setTextSize(14);
        hint.setPadding(24, 18, 24, 18);
        root.addView(hint, new FrameLayout.LayoutParams(-2, -2));
        setContentView(root);
    }
    @Override protected void onPause() { super.onPause(); if(renderer!=null) renderer.pause(); }
    @Override protected void onResume() { super.onResume(); if(renderer!=null) renderer.resume(); }
}
