package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.Random;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/** Real isometric presentation layer. Existing gameplay coordinates stay X/Y. */
public final class IsoWorldRenderer {
    public static final float WORLD_SIZE = 6000f;
    private final Paint p = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final ArrayList<Building> buildings = new ArrayList<>();
    private final ArrayList<Tree> trees = new ArrayList<>();
    private final ArrayList<Bitmap> buildingArt = new ArrayList<>();
    private final Random random = new Random(20260817L);

    public IsoWorldRenderer(Context context) {
        loadBuildingArt(context);
        buildCity();
    }

    private void loadBuildingArt(Context context) {
        try (InputStream raw = context.getAssets().open("original_packages/kenney_isometric-buildings.zip");
             ZipInputStream zin = new ZipInputStream(raw)) {
            ZipEntry e;
            while ((e = zin.getNextEntry()) != null) {
                if (e.isDirectory()) continue;
                String n = e.getName().toLowerCase(java.util.Locale.US);
                if (!n.endsWith(".png") || !n.contains("buildingtile")) continue;
                byte[] bytes = read(zin);
                Bitmap b = BitmapFactory.decodeByteArray(bytes, 0, bytes.length);
                if (b != null && b.getWidth() >= 48 && b.getHeight() >= 48 && b.getWidth() <= 2048 && b.getHeight() <= 2048) {
                    buildingArt.add(b);
                }
            }
        } catch (Exception ignored) {
            // A missing optional art package must never prevent the game from starting.
        }
    }

    private byte[] read(ZipInputStream z) throws Exception {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buf = new byte[8192];
        int n;
        while ((n = z.read(buf)) > 0) out.write(buf, 0, n);
        return out.toByteArray();
    }

    private void buildCity() {
        int[][] blocks = {
            {350,350,520,360},{1320,350,520,360},{2250,350,520,360},{3180,350,520,360},{4110,350,520,360},{5040,350,500,360},
            {350,1080,520,380},{1320,1080,520,380},{2250,1080,520,380},{3180,1080,520,380},{4110,1080,520,380},{5040,1080,500,380},
            {350,1840,520,380},{1320,1840,520,380},{2250,1840,520,380},{3180,1840,520,380},{4110,1840,520,380},{5040,1840,500,380},
            {350,2600,520,380},{1320,2600,520,380},{2250,2600,520,380},{3180,2600,520,380},{4110,2600,520,380},{5040,2600,500,380},
            {350,3360,520,380},{1320,3360,520,380},{2250,3360,520,380},{3180,3360,520,380},{4110,3360,520,380},{5040,3360,500,380},
            {350,4120,520,380},{1320,4120,520,380},{2250,4120,520,380},{3180,4120,520,380},{4110,4120,520,380},{5040,4120,500,380},
            {350,4880,520,350},{1320,4880,520,350},{2250,4880,520,350},{3180,4880,520,350},{4110,4880,520,350},{5040,4880,500,350}
        };
        for (int i = 0; i < blocks.length; i++) {
            int[] b = blocks[i];
            buildings.add(new Building(b[0], b[1], b[2], b[3], i));
        }
        for (int i = 0; i < 95; i++) {
            float x = 180 + random.nextFloat() * 5640f;
            float y = 180 + random.nextFloat() * 5640f;
            if (nearRoad(x, y, 130) || nearBuilding(x, y, 110)) continue;
            trees.add(new Tree(x, y, 28 + random.nextFloat() * 22f, i & 3));
        }
    }

    public float screenX(float worldX, float worldY, float cameraX, float cameraY, float centerX, float scale) {
        return centerX + (worldX - cameraX - worldY + cameraY) * 0.5f * scale;
    }

    public float screenY(float worldX, float worldY, float cameraX, float cameraY, float centerY, float scale) {
        return centerY + (worldX - cameraX + worldY - cameraY) * 0.25f * scale;
    }

    public float[] screenToWorld(float sx, float sy, float cameraX, float cameraY, float centerX, float centerY, float scale) {
        float a = (sx - centerX) / (0.5f * scale);
        float b = (sy - centerY) / (0.25f * scale);
        return new float[]{cameraX + (a + b) * 0.5f, cameraY + (b - a) * 0.5f};
    }

    public void draw(Canvas c, float cameraX, float cameraY, float centerX, float centerY, float scale, float hudH) {
        c.drawColor(Color.rgb(121, 108, 76));
        drawGround(c, cameraX, cameraY, centerX, centerY, scale, hudH);
        drawRoads(c, cameraX, cameraY, centerX, centerY, scale, hudH);

        ArrayList<DepthItem> all = new ArrayList<>();
        for (Building b : buildings) all.add(new DepthItem(b.x + b.w * .5f, b.y + b.h, 0, b));
        for (Tree t : trees) all.add(new DepthItem(t.x, t.y, 1, t));
        Collections.sort(all, Comparator.comparingDouble((DepthItem d) -> d.y + d.x).thenComparingInt(d -> d.kind));
        for (DepthItem item : all) {
            if (item.kind == 0) drawBuilding(c, (Building)item.object, cameraX, cameraY, centerX, centerY, scale);
            else drawTree(c, (Tree)item.object, cameraX, cameraY, centerX, centerY, scale);
        }
    }

    private void drawGround(Canvas c, float cx, float cy, float ox, float oy, float s, float hudH) {
        p.setStyle(Paint.Style.FILL);
        p.setColor(0xff857656);
        c.drawRect(0, hudH, c.getWidth(), c.getHeight(), p);
        p.setColor(0x143f4938);
        for (int x = 0; x <= (int)WORLD_SIZE; x += 250) {
            float sx = screenX(x, 0, cx, cy, ox, s);
            float sy = screenY(x, 0, cx, cy, oy, s);
            float sx2 = screenX(x, WORLD_SIZE, cx, cy, ox, s);
            float sy2 = screenY(x, WORLD_SIZE, cx, cy, oy, s);
            c.drawLine(sx, sy, sx2, sy2, p);
        }
        for (int y = 0; y <= (int)WORLD_SIZE; y += 250) {
            float sx = screenX(0, y, cx, cy, ox, s);
            float sy = screenY(0, y, cx, cy, oy, s);
            float sx2 = screenX(WORLD_SIZE, y, cx, cy, ox, s);
            float sy2 = screenY(WORLD_SIZE, y, cx, cy, oy, s);
            c.drawLine(sx, sy, sx2, sy2, p);
        }
    }

    private void drawRoads(Canvas c, float cx, float cy, float ox, float oy, float s, float hudH) {
        p.setStyle(Paint.Style.FILL);
        p.setColor(0xff44413a);
        for (int y = 760; y <= 5300; y += 760) drawDiamondRoad(c, 300, y, 5700, y, 150, cx, cy, ox, oy, s);
        for (int x = 950; x <= 5050; x += 820) drawDiamondRoad(c, x, 300, x, 5700, 150, cx, cy, ox, oy, s);
        p.setColor(0x889a906e);
        p.setStrokeWidth(Math.max(2f, 3f * s));
        for (int y = 760; y <= 5300; y += 760) {
            float a = screenX(500, y, cx, cy, ox, s), b = screenY(500, y, cx, cy, oy, s);
            float d = screenX(5400, y, cx, cy, ox, s), e = screenY(5400, y, cx, cy, oy, s);
            c.drawLine(a, b, d, e, p);
        }
    }

    private void drawDiamondRoad(Canvas c, float x1, float y1, float x2, float y2, float width,
                                 float cx, float cy, float ox, float oy, float s) {
        float dx = x2 - x1, dy = y2 - y1;
        float len = Math.max(1f, (float)Math.hypot(dx, dy));
        float nx = -dy / len * width * .5f, ny = dx / len * width * .5f;
        float[] px = {x1+nx,x2+nx,x2-nx,x1-nx};
        float[] py = {y1+ny,y2+ny,y2-ny,y1-ny};
        android.graphics.Path path = new android.graphics.Path();
        path.moveTo(screenX(px[0],py[0],cx,cy,ox,s), screenY(px[0],py[0],cx,cy,oy,s));
        for(int i=1;i<4;i++) path.lineTo(screenX(px[i],py[i],cx,cy,ox,s),screenY(px[i],py[i],cx,cy,oy,s));
        path.close(); c.drawPath(path,p);
    }

    private void drawBuilding(Canvas c, Building b, float cx, float cy, float ox, float oy, float s) {
        float anchorX = b.x + b.w * .5f;
        float anchorY = b.y + b.h;
        float x = screenX(anchorX, anchorY, cx, cy, ox, s);
        float y = screenY(anchorX, anchorY, cx, cy, oy, s);
        float base = Math.max(70f, b.w * .58f * s);
        if (buildingArt.isEmpty()) {
            p.setColor(0xffc8bea0); c.drawRect(x-base*.45f,y-base*.75f,x+base*.45f,y,p); return;
        }
        Bitmap art = buildingArt.get(Math.floorMod(b.kind, buildingArt.size()));
        float w = Math.max(70f, b.w * .70f * s);
        float h = w * art.getHeight() / (float)Math.max(1, art.getWidth());
        h = Math.min(h, 270f*s);
        w = h * art.getWidth() / (float)Math.max(1,art.getHeight());
        p.setColor(0x42000000);
        c.drawOval(new RectF(x-w*.42f,y-4*s,x+w*.42f,y+15*s),p);
        c.drawBitmap(art,null,new RectF(x-w*.5f,y-h,x+w*.5f,y),p);
    }

    private void drawTree(Canvas c, Tree t, float cx, float cy, float ox, float oy, float s) {
        float x=screenX(t.x,t.y,cx,cy,ox,s), y=screenY(t.x,t.y,cx,cy,oy,s), r=t.r*s;
        p.setColor(0x44000000);c.drawOval(new RectF(x-r*.9f,y-3*s,x+r*.9f,y+10*s),p);
        p.setColor(0xff5a4630);c.drawRect(x-4*s,y-34*s,x+4*s,y,p);
        int[] g={0xff3d6839,0xff4a7740,0xff315a34,0xff568249};p.setColor(g[t.kind]);c.drawCircle(x,y-r*.65f,r,p);
        p.setColor(0xff65904e);c.drawCircle(x+r*.28f,y-r*.85f,r*.58f,p);
    }

    public boolean isBlocked(float x,float y,float radius){
        if(x<80||y<80||x>WORLD_SIZE-80||y>WORLD_SIZE-80)return true;
        for(Building b:buildings)if(x>b.x-radius&&x<b.x+b.w+radius&&y>b.y-radius&&y<b.y+b.h+radius)return true;
        return false;
    }
    private boolean nearRoad(float x,float y,float pad){return Math.abs((y/760f)-Math.round(y/760f))*760f<90+pad || Math.abs((x/820f)-Math.round(x/820f))*820f<90+pad;}
    private boolean nearBuilding(float x,float y,float pad){for(Building b:buildings)if(x>b.x-pad&&x<b.x+b.w+pad&&y>b.y-pad&&y<b.y+b.h+pad)return true;return false;}

    static final class Building {final float x,y,w,h;final int kind;Building(float x,float y,float w,float h,int kind){this.x=x;this.y=y;this.w=w;this.h=h;this.kind=kind;}}
    static final class Tree {final float x,y,r;final int kind;Tree(float x,float y,float r,int kind){this.x=x;this.y=y;this.r=r;this.kind=kind;}}
    static final class DepthItem {final float x,y;final int kind;final Object object;DepthItem(float x,float y,int kind,Object object){this.x=x;this.y=y;this.kind=kind;this.object=object;}}
}
