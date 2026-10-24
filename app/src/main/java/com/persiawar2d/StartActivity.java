package com.persiawar2d;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

/** Campaign home screen with large landscape-friendly touch targets. */
public final class StartActivity extends Activity {
    private static final int GOLD = Color.rgb(241, 199, 98);
    private static final int CREAM = Color.rgb(226, 222, 201);
    private static final int INK = Color.rgb(7, 24, 33);

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().setFlags(1024, 1024);
        build();
    }

    @Override public void onBackPressed() {
        super.onBackPressed();
    }

    private void build() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(28), dp(15), dp(28), dp(14));
        root.setBackground(new GradientDrawable(
                GradientDrawable.Orientation.TL_BR,
                new int[]{0xFF071923, 0xFF102D35, 0xFF1C3025}));

        LinearLayout header = new LinearLayout(this);
        header.setOrientation(LinearLayout.HORIZONTAL);
        header.setGravity(Gravity.CENTER_VERTICAL);
        root.addView(header, new LinearLayout.LayoutParams(-1, 0, 1.05f));

        TextView emblem = new TextView(this);
        emblem.setText("𐎠");
        emblem.setTextColor(GOLD);
        emblem.setTypeface(Typeface.DEFAULT_BOLD);
        emblem.setTextSize(48);
        emblem.setGravity(Gravity.CENTER);
        GradientDrawable emblemBg = new GradientDrawable();
        emblemBg.setColor(0x662A493F);
        emblemBg.setCornerRadius(dp(18));
        emblemBg.setStroke(dp(2), 0xFFB68E42);
        emblem.setBackground(emblemBg);
        header.addView(emblem, new LinearLayout.LayoutParams(dp(88), -1));

        LinearLayout brand = new LinearLayout(this);
        brand.setOrientation(LinearLayout.VERTICAL);
        brand.setGravity(Gravity.CENTER_VERTICAL);
        brand.setPadding(dp(18), 0, 0, 0);
        header.addView(brand, new LinearLayout.LayoutParams(0, -1, 1f));

        TextView title = new TextView(this);
        title.setText("PERSIA WAR");
        title.setTextColor(GOLD);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        title.setTextSize(38);
        brand.addView(title, new LinearLayout.LayoutParams(-1, dp(54)));

        TextView subtitle = new TextView(this);
        subtitle.setText("CAMPAIGN  •  ANCIENT PERSIA  •  2.5D");
        subtitle.setTextColor(CREAM);
        subtitle.setTextSize(14);
        brand.addView(subtitle, new LinearLayout.LayoutParams(-1, dp(30)));

        TextView status = new TextView(this);
        int unlocked = getUnlockedStage();
        status.setText("PROGRESS SAVED\nSTAGE " + String.format("%02d", unlocked) + " UNLOCKED");
        status.setTextColor(GOLD);
        status.setTextSize(14);
        status.setTypeface(Typeface.DEFAULT_BOLD);
        status.setGravity(Gravity.CENTER);
        GradientDrawable statusBg = new GradientDrawable();
        statusBg.setColor(0x8820332E);
        statusBg.setCornerRadius(dp(14));
        statusBg.setStroke(dp(1), 0x997F875E);
        status.setBackground(statusBg);
        header.addView(status, new LinearLayout.LayoutParams(dp(190), dp(74)));

        LinearLayout main = new LinearLayout(this);
        main.setOrientation(LinearLayout.HORIZONTAL);
        main.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams mainParams = new LinearLayout.LayoutParams(-1, 0, 2.3f);
        mainParams.setMargins(0, dp(8), 0, dp(8));
        root.addView(main, mainParams);

        LinearLayout introCard = new LinearLayout(this);
        introCard.setOrientation(LinearLayout.VERTICAL);
        introCard.setGravity(Gravity.CENTER_VERTICAL);
        introCard.setPadding(dp(24), dp(20), dp(24), dp(20));
        GradientDrawable cardBg = new GradientDrawable();
        cardBg.setColor(0xC910242C);
        cardBg.setCornerRadius(dp(22));
        cardBg.setStroke(dp(1), 0x667C9386);
        introCard.setBackground(cardBg);
        LinearLayout.LayoutParams introParams = new LinearLayout.LayoutParams(0, -1, 1.05f);
        introParams.setMargins(0, 0, dp(12), 0);
        main.addView(introCard, introParams);

        TextView eyebrow = new TextView(this);
        eyebrow.setText("THE CITY IS WAITING");
        eyebrow.setTextColor(0xFFD7B65C);
        eyebrow.setTypeface(Typeface.DEFAULT_BOLD);
        eyebrow.setTextSize(13);
        introCard.addView(eyebrow, new LinearLayout.LayoutParams(-1, dp(28)));

        TextView tagline = new TextView(this);
        tagline.setText("Choose your hero.\nDefend the city.\nClear every stage.");
        tagline.setTextColor(Color.WHITE);
        tagline.setTypeface(Typeface.DEFAULT_BOLD);
        tagline.setTextSize(25);
        tagline.setLineSpacing(dp(4), 1.0f);
        introCard.addView(tagline, new LinearLayout.LayoutParams(-1, dp(112)));

        TextView detail = new TextView(this);
        detail.setText("Win to unlock the next stage.\nYour campaign progress is saved on this device.");
        detail.setTextColor(0xFFBCCBC5);
        detail.setTextSize(14);
        detail.setLineSpacing(dp(3), 1.0f);
        LinearLayout.LayoutParams detailParams = new LinearLayout.LayoutParams(-1, dp(56));
        detailParams.topMargin = dp(8);
        introCard.addView(detail, detailParams);

        LinearLayout actions = new LinearLayout(this);
        actions.setOrientation(LinearLayout.VERTICAL);
        actions.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams actionParams = new LinearLayout.LayoutParams(0, -1, 0.95f);
        actionParams.setMargins(dp(12), 0, 0, 0);
        main.addView(actions, actionParams);

        TextView begin = button("▶   CONTINUE CAMPAIGN", true);
        actions.addView(begin, new LinearLayout.LayoutParams(-1, 0, 1f));
        LinearLayout.LayoutParams beginParams = (LinearLayout.LayoutParams) begin.getLayoutParams();
        beginParams.bottomMargin = dp(10);
        begin.setLayoutParams(beginParams);
        begin.setOnClickListener(v -> openCharacterSelect());

        TextView characters = button("♜   CHOOSE CHARACTER", false);
        actions.addView(characters, new LinearLayout.LayoutParams(-1, 0, 1f));
        LinearLayout.LayoutParams characterParams = (LinearLayout.LayoutParams) characters.getLayoutParams();
        characterParams.topMargin = dp(10);
        characters.setLayoutParams(characterParams);
        characters.setOnClickListener(v -> openCharacterSelect());

        TextView footer = new TextView(this);
        footer.setText("PERSIA WAR   •   CAMPAIGN EDITION");
        footer.setTextColor(0xFF8D9D94);
        footer.setTextSize(11);
        footer.setGravity(Gravity.CENTER);
        root.addView(footer, new LinearLayout.LayoutParams(-1, dp(22)));

        setContentView(root);
    }

    private void openCharacterSelect() {
        Intent intent = new Intent(this, CharacterSelectActivity.class);
        intent.putExtra(CharacterSelectActivity.EXTRA_STAGE, getUnlockedStage());
        startActivity(intent);
    }

    private int getUnlockedStage() {
        return Math.max(1, getSharedPreferences("progress", 0).getInt("maxUnlockedStage", 1));
    }

    private TextView button(String label, boolean primary) {
        TextView view = new TextView(this);
        view.setText(label);
        view.setTextColor(Color.WHITE);
        view.setTextSize(primary ? 19 : 17);
        view.setTypeface(Typeface.DEFAULT_BOLD);
        view.setGravity(Gravity.CENTER);
        view.setPadding(dp(12), dp(10), dp(12), dp(10));
        GradientDrawable shape = new GradientDrawable();
        shape.setColor(primary ? 0xFF1B624A : 0xFF24343A);
        shape.setCornerRadius(dp(18));
        shape.setStroke(dp(primary ? 3 : 2), GOLD);
        view.setBackground(shape);
        view.setMinHeight(dp(76));
        view.setElevation(dp(3));
        return view;
    }

    private int dp(float value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
