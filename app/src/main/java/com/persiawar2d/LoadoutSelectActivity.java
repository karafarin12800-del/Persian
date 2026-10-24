package com.persiawar2d;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.Gravity;
import android.widget.LinearLayout;
import android.widget.TextView;

import java.util.ArrayList;

/** Stage-gated fictional loadout screen. Each cleared stage unlocks one more choice. */
public final class LoadoutSelectActivity extends Activity {
    public static final String EXTRA_STAGE = "campaign_stage";
    public static final String EXTRA_NEXT_STAGE_FLOW = "next_stage_flow";
    public static final String EXTRA_SELECTED_WEAPON = "selected_weapon";

    private static final int GOLD = Color.rgb(239, 197, 92);

    private final String[] ids = {
            "royal_bow", "scout_bow", "war_spear",
            "sun_bow", "arcane_orb", "royal_crossbow"
    };
    private final String[] names = {
            "ROYAL BOW", "SCOUT BOW", "WAR SPEAR",
            "SUN BOW", "ARCANE ORB", "ROYAL CROSSBOW"
    };
    private final String[] icons = {"RB", "SB", "WS", "SUN", "ARC", "RX"};
    private final String[] descriptions = {
            "Balanced • reliable", "Rapid • light impact", "Heavy • focused",
            "Piercing • precise", "Energy • area effect", "Precision • powerful"
    };
    private final int[] damage = {3, 2, 5, 4, 3, 5};
    private final int[] speed = {3, 5, 2, 3, 4, 2};
    private final int[] reach = {4, 3, 3, 5, 4, 5};

    private final ArrayList<LinearLayout> cards = new ArrayList<>();
    private final ArrayList<TextView> cardTitles = new ArrayList<>();
    private final ArrayList<TextView> cardStatus = new ArrayList<>();
    private int stage = 2;
    private int unlockedCount = 2;
    private boolean nextStageFlow;
    private String selectedId = "royal_bow";
    private TextView footerSelection;
    private TextView enterButton;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().setFlags(1024, 1024);
        stage = Math.max(2, getIntent().getIntExtra(EXTRA_STAGE, 2));
        nextStageFlow = getIntent().getBooleanExtra(EXTRA_NEXT_STAGE_FLOW, false);
        int maxStage = Math.max(1, getSharedPreferences("progress", 0).getInt("maxUnlockedStage", 1));
        unlockedCount = Math.min(ids.length, maxStage);
        selectedId = getSharedPreferences("player", 0).getString(EXTRA_SELECTED_WEAPON, ids[0]);
        if (indexOf(selectedId) < 0 || indexOf(selectedId) >= unlockedCount) selectedId = ids[0];
        buildScreen();
    }

    private void buildScreen() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(18), dp(8), dp(18), dp(8));
        root.setBackground(new GradientDrawable(
                GradientDrawable.Orientation.TL_BR,
                new int[]{0xFF071923, 0xFF12313A, 0xFF17291F}));

        LinearLayout header = new LinearLayout(this);
        header.setOrientation(LinearLayout.HORIZONTAL);
        header.setGravity(Gravity.CENTER_VERTICAL);
        root.addView(header, new LinearLayout.LayoutParams(-1, dp(48)));

        TextView back = button("‹  BACK", false);
        header.addView(back, new LinearLayout.LayoutParams(dp(112), dp(40)));
        back.setOnClickListener(v -> finish());

        TextView title = new TextView(this);
        title.setText("CHOOSE YOUR LOADOUT");
        title.setTextColor(Color.WHITE);
        title.setTextSize(22);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        title.setGravity(Gravity.CENTER);
        header.addView(title, new LinearLayout.LayoutParams(0, -1, 1f));

        TextView stageBadge = new TextView(this);
        stageBadge.setText("STAGE " + String.format("%02d", stage));
        stageBadge.setTextColor(GOLD);
        stageBadge.setTextSize(15);
        stageBadge.setTypeface(Typeface.DEFAULT_BOLD);
        stageBadge.setGravity(Gravity.CENTER);
        header.addView(stageBadge, new LinearLayout.LayoutParams(dp(120), dp(40)));

        TextView subtitle = new TextView(this);
        subtitle.setText("New choices unlock as you clear campaign stages.");
        subtitle.setTextColor(0xFFD1DAD4);
        subtitle.setTextSize(12);
        subtitle.setGravity(Gravity.CENTER);
        root.addView(subtitle, new LinearLayout.LayoutParams(-1, dp(22)));

        LinearLayout firstRow = new LinearLayout(this);
        firstRow.setOrientation(LinearLayout.HORIZONTAL);
        firstRow.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams rowParams = new LinearLayout.LayoutParams(-1, 0, 1f);
        rowParams.setMargins(0, dp(3), 0, dp(4));
        root.addView(firstRow, rowParams);
        addCardRow(firstRow, 0, 3);

        LinearLayout secondRow = new LinearLayout(this);
        secondRow.setOrientation(LinearLayout.HORIZONTAL);
        secondRow.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams secondParams = new LinearLayout.LayoutParams(-1, 0, 1f);
        secondParams.setMargins(0, dp(4), 0, dp(4));
        root.addView(secondRow, secondParams);
        addCardRow(secondRow, 3, 3);

        LinearLayout footer = new LinearLayout(this);
        footer.setOrientation(LinearLayout.HORIZONTAL);
        footer.setGravity(Gravity.CENTER_VERTICAL);
        root.addView(footer, new LinearLayout.LayoutParams(-1, dp(48)));

        footerSelection = new TextView(this);
        footerSelection.setTextColor(0xFFE7D7AC);
        footerSelection.setTypeface(Typeface.DEFAULT_BOLD);
        footerSelection.setTextSize(12);
        footer.addView(footerSelection, new LinearLayout.LayoutParams(0, -1, 1f));

        enterButton = button("ENTER STAGE", true);
        footer.addView(enterButton, new LinearLayout.LayoutParams(dp(246), dp(44)));
        enterButton.setOnClickListener(v -> enterStage());

        setContentView(root);
        refreshCards();
    }

    private void addCardRow(LinearLayout row, int start, int count) {
        for (int offset = 0; offset < count; offset++) {
            final int index = start + offset;
            LinearLayout card = new LinearLayout(this);
            card.setOrientation(LinearLayout.VERTICAL);
            card.setGravity(Gravity.CENTER);
            card.setPadding(dp(7), dp(7), dp(7), dp(6));
            LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(0, -1, 1f);
            params.setMargins(dp(4), dp(2), dp(4), dp(2));
            row.addView(card, params);

            TextView icon = new TextView(this);
            icon.setText(icons[index]);
            icon.setGravity(Gravity.CENTER);
            icon.setTextSize(24);
            icon.setTypeface(Typeface.DEFAULT_BOLD);
            icon.setTextColor(GOLD);
            GradientDrawable iconBg = new GradientDrawable();
            iconBg.setColor(0x443F6255);
            iconBg.setCornerRadius(dp(12));
            iconBg.setStroke(dp(1), 0x667B987D);
            icon.setBackground(iconBg);
            card.addView(icon, new LinearLayout.LayoutParams(-1, 0, 1f));

            TextView name = new TextView(this);
            name.setText(names[index]);
            name.setTextColor(Color.WHITE);
            name.setTypeface(Typeface.DEFAULT_BOLD);
            name.setTextSize(13);
            name.setGravity(Gravity.CENTER);
            card.addView(name, new LinearLayout.LayoutParams(-1, dp(22)));

            TextView desc = new TextView(this);
            desc.setText(descriptions[index]);
            desc.setTextColor(0xFFCBD7D0);
            desc.setTextSize(10);
            desc.setGravity(Gravity.CENTER);
            card.addView(desc, new LinearLayout.LayoutParams(-1, dp(20)));

            TextView status = new TextView(this);
            status.setGravity(Gravity.CENTER);
            status.setTextSize(10);
            status.setTypeface(Typeface.DEFAULT_BOLD);
            card.addView(status, new LinearLayout.LayoutParams(-1, dp(20)));

            cards.add(card);
            cardTitles.add(name);
            cardStatus.add(status);
            card.setOnClickListener(v -> {
                if (index >= unlockedCount) return;
                selectedId = ids[index];
                refreshCards();
            });
        }
    }

    private void refreshCards() {
        for (int i = 0; i < ids.length; i++) {
            boolean unlocked = i < unlockedCount;
            boolean selected = ids[i].equals(selectedId);
            GradientDrawable background = new GradientDrawable();
            background.setColor(!unlocked ? 0xBB111C22 : (selected ? 0xEE214B42 : 0xCC10252D));
            background.setCornerRadius(dp(15));
            background.setStroke(dp(selected ? 3 : 1), selected ? GOLD : 0x667F998C);
            cards.get(i).setBackground(background);
            cardTitles.get(i).setTextColor(unlocked ? (selected ? GOLD : Color.WHITE) : 0xFF87928D);
            cardStatus.get(i).setText(unlocked ? (selected ? "SELECTED" : "UNLOCKED")
                    : "CLEAR STAGE " + i);
            cardStatus.get(i).setTextColor(!unlocked ? 0xFF9CA6A0 : (selected ? GOLD : 0xFFB8C9BD));
            cards.get(i).setAlpha(unlocked ? 1f : 0.72f);
        }
        int index = Math.max(0, indexOf(selectedId));
        footerSelection.setText(names[index] + "  •  DMG " + damage[index]
                + "  •  SPEED " + speed[index] + "  •  REACH " + reach[index]);
        enterButton.setText((nextStageFlow ? "CONTINUE TO STAGE " : "ENTER STAGE ")
                + String.format("%02d", stage));
    }

    private void enterStage() {
        getSharedPreferences("player", 0).edit().putString(EXTRA_SELECTED_WEAPON, selectedId).apply();
        Intent result = new Intent();
        result.putExtra(EXTRA_STAGE, stage);
        result.putExtra(EXTRA_SELECTED_WEAPON, selectedId);
        setResult(RESULT_OK, result);
        finish();
    }

    private int indexOf(String id) {
        for (int i = 0; i < ids.length; i++) if (ids[i].equals(id)) return i;
        return -1;
    }

    private TextView button(String label, boolean primary) {
        TextView view = new TextView(this);
        view.setText(label);
        view.setTextColor(Color.WHITE);
        view.setTextSize(primary ? 15 : 13);
        view.setTypeface(Typeface.DEFAULT_BOLD);
        view.setGravity(Gravity.CENTER);
        view.setPadding(dp(8), dp(6), dp(8), dp(6));
        GradientDrawable shape = new GradientDrawable();
        shape.setColor(primary ? 0xFF1B624A : 0xFF24333A);
        shape.setCornerRadius(dp(13));
        shape.setStroke(dp(2), GOLD);
        view.setBackground(shape);
        return view;
    }

    private int dp(float value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
