package com.persiawar2d;

import android.app.*;
import android.os.*;
import android.content.*;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.view.*;
import android.widget.*;

/** Battle entry point with a lightweight in-game menu overlay. */
public class ControlActivity extends MainActivity {
    FrameLayout root;
    GameView game;
    TextView menuButton;
    LinearLayout menuPanel;
    final int gold=Color.rgb(239,197,92), cream=Color.rgb(248,238,211), dark=Color.rgb(15,27,24);

    @Override public void onCreate(Bundle b){
        super.onCreate(b);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN,WindowManager.LayoutParams.FLAG_FULLSCREEN);
        root=new FrameLayout(this);
        game=new GameView(this);
        root.addView(game,new FrameLayout.LayoutParams(-1,-1));
        addMenuButton();
        setContentView(root);
    }

    GradientDrawable bg(int color){GradientDrawable g=new GradientDrawable();g.setColor(color);g.setStroke(3,gold);g.setCornerRadius(22);return g;}
    TextView actionButton(String text){TextView v=new TextView(this);v.setText(text);v.setTextColor(cream);v.setTextSize(21);v.setGravity(Gravity.CENTER);v.setTypeface(null,1);v.setIncludeFontPadding(false);v.setBackground(bg(0xff34463d));v.setClickable(true);LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,62);p.setMargins(0,7,0,7);v.setLayoutParams(p);return v;}
    void addMenuButton(){menuButton=new TextView(this);menuButton.setText("☰");menuButton.setTextColor(cream);menuButton.setTextSize(27);menuButton.setGravity(Gravity.CENTER);menuButton.setTypeface(null,1);menuButton.setBackground(bg(0xdd293b35));menuButton.setClickable(true);FrameLayout.LayoutParams p=new FrameLayout.LayoutParams(62,62,Gravity.RIGHT|Gravity.TOP);p.setMargins(0,8,12,0);root.addView(menuButton,p);menuButton.setOnClickListener(v->showMenu());}
    void showMenu(){if(menuPanel!=null)return;View shade=new View(this);shade.setBackgroundColor(0x99000000);shade.setTag("shade");root.addView(shade,new FrameLayout.LayoutParams(-1,-1));menuPanel=new LinearLayout(this);menuPanel.setOrientation(LinearLayout.VERTICAL);menuPanel.setGravity(Gravity.CENTER_HORIZONTAL);menuPanel.setPadding(30,24,30,24);menuPanel.setBackground(bg(0xee17251f));TextView title=new TextView(this);title.setText("PAUSE MENU");title.setTextColor(cream);title.setTextSize(29);title.setGravity(Gravity.CENTER);title.setTypeface(null,1);title.setPadding(0,0,0,14);menuPanel.addView(title,new LinearLayout.LayoutParams(-1,58));TextView resume=actionButton("▶  RESUME");resume.setOnClickListener(v->closeMenu());menuPanel.addView(resume);TextView restart=actionButton("↻  RESTART BATTLE");restart.setOnClickListener(v->{game.resetGame();closeMenu();});menuPanel.addView(restart);TextView settings=actionButton("⚙  SETTINGS");settings.setOnClickListener(v->settingsDialog());menuPanel.addView(settings);TextView home=actionButton("⌂  MAIN MENU");home.setOnClickListener(v->{Intent i=new Intent(this,StartActivity.class);i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP|Intent.FLAG_ACTIVITY_SINGLE_TOP);startActivity(i);finish();});menuPanel.addView(home);FrameLayout.LayoutParams pp=new FrameLayout.LayoutParams(390,430,Gravity.CENTER);root.addView(menuPanel,pp);}
    void closeMenu(){if(menuPanel!=null){for(int i=root.getChildCount()-1;i>=0;i--){View v=root.getChildAt(i);if(v==menuPanel||"shade".equals(v.getTag()))root.removeViewAt(i);}menuPanel=null;}}
    void settingsDialog(){final String[] opts={"Vibration on fire","Large HUD","Low-opacity controls"};boolean[] c={getSharedPreferences("settings",0).getBoolean("vibrate",true),getSharedPreferences("settings",0).getBoolean("bigHud",true),getSharedPreferences("settings",0).getBoolean("lowOpacity",true)};new AlertDialog.Builder(this).setTitle("SETTINGS").setMultiChoiceItems(opts,c,(d,w,on)->getSharedPreferences("settings",0).edit().putBoolean(w==0?"vibrate":w==1?"bigHud":"lowOpacity",on).apply()).setPositiveButton("SAVE",null).show();}
    @Override public void onBackPressed(){if(menuPanel!=null){closeMenu();return;}showMenu();}
}
