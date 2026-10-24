package com.persiawar2d;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import java.util.ArrayList;

/** Large, touch-friendly hero selection screen used before every campaign stage. */
public final class CharacterSelectActivity extends Activity {
    public static final String EXTRA_STAGE = "campaign_stage";
    public static final String EXTRA_NEXT_STAGE_FLOW = "next_stage_flow";
    public static final String EXTRA_SELECTED_SKIN = "selected_skin";
    private static final int REQUEST_LOADOUT = 6121;

    private static final int GOLD = Color.rgb(239, 197, 92);
    private static final int INK = Color.rgb(7, 24, 33);

    private final String[] skinIds = {"classic", "blue", "red", "darius"};
    private final String[] names = {"KING ARDESHIR", "PARS GUARD", "ROYAL SCOUT", "ACHAEMENID"};
    private final String[] details = {
            "ROYAL LEADER • BALANCED",
            "CITY DEFENDER • STEADY",
            "FIELD SCOUT • AGILE",
            "ROYAL COMMANDER • VETERAN"
    };
    private final int[] portraits = {
            R.drawable.persia_player,
            R.drawable.player_guard,
            R.drawable.player_scout,
            R.drawable.achaemenid_player
    };

    private final ArrayList<View> cards = new ArrayList<>();
    private final ArrayList<TextView> cardNames = new ArrayList<>();
    private String selectedSkin = "classic";
    private int targetStage = 1;
    private boolean nextStageFlow;
    private TextView selectedSummary;
    private TextView continueButton;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().setFlags(1024, 1024);
        targetStage = Math.max(1, getIntent().getIntExtra(
                EXTRA_STAGE, getSharedPreferences("progress", 0).getInt("maxUnlockedStage", 1)));
        nextStageFlow = getIntent().getBooleanExtra(EXTRA_NEXT_STAGE_FLOW, false);
        selectedSkin = getSharedPreferences("player", 0).getString("skin", "classic");
        if (!isKnownSkin(selectedSkin)) selectedSkin = "classic";
        buildScreen();
    }

    private void buildScreen() {
        FrameLayout root = new FrameLayout(this);
        GradientDrawable background = new GradientDrawable(
                GradientDrawable.Orientation.TL_BR,
                new int[]{0xFF071923, 0xFF12313A, 0xFF17291F});
        root.setBackground(background);

        LinearLayout page = new LinearLayout(this);
        page.setOrientation(LinearLayout.VERTICAL);
        page.setPadding(dp(22), dp(12), dp(22), dp(12));
        root.addView(page, new FrameLayout.LayoutParams(-1, -1));

        LinearLayout header = new LinearLayout(this);
        header.setOrientation(LinearLayout.HORIZONTAL);
        header.setGravity(Gravity.CENTER_VERTICAL);
        page.addView(header, new LinearLayout.LayoutParams(-1, dp(48)));

        TextView back = button("‹  BACK", false);
        header.addView(back, new LinearLayout.LayoutParams(dp(112), dp(42)));
        back.setOnClickListener(v -> finish());

        TextView title = new TextView(this);
        title.setText("CHOOSE YOUR HERO");
        title.setTextColor(Color.WHITE);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        title.setTextSize(24);
        title.setGravity(Gravity.CENTER);
        header.addView(title, new LinearLayout.LayoutParams(0, -1, 1f));

        TextView stageBadge = new TextView(this);
        stageBadge.setText(String.format("STAGE %02d", targetStage));
        stageBadge.setTextColor(GOLD);
        stageBadge.setTypeface(Typeface.DEFAULT_BOLD);
        stageBadge.setTextSize(16);
        stageBadge.setGravity(Gravity.CENTER);
        header.addView(stageBadge, new LinearLayout.LayoutParams(dp(124), dp(42)));

        TextView subtitle = new TextView(this);
        subtitle.setText(nextStageFlow
                ? "Choose your hero before entering the next stage."
                : "Select a character for your campaign.");
        subtitle.setTextColor(0xFFD1DAD4);
        subtitle.setTextSize(13);
        subtitle.setGravity(Gravity.CENTER);
        page.addView(subtitle, new LinearLayout.LayoutParams(-1, dp(28)));

        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams rowParams = new LinearLayout.LayoutParams(-1, 0, 1f);
        rowParams.setMargins(0, dp(4), 0, dp(8));
        page.addView(row, rowParams);

        cards.clear();
        cardNames.clear();
        for (int i = 0; i < skinIds.length; i++) {
            final int index = i;
            LinearLayout card = new LinearLayout(this);
            card.setOrientation(LinearLayout.VERTICAL);
            card.setGravity(Gravity.CENTER);
            card.setPadding(dp(8), dp(8), dp(8), dp(6));

            LinearLayout.LayoutParams cardParams = new LinearLayout.LayoutParams(0, -1, 1f);
            cardParams.setMargins(dp(5), dp(4), dp(5), dp(4));
            row.addView(card, cardParams);

            ImageView portrait = new ImageView(this);
            portrait.setImageResource(portraits[i]);
            portrait.setScaleType(ImageView.ScaleType.FIT_CENTER);
            portrait.setAdjustViewBounds(true);
            card.addView(portrait, new LinearLayout.LayoutParams(-1, 0, 1f));

            TextView name = new TextView(this);
            name.setText(names[i]);
            name.setTextColor(Color.WHITE);
            name.setTypeface(Typeface.DEFAULT_BOLD);
            name.setTextSize(14);
            name.setGravity(Gravity.CENTER);
            card.addView(name, new LinearLayout.LayoutParams(-1, dp(27)));

            TextView detail = new TextView(this);
            detail.setText(details[i]);
            detail.setTextColor(0xFFCAD6CF);
            detail.setTextSize(9);
            detail.setGravity(Gravity.CENTER);
            card.addView(detail, new LinearLayout.LayoutParams(-1, dp(24)));

            cards.add(card);
            cardNames.add(name);
            card.setOnClickListener(v -> {
                selectedSkin = skinIds[index];
                updateSelection();
            });
        }

        LinearLayout footer = new LinearLayout(this);
        footer.setOrientation(LinearLayout.HORIZONTAL);
        footer.setGravity(Gravity.CENTER_VERTICAL);
        page.addView(footer, new LinearLayout.LayoutParams(-1, dp(54)));

        selectedSummary = new TextView(this);
        selectedSummary.setTextColor(0xFFE7D7AC);
        selectedSummary.setTypeface(Typeface.DEFAULT_BOLD);
        selectedSummary.setTextSize(13);
        footer.addView(selectedSummary, new LinearLayout.LayoutParams(0, -1, 1f));

        continueButton = button("ENTER STAGE", true);
        footer.addView(continueButton, new LinearLayout.LayoutParams(dp(254), dp(48)));
        continueButton.setOnClickListener(v -> continueToStage());

        setContentView(root);
        updateSelection();
    }

    private void updateSelection() {
        int selected = 0;
        for (int i = 0; i < skinIds.length; i++) {
            boolean active = skinIds[i].equals(selectedSkin);
            if (active) selected = i;
            GradientDrawable cardBg = new GradientDrawable();
            cardBg.setColor(active ? 0xEE214B42 : 0xCC10252D);
            cardBg.setCornerRadius(dp(16));
            cardBg.setStroke(dp(active ? 3 : 1), active ? GOLD : 0x667F998C);
            cards.get(i).setBackground(cardBg);
            cardNames.get(i).setTextColor(active ? GOLD : Color.WHITE);
        }
        selectedSummary.setText(names[selected] + "   •   STAGE " + String.format("%02d", targetStage));
        continueButton.setText(nextStageFlow ? "CONTINUE TO STAGE " + String.format("%02d", targetStage)
                : "START STAGE " + String.format("%02d", targetStage));
    }

    private void continueToStage() {
        getSharedPreferences("player", 0).edit().putString("skin", selectedSkin).apply();
        if (targetStage >= 2) {
            Intent loadout = new Intent(this, LoadoutSelectActivity.class);
            loadout.putExtra(LoadoutSelectActivity.EXTRA_STAGE, targetStage);
            loadout.putExtra(LoadoutSelectActivity.EXTRA_NEXT_STAGE_FLOW, nextStageFlow);
            startActivityForResult(loadout, REQUEST_LOADOUT);
            return;
        }
        getSharedPreferences("player", 0).edit()
                .putString(LoadoutSelectActivity.EXTRA_SELECTED_WEAPON, "royal_bow").apply();
        enterStage(null);
    }

    @Override protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST_LOADOUT || resultCode != RESULT_OK || data == null) return;
        String weaponId = data.getStringExtra(LoadoutSelectActivity.EXTRA_SELECTED_WEAPON);
        if (weaponId != null) {
            getSharedPreferences("player", 0).edit()
                    .putString(LoadoutSelectActivity.EXTRA_SELECTED_WEAPON, weaponId).apply();
        }
        enterStage(weaponId);
    }

    private void enterStage(String weaponId) {
        Intent result = new Intent();
        result.putExtra(EXTRA_STAGE, targetStage);
        result.putExtra(EXTRA_SELECTED_SKIN, selectedSkin);
        if (weaponId != null) result.putExtra(LoadoutSelectActivity.EXTRA_SELECTED_WEAPON, weaponId);
        if (nextStageFlow) {
            setResult(RESULT_OK, result);
            finish();
            return;
        }
        Intent game = new Intent(this, ThreeDGameActivity.class);
        game.putExtra(EXTRA_STAGE, targetStage);
        startActivity(game);
        finish();
    }

    private boolean isKnownSkin(String skin) {
        for (String id : skinIds) if (id.equals(skin)) return true;
        return false;
    }

    private TextView button(String label, boolean primary) {
        TextView view = new TextView(this);
        view.setText(label);
        view.setTextColor(Color.WHITE);
        view.setTextSize(primary ? 15 : 14);
        view.setTypeface(Typeface.DEFAULT_BOLD);
        view.setGravity(Gravity.CENTER);
        view.setPadding(dp(10), dp(6), dp(10), dp(6));
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
