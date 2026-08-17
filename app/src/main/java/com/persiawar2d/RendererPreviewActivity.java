package com.persiawar2d;

import android.app.Activity;
import android.os.Bundle;
import android.graphics.Color;
import android.view.View;
import android.widget.TextView;

/**
 * Lightweight preview entry point for validating the new world renderer without
 * changing the existing gameplay Activity.
 */
public final class RendererPreviewActivity extends Activity {
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        TextView view = new TextView(this);
        view.setTextColor(Color.WHITE);
        view.setBackgroundColor(Color.rgb(34, 38, 32));
        view.setText("Persia War 2.5D\nRenderer preview\n" + RendererSelfTest.runAll());
        view.setTextSize(18f);
        view.setPadding(32, 32, 32, 32);
        setContentView(view);
    }
}
