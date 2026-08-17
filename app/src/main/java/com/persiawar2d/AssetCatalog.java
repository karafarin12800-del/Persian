package com.persiawar2d;

import android.content.Context;

import java.io.BufferedInputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;
import java.util.ArrayList;
import java.util.List;

/** Runtime catalog for existing packaged art. */
public final class AssetCatalog {
    private final Context context;

    public AssetCatalog(Context context) {
        this.context = context.getApplicationContext();
    }

    public List<String> listBuildingPngs() throws Exception {
        ArrayList<String> result = new ArrayList<>();
        File zip = copyZipToCache();
        try (ZipFile z = new ZipFile(zip)) {
            java.util.Enumeration<? extends ZipEntry> entries = z.entries();
            while (entries.hasMoreElements()) {
                ZipEntry e = entries.nextElement();
                if (e.isDirectory()) continue;
                String n = e.getName().toLowerCase(java.util.Locale.US);
                if (!n.endsWith(".png")) continue;
                if (n.contains("building") || n.contains("house") || n.contains("shop")) {
                    result.add(e.getName());
                }
            }
        }
        return result;
    }

    public File extract(String entryName) throws Exception {
        File zipFile = copyZipToCache();
        File out = new File(context.getCacheDir(), "scene_" + Integer.toHexString(entryName.hashCode()) + ".png");
        if (out.exists() && out.length() > 0) return out;
        try (ZipFile z = new ZipFile(zipFile)) {
            ZipEntry entry = z.getEntry(entryName);
            if (entry == null) throw new IllegalArgumentException("Missing asset: " + entryName);
            try (InputStream in = new BufferedInputStream(z.getInputStream(entry));
                 FileOutputStream fos = new FileOutputStream(out)) {
                byte[] buf = new byte[16 * 1024];
                int n;
                while ((n = in.read(buf)) >= 0) fos.write(buf, 0, n);
            }
        }
        return out;
    }

    private File copyZipToCache() throws Exception {
        File out = new File(context.getCacheDir(), "kenney_isometric-buildings.zip");
        if (out.exists() && out.length() > 0) return out;
        try (InputStream in = context.getAssets().open("original_packages/kenney_isometric-buildings.zip");
             FileOutputStream fos = new FileOutputStream(out)) {
            byte[] buf = new byte[32 * 1024];
            int n;
            while ((n = in.read(buf)) >= 0) fos.write(buf, 0, n);
        }
        return out;
    }
}
