package com.persiawar2d;

import android.app.Activity;
import android.app.AlertDialog;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.Gravity;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

/** Offline mock shop. It deliberately does not process real payments. */
public final class StoreActivity extends Activity {
    private final int gold = Color.rgb(239,197,92);
    private LinearLayout list;
    private TextView wallet;

    private static final String[][] ITEMS = {
            {"blue", "BLUE GUARD", "250", "A cool-toned guard skin."},
            {"red", "RED GUARD", "350", "A bold red cosmetic skin."},
            {"darius", "DARIUS", "500", "A premium-looking warrior skin."}
    };

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().setFlags(android.view.WindowManager.LayoutParams.FLAG_FULLSCREEN,
                android.view.WindowManager.LayoutParams.FLAG_FULLSCREEN);
        getWindow().getDecorView().setSystemUiVisibility(
                android.view.View.SYSTEM_UI_FLAG_FULLSCREEN | android.view.View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY |
                android.view.View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | android.view.View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN |
                android.view.View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION | android.view.View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
        PlayerProfile.ensureCreated(this);
        build();
    }

    private GradientDrawable panel(int color, int stroke) {
        GradientDrawable d = new GradientDrawable();
        d.setColor(color); d.setCornerRadius(22); d.setStroke(2, stroke); return d;
    }

    private TextView text(String value, int size, int color, boolean bold) {
        TextView t = new TextView(this);
        t.setText(value); t.setTextSize(size); t.setTextColor(color);
        t.setGravity(Gravity.CENTER_VERTICAL);
        if (bold) t.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        return t;
    }

    private TextView action(String label) {
        TextView t = text(label, 16, Color.WHITE, true);
        t.setGravity(Gravity.CENTER);
        t.setPadding(18, 10, 18, 10);
        t.setBackground(panel(Color.rgb(31,100,75), gold));
        return t;
    }

    private void build() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(28, 18, 28, 18);
        root.setBackgroundColor(Color.rgb(7,27,43));

        LinearLayout header = new LinearLayout(this);
        header.setGravity(Gravity.CENTER_VERTICAL);
        header.setOrientation(LinearLayout.HORIZONTAL);
        TextView title = text("FIELD STORE", 30, gold, true);
        header.addView(title, new LinearLayout.LayoutParams(0, 58, 1));
        wallet = text("", 17, Color.WHITE, true);
        wallet.setGravity(Gravity.CENTER);
        wallet.setPadding(14, 8, 14, 8);
        wallet.setBackground(panel(Color.rgb(19,48,61), gold));
        header.addView(wallet, new LinearLayout.LayoutParams(-2, 48));
        root.addView(header);

        TextView notice = text("TEST STORE • NO REAL MONEY • COSMETICS ONLY", 13,
                Color.rgb(230,205,155), true);
        notice.setGravity(Gravity.CENTER);
        root.addView(notice, new LinearLayout.LayoutParams(-1, 36));

        ScrollView scroll = new ScrollView(this);
        list = new LinearLayout(this);
        list.setOrientation(LinearLayout.VERTICAL);
        list.setPadding(0, 8, 0, 8);
        scroll.addView(list);
        root.addView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));

        TextView back = action("←  BACK TO MENU");
        back.setOnClickListener(v -> finish());
        LinearLayout.LayoutParams backLp = new LinearLayout.LayoutParams(-1, 52);
        backLp.topMargin = 10;
        root.addView(back, backLp);
        setContentView(root);
        refresh();
    }

    private void refresh() {
        wallet.setText("◈  " + PlayerProfile.coins(this));
        list.removeAllViews();
        for (String[] item : ITEMS) {
            final String id = item[0];
            int price = Integer.parseInt(item[2]);
            boolean owned = PlayerProfile.isOwned(this, id);
            LinearLayout card = new LinearLayout(this);
            card.setOrientation(LinearLayout.HORIZONTAL);
            card.setGravity(Gravity.CENTER_VERTICAL);
            card.setPadding(18, 12, 14, 12);
            card.setBackground(panel(Color.rgb(15,43,57), Color.rgb(55,91,100)));
            LinearLayout.LayoutParams cp = new LinearLayout.LayoutParams(-1, 94);
            cp.bottomMargin = 10;
            list.addView(card, cp);

            LinearLayout info = new LinearLayout(this);
            info.setOrientation(LinearLayout.VERTICAL);
            TextView name = text(item[1], 18, Color.WHITE, true);
            TextView desc = text(item[3], 12, Color.rgb(190,205,205), false);
            info.addView(name);
            info.addView(desc);
            card.addView(info, new LinearLayout.LayoutParams(0, -2, 1));

            TextView buy = action(owned ? "OWNED" : "◈ " + price);
            if (owned) buy.setAlpha(.65f);
            card.addView(buy, new LinearLayout.LayoutParams(150, 50));
            buy.setOnClickListener(v -> {
                if (PlayerProfile.isOwned(this, id)) {
                    new AlertDialog.Builder(this).setTitle(item[1])
                            .setMessage("This cosmetic is already in your collection.")
                            .setPositiveButton("OK", null).show();
                    return;
                }
                if (PlayerProfile.coins(this) < price) {
                    new AlertDialog.Builder(this).setTitle("Not enough coins")
                            .setMessage("This is a test shop. No real payment will be taken.")
                            .setPositiveButton("OK", null).show();
                    return;
                }
                new AlertDialog.Builder(this).setTitle("Unlock " + item[1] + "?")
                        .setMessage("Spend " + price + " test coins? This does not charge real money.")
                        .setNegativeButton("CANCEL", null)
                        .setPositiveButton("UNLOCK", (d, w) -> {
                            if (PlayerProfile.spendCoins(this, price)) {
                                PlayerProfile.setOwned(this, id, true);
                                refresh();
                            }
                        }).show();
            });
        }
        TextView earn = action("+ 100 TEST COINS (DEVELOPMENT)");
        earn.setOnClickListener(v -> {
            PlayerProfile.addCoins(this, 100);
            refresh();
        });
        LinearLayout.LayoutParams ep = new LinearLayout.LayoutParams(-1, 48);
        ep.topMargin = 4;
        list.addView(earn, ep);
    }
}
