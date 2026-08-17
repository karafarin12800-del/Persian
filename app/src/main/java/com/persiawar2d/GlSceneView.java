package com.persiawar2d;

import android.content.Context;
import android.opengl.GLSurfaceView;
import android.view.MotionEvent;

/** OpenGL ES 2.0 scene view for the new 2.5D presentation layer. */
public final class GlSceneView extends GLSurfaceView {
    private final GlSceneRenderer renderer;
    private float lastX;
    private float lastY;

    public GlSceneView(Context context) {
        super(context);
        setEGLContextClientVersion(2);
        renderer = new GlSceneRenderer(context.getApplicationContext());
        setRenderer(renderer);
        setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                lastX = e.getX();
                lastY = e.getY();
                return true;
            case MotionEvent.ACTION_MOVE:
                float dx = e.getX() - lastX;
                float dy = e.getY() - lastY;
                renderer.pan(dx, dy);
                lastX = e.getX();
                lastY = e.getY();
                return true;
            default:
                return true;
        }
    }
}
