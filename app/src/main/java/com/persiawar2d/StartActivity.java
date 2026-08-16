package com.persiawar2d;

import android.app.*;
import android.os.Bundle;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.view.Gravity;
import android.widget.*;

public class StartActivity extends Activity {
    final int gold=Color.rgb(239,197,92);
    @Override public void onCreate(Bundle b){super.onCreate(b);build();}
    TextView button(String s){TextView v=new TextView(this);v.setText(s);v.setTextColor(Color.WHITE);v.setTextSize(20);v.setGravity(Gravity.CENTER);v.setTypeface(null,1);GradientDrawable g=new GradientDrawable();g.setColor(Color.rgb(31,100,75));g.setStroke(3,gold);g.setCornerRadius(20);v.setBackground(g);return v;}
    void build(){LinearLayout root=new LinearLayout(this);root.setOrientation(LinearLayout.VERTICAL);root.setGravity(Gravity.CENTER);root.setPadding(40,30,40,30);root.setBackgroundColor(Color.rgb(7,27,43));TextView title=new TextView(this);title.setText("PERSIA WAR");title.setTextColor(gold);title.setTextSize(42);title.setGravity(Gravity.CENTER);root.addView(title,new LinearLayout.LayoutParams(-1,90));TextView sub=new TextView(this);sub.setText("2D • ANCIENT PERSIA");sub.setTextColor(Color.rgb(230,205,155));sub.setTextSize(18);sub.setGravity(Gravity.CENTER);root.addView(sub,new LinearLayout.LayoutParams(-1,55));TextView start=button("▶  START BATTLE");start.setOnClickListener(v->startActivity(new Intent(this,ControlActivity.class)));LinearLayout.LayoutParams bp=new LinearLayout.LayoutParams(520,74);bp.setMargins(0,14,0,14);root.addView(start,bp);TextView chars=button("⚔  CHARACTER SELECT");chars.setOnClickListener(v->chooseCharacter());root.addView(chars,new LinearLayout.LayoutParams(520,74));TextView version=new TextView(this);version.setText("PERSIA WAR 2D • TEST BUILD");version.setTextColor(Color.GRAY);version.setGravity(Gravity.CENTER);root.addView(version,new LinearLayout.LayoutParams(-1,60));setContentView(root);}
    void chooseCharacter(){final String[] names={"Classic Warrior","Blue Guard","Red Guard","ARYAN"};final String[] ids={"classic","blue","red","darius"};String cur=getSharedPreferences("player",0).getString("skin","classic");LinearLayout list=new LinearLayout(this);list.setOrientation(LinearLayout.HORIZONTAL);RadioButton[] radios=new RadioButton[names.length];for(int i=0;i<names.length;i++){final int idx=i;RadioButton r=new RadioButton(this);r.setText(names[i]);r.setTextColor(Color.WHITE);r.setTextSize(17);r.setChecked(ids[i].equals(cur));r.setOnClickListener(v->getSharedPreferences("player",0).edit().putString("skin",ids[idx]).apply());radios[i]=r;list.addView(r,new LinearLayout.LayoutParams(180,70));}new AlertDialog.Builder(this).setTitle("CHARACTER SELECT").setView(list).setPositiveButton("PLAY",(d,w)->startActivity(new Intent(this,ControlActivity.class))).setNegativeButton("CANCEL",null).show();}
}
