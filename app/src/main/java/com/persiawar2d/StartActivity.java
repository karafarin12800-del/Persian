package com.persiawar2d;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.Gravity;
import android.widget.*;

/** Main menu and character-selection entry point. */
public final class StartActivity extends Activity {
    private final int gold=Color.rgb(239,197,92);
    @Override public void onCreate(Bundle b){super.onCreate(b);build();}
    private TextView button(String s){TextView v=new TextView(this);v.setText(s);v.setTextColor(Color.WHITE);v.setTextSize(20);v.setGravity(Gravity.CENTER);v.setTypeface(null,1);GradientDrawable g=new GradientDrawable();g.setColor(Color.rgb(31,100,75));g.setStroke(3,gold);g.setCornerRadius(20);v.setBackground(g);return v;}
    private void build(){
        LinearLayout root=new LinearLayout(this);root.setOrientation(LinearLayout.VERTICAL);root.setGravity(Gravity.CENTER);root.setPadding(40,30,40,30);root.setBackgroundColor(Color.rgb(7,27,43));
        TextView title=new TextView(this);title.setText("PERSIA WAR");title.setTextColor(gold);title.setTextSize(42);title.setGravity(Gravity.CENTER);root.addView(title,new LinearLayout.LayoutParams(-1,90));
        TextView sub=new TextView(this);sub.setText("3D • ANCIENT PERSIA");sub.setTextColor(Color.rgb(230,205,155));sub.setTextSize(18);sub.setGravity(Gravity.CENTER);root.addView(sub,new LinearLayout.LayoutParams(-1,55));
        TextView start=button("▶  START BATTLE");start.setOnClickListener(v->startActivity(new Intent(this,MainActivity.class)));LinearLayout.LayoutParams bp=new LinearLayout.LayoutParams(520,74);bp.setMargins(0,14,0,14);root.addView(start,bp);
        TextView chars=button("⚔  CHARACTER SELECT");chars.setOnClickListener(v->chooseCharacter());root.addView(chars,new LinearLayout.LayoutParams(520,74));
        TextView info=new TextView(this);info.setText("AUTO-AIM • LINE OF SIGHT • SWORD • BOMB • SHIELD • 6000×6000 WORLD");info.setTextColor(Color.rgb(200,205,196));info.setTextSize(14);info.setGravity(Gravity.CENTER);root.addView(info,new LinearLayout.LayoutParams(-1,70));
        setContentView(root);
    }
    private void chooseCharacter(){
        final String[] names={"Classic Warrior","Blue Guard","Red Guard","Darius"};final String[] ids={"classic","blue","red","darius"};String cur=getSharedPreferences("player",0).getString("skin","classic");final int[] chosen={0};for(int i=0;i<ids.length;i++)if(ids[i].equals(cur))chosen[0]=i;
        new AlertDialog.Builder(this).setTitle("CHARACTER SELECT").setSingleChoiceItems(names,chosen[0],(d,w)->{chosen[0]=w;getSharedPreferences("player",0).edit().putString("skin",ids[w]).apply();}).setPositiveButton("PLAY",(d,w)->startActivity(new Intent(this,MainActivity.class))).setNegativeButton("CANCEL",null).show();
    }
}
