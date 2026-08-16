package com.persiawar2d;

import android.app.Activity;
import android.os.Bundle;
import android.content.Intent;

/** Lightweight shell kept as a stable navigation target for future expansion. */
public class GameShellActivity extends Activity {
    @Override public void onCreate(Bundle b){super.onCreate(b);startActivity(new Intent(this,ControlActivity.class));finish();}
}
