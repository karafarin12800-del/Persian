package com.persiawar2d;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.view.WindowManager;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;

/** Main menu, local-first player profile, character selection and store entry point. */
public final class StartActivity extends Activity {
    private final int gold = Color.rgb(239,197,92);
    private TextView playerLabel;

    @Override public void onCreate(Bundle b) {
        super.onCreate(b);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
        getWindow().getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY |
                View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN |
                View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
        PlayerProfile.ensureCreated(this);
        build();
        SharedPreferences p = getSharedPreferences("player_profile", MODE_PRIVATE);
        if (!p.getBoolean("name_setup_complete", false)) showNameDialog(true);
    }

    private int dp(float value) { return (int)(value * getResources().getDisplayMetrics().density + .5f); }

    private GradientDrawable shape(int color, int stroke) {
        GradientDrawable g = new GradientDrawable();
        g.setColor(color); g.setStroke(dp(2), stroke); g.setCornerRadius(dp(18)); return g;
    }

    private TextView button(String s) {
        TextView v = new TextView(this);
        v.setText(s); v.setTextColor(Color.WHITE); v.setTextSize(18); v.setGravity(Gravity.CENTER);
        v.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        v.setPadding(dp(12), dp(8), dp(12), dp(8));
        v.setBackground(shape(Color.rgb(31,100,75), gold));
        return v;
    }

    private void build() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL); root.setGravity(Gravity.CENTER);
        root.setPadding(dp(24), dp(12), dp(24), dp(12));
        root.setBackgroundColor(Color.rgb(7,27,43));

        TextView title = new TextView(this);
        title.setText("PARALLEL WORLDS"); title.setTextColor(gold); title.setTextSize(34);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD); title.setGravity(Gravity.CENTER);
        root.addView(title, new LinearLayout.LayoutParams(-1, dp(54)));

        TextView subtitle = new TextView(this);
        subtitle.setText("SURVIVE • EXPLORE • UNLOCK NEW WORLDS");
        subtitle.setTextColor(Color.rgb(230,205,155)); subtitle.setTextSize(13); subtitle.setGravity(Gravity.CENTER);
        root.addView(subtitle, new LinearLayout.LayoutParams(-1, dp(28)));

        playerLabel = new TextView(this);
        playerLabel.setTextColor(Color.WHITE); playerLabel.setTextSize(15); playerLabel.setGravity(Gravity.CENTER);
        root.addView(playerLabel, new LinearLayout.LayoutParams(-1, dp(34)));

        TextView start = button("▶  CONTINUE / START GAME");
        start.setOnClickListener(v -> startActivity(new Intent(this, MainActivity.class)));
        addMenuButton(root, start, 58);

        LinearLayout row = new LinearLayout(this);
        row.setGravity(Gravity.CENTER);
        TextView chars = button("⚔  CHARACTERS");
        chars.setOnClickListener(v -> chooseCharacter());
        TextView store = button("◈  STORE");
        store.setOnClickListener(v -> startActivity(new Intent(this, StoreActivity.class)));
        LinearLayout.LayoutParams half = new LinearLayout.LayoutParams(0, dp(54), 1);
        half.setMargins(dp(5), dp(6), dp(5), dp(6));
        row.addView(chars, half);
        row.addView(store, new LinearLayout.LayoutParams(0, dp(54), 1));
        LinearLayout.LayoutParams rowLp = new LinearLayout.LayoutParams(dp(540), -2);
        root.addView(row, rowLp);

        TextView profile = button("☻  PLAYER PROFILE / CHANGE NAME");
        profile.setOnClickListener(v -> showNameDialog(false));
        addMenuButton(root, profile, 50);

        TextView info = new TextView(this);
        info.setText("Offline-first profile  •  Touch controls unchanged  •  Store purchases are currently simulated");
        info.setTextColor(Color.rgb(185,198,197)); info.setTextSize(11); info.setGravity(Gravity.CENTER);
        root.addView(info, new LinearLayout.LayoutParams(-1, dp(30)));
        setContentView(root);
        refreshPlayerLabel();
    }

    private void addMenuButton(LinearLayout root, TextView button, int heightDp) {
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(dp(540), dp(heightDp));
        lp.setMargins(0, dp(5), 0, dp(5));
        root.addView(button, lp);
    }

    private void refreshPlayerLabel() {
        if (playerLabel != null)
            playerLabel.setText("PLAYER  •  " + PlayerProfile.name(this) + "     |     ◈ " + PlayerProfile.coins(this));
    }

    private void showNameDialog(boolean onboarding) {
        EditText input = new EditText(this);
        input.setSingleLine(true);
        input.setText(PlayerProfile.name(this));
        input.setHint("Enter your player name");
        input.setSelectAllOnFocus(true);
        new AlertDialog.Builder(this)
                .setTitle(onboarding ? "WELCOME, SURVIVOR" : "PLAYER PROFILE")
                .setMessage(onboarding ? "Choose a display name. You can play offline and link an online account later." :
                        "Change your display name. Your local progress and player ID will stay the same.")
                .setView(input)
                .setCancelable(!onboarding)
                .setNegativeButton(onboarding ? "USE PLAYER" : "CANCEL", (d,w) -> {
                    if (onboarding) finishNameSetup("Player");
                })
                .setPositiveButton("SAVE", (d,w) -> {
                    String name = input.getText().toString().trim();
                    if (name.isEmpty()) name = "Player";
                    finishNameSetup(name);
                }).show();
    }

    private void finishNameSetup(String name) {
        PlayerProfile.setName(this, name);
        getSharedPreferences("player_profile", MODE_PRIVATE).edit().putBoolean("name_setup_complete", true).apply();
        refreshPlayerLabel();
    }

    private void chooseCharacter() {
        final String[] ids = {"classic", "blue", "red", "darius"};
        final String[] names = {"Classic Warrior (FREE)", "Blue Guard", "Red Guard", "Darius"};
        java.util.ArrayList<String> availableNames = new java.util.ArrayList<>();
        java.util.ArrayList<String> availableIds = new java.util.ArrayList<>();
        for (int i=0; i<ids.length; i++) {
            if ("classic".equals(ids[i]) || PlayerProfile.isOwned(this, ids[i])) {
                availableIds.add(ids[i]);
                availableNames.add(names[i]);
            }
        }
        String current = getSharedPreferences("player", MODE_PRIVATE).getString("skin", "classic");
        int selected = Math.max(0, availableIds.indexOf(current));
        new AlertDialog.Builder(this).setTitle("CHARACTER COLLECTION")
                .setSingleChoiceItems(availableNames.toArray(new String[0]), selected, (d,w) -> {
                    String id = availableIds.get(w);
                    getSharedPreferences("player", MODE_PRIVATE).edit().putString("skin", id).apply();
                })
                .setPositiveButton("PLAY", (d,w) -> {})
                .setNeutralButton("VISIT STORE", (d,w) -> startActivity(new Intent(this, StoreActivity.class)))
                .setNegativeButton("CLOSE", null).show();
    }
}
