package com.persiawar2d;

import android.app.Activity;
import android.os.Bundle;
import android.content.Intent;
import android.graphics.Color;
import android.view.Gravity;
import android.widget.*;

public class GameMenuActivity extends Activity {
    @Override public void onCreate(Bundle b){super.onCreate(b);LinearLayout r=new LinearLayout(this);r.setOrientation(LinearLayout.VERTICAL);r.setGravity(Gravity.CENTER);r.setPadding(30,30,30,30);r.setBackgroundColor(Color.rgb(7,27,43));TextView t=new TextView(this);t.setText("PAUSE MENU");t.setTextColor(Color.WHITE);t.setTextSize(32);t.setGravity(Gravity.CENTER);r.addView(t,new LinearLayout.LayoutParams(-1,80));TextView resume=btn("▶ RESUME");resume.setOnClickListener(v->finish());r.addView(resume);TextView restart=btn("↻ RESTART");restart.setOnClickListener(v->{startActivity(new Intent(this,ControlActivity.class));finish();});r.addView(restart);TextView home=btn("⌂ MAIN MENU");home.setOnClickListener(v->{startActivity(new Intent(this,StartActivity.class));finish();});r.addView(home);setContentView(r);}
    TextView btn(String s){TextView v=new TextView(this);v.setText(s);v.setTextColor(Color.WHITE);v.setTextSize(20);v.setGravity(Gravity.CENTER);v.setPadding(12,12,12,12);return v;}
}
