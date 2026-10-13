package com.persiawar2d;

import android.content.Context;
import android.content.SharedPreferences;
import java.util.UUID;

/** Local-first player profile. Online linking can be added without changing the local profile ID. */
public final class PlayerProfile {
    private static final String PREFS = "player_profile";
    private static final String KEY_ID = "player_id";
    private static final String KEY_NAME = "display_name";
    private static final String KEY_COINS = "soft_currency";
    private PlayerProfile() {}

    private static SharedPreferences prefs(Context c) {
        return c.getApplicationContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    public static void ensureCreated(Context c) {
        SharedPreferences p = prefs(c);
        if (!p.contains(KEY_ID)) {
            p.edit().putString(KEY_ID, UUID.randomUUID().toString())
                    .putString(KEY_NAME, "Player")
                    .putInt(KEY_COINS, 500)
                    .apply();
        }
    }

    public static String id(Context c) { ensureCreated(c); return prefs(c).getString(KEY_ID, ""); }
    public static String name(Context c) { ensureCreated(c); return prefs(c).getString(KEY_NAME, "Player"); }
    public static void setName(Context c, String name) {
        String safe = name == null ? "" : name.trim();
        if (!safe.isEmpty()) prefs(c).edit().putString(KEY_NAME, safe).apply();
    }
    public static int coins(Context c) { ensureCreated(c); return Math.max(0, prefs(c).getInt(KEY_COINS, 500)); }
    public static boolean spendCoins(Context c, int amount) {
        if (amount < 0) return false;
        ensureCreated(c);
        SharedPreferences p = prefs(c);
        int current = Math.max(0, p.getInt(KEY_COINS, 500));
        if (current < amount) return false;
        p.edit().putInt(KEY_COINS, current - amount).apply();
        return true;
    }
    public static void addCoins(Context c, int amount) {
        if (amount <= 0) return;
        ensureCreated(c);
        SharedPreferences p = prefs(c);
        long next = (long)Math.max(0, p.getInt(KEY_COINS, 500)) + amount;
        p.edit().putInt(KEY_COINS, (int)Math.min(Integer.MAX_VALUE, next)).apply();
    }
    public static boolean isOwned(Context c, String itemId) {
        return prefs(c).getBoolean("owned_" + itemId, "classic".equals(itemId));
    }
    public static void setOwned(Context c, String itemId, boolean owned) {
        prefs(c).edit().putBoolean("owned_" + itemId, owned).apply();
    }
}
