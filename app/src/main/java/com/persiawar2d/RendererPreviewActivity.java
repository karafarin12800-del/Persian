package com.persiawar2d;

import android.app.Activity;
import android.os.Bundle;

/** Visual validation entry point for the OpenGL ES 2.0 2.5D scene. */
public final class RendererPreviewActivity extends Activity {
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(new GlSceneView(this));
    }
}
