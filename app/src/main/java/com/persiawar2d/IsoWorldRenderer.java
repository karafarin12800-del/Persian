package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.Random;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/**
 * Production isometric presentation layer. It deliberately uses the supplied
 * Kenney building sprites instead of drawing placeholder boxes.
 */
public final class IsoWorldRenderer {
    public static final float WORLD_SIZE = 6000f;
    private static final float ROAD_W = 170f;
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
            ArrayList<NamedBitmap> candidates = new ArrayList<>();
            ZipEntry e;
            while ((e = zin.getNextEntry()) != null) {
                if (e.isDirectory()) continue;
                String n = e.getName().toLowerCase(java.util.Locale.US);
                String compact = n.replaceAll("[^a-z0-9]", "");
                if (!n.endsWith(".png")) continue;
                if (compact.contains("preview") || compact.contains("spritesheet") || compact.contains("atlas")) continue;
                // Kenney's pack contains separate building sprites plus auxiliary tiles.
                // Accept both old and new filename conventions (buildingTile, building-tile, etc.).
                if (!(compact.contains("buildingtile") || compact.contains("building"))) continue;
                byte[] bytes = read(zin);
                Bitmap b = BitmapFactory.decodeByteArray(bytes, 0, bytes.length);
                if (b == null) continue;
                if (b.getWidth() < 48 || b.getHeight() < 48 || b.getWidth() > 1024 || b.getHeight() > 1024) {
                    b.recycle();
                    continue;
                }
                candidates.add(new NamedBitmap(n, b));
            }
            Collections.sort(candidates, Comparator.comparing(v -> v.name));
            // Keep memory predictable on older Android phones while retaining visual variety.
            for (int i = 0; i < Math.min(48, candidates.size()); i++) buildingArt.add(candidates.get(i).bitmap);
            for (int i = Math.min(48, candidates.size()); i < candidates.size(); i++) candidates.get(i).bitmap.recycle();
        } catch (Exception ignored) {
            // Optional artwork must not prevent the game from opening.
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
        // Deliberately varied blocks. Buildings are offset from intersections so
        // the map reads as a city rather than a repeated checkerboard of squares.
        float[][] specs = {
                {380,330,430,300},{1420,300,520,340},{2520,330,410,300},{3660,300,520,350},{4880,350,430,300},
                {300,1160,520,350},{1450,1120,420,320},{2480,1160,560,340},{3650,1110,430,360},{4900,1160,520,330},
                {420,1950,460,300},{1510,1910,540,350},{2510,1960,420,300},{3700,1910,520,340},{4930,1980,450,300},
                {300,2800,520,340},{1450,2750,440,300},{2510,2820,560,350},{3650,2780,460,320},{4890,2820,520,340},
                {420,3670,440,300},{1480,3620,520,340},{2530,3700,430,300},{3680,3640,540,350},{4910,3700,440,300},
                {320,4520,540,340},{1490,4460,430,300},{2510,4560,520,330},{3680,4490,440,300},{4920,4560,540,350},
                {430,5300,460,300},{1540,5240,520,330},{2580,5320,440,300},{3740,5240,520,330},{4930,5300,460,300}
        };
        for (int i = 0; i < specs.length; i++) {
            float[] b = specs[i];
            buildings.add(new Building(b[0], b[1], b[2], b[3], i));
        }
        for (int i = 0; i < 105; i++) {
            float x = 150 + random.nextFloat() * 5700f;
            float y = 150 + random.nextFloat() * 5700f;
            if (nearRoad(x, y, 150) || nearBuilding(x, y, 100)) continue;
            trees.add(new Tree(x, y, 30 + random.nextFloat() * 24f, i & 3));
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

        ArrayList<DepthItem> all = new ArrayList<>(buildings.size() + trees.size());
        for (Building b : buildings) all.add(new DepthItem(b.x + b.w * .5f, b.y + b.h, 0, b));
        for (Tree t : trees) all.add(new DepthItem(t.x, t.y, 1, t));
        Collections.sort(all, Comparator.comparingDouble((DepthItem d) -> d.x + d.y).thenComparingInt(d -> d.kind));
        for (DepthItem item : all) {
            if (item.kind == 0) drawBuilding(c, (Building)item.object, cameraX, cameraY, centerX, centerY, scale);
            else drawTree(c, (Tree)item.object, cameraX, cameraY, centerX, centerY, scale);
        }
    }

    private void drawGround(Canvas c, float cx, float cy, float ox, float oy, float s, float hudH) {
        p.setStyle(Paint.Style.FILL);
        p.setColor(0xff857656);
        c.drawRect(0, hudH, c.getWidth(), c.getHeight(), p);
        // Subtle diamond paving gives the map a real isometric floor instead of a flat fill.
        p.setColor(0x123f4938);
        p.setStrokeWidth(Math.max(1f, s));
        for (int x = 0; x <= (int)WORLD_SIZE; x += 250) {
            c.drawLine(screenX(x,0,cx,cy,ox,s),screenY(x,0,cx,cy,oy,s),screenX(x,WORLD_SIZE,cx,cy,ox,s),screenY(x,WORLD_SIZE,cx,cy,oy,s),p);
        }
        for (int y = 0; y <= (int)WORLD_SIZE; y += 250) {
            c.drawLine(screenX(0,y,cx,cy,ox,s),screenY(0,y,cx,cy,oy,s),screenX(WORLD_SIZE,y,cx,cy,ox,s),screenY(WORLD_SIZE,y,cx,cy,oy,s),p);
        }
    }

    private void drawRoads(Canvas c, float cx, float cy, float ox, float oy, float s, float hudH) {
        p.setStyle(Paint.Style.FILL);
        p.setColor(0xff45423d);
        for (int y = 760; y <= 5300; y += 760) drawDiamondRoad(c, 200, y, 5800, y, ROAD_W, cx, cy, ox, oy, s);
        for (int x = 950; x <= 5050; x += 820) drawDiamondRoad(c, x, 200, x, 5800, ROAD_W, cx, cy, ox, oy, s);
        p.setColor(0x806f6a59);
        p.setStrokeWidth(Math.max(2f, 3f * s));
        for (int y = 760; y <= 5300; y += 760) {
            c.drawLine(screenX(500,y,cx,cy,ox,s),screenY(500,y,cx,cy,oy,s),screenX(5500,y,cx,cy,ox,s),screenY(5500,y,cx,cy,oy,s),p);
        }
    }

    private void drawDiamondRoad(Canvas c, float x1, float y1, float x2, float y2, float width,
                                 float cx, float cy, float ox, float oy, float s) {
        float dx=x2-x1,dy=y2-y1,len=Math.max(1f,(float)Math.hypot(dx,dy));
        float nx=-dy/len*width*.5f,ny=dx/len*width*.5f;
        float[] px={x1+nx,x2+nx,x2-nx,x1-nx},py={y1+ny,y2+ny,y2-ny,y1-ny};
        Path path=new Path();path.moveTo(screenX(px[0],py[0],cx,cy,ox,s),screenY(px[0],py[0],cx,cy,oy,s));
        for(int i=1;i<4;i++)path.lineTo(screenX(px[i],py[i],cx,cy,ox,s),screenY(px[i],py[i],cx,cy,oy,s));
        path.close();c.drawPath(path,p);
    }

    private void drawBuilding(Canvas c, Building b, float cx, float cy, float ox, float oy, float s) {
        float anchorX=b.x+b.w*.5f,anchorY=b.y+b.h;
        float x=screenX(anchorX,anchorY,cx,cy,ox,s),y=screenY(anchorX,anchorY,cx,cy,oy,s);
        if (buildingArt.isEmpty()) {
            // Only a safety fallback; normal builds use the supplied Kenney sprites.
            p.setColor(0xff777267);c.drawRect(x-55*s,y-90*s,x+55*s,y,p);return;
        }
        Bitmap art=buildingArt.get(Math.floorMod(b.kind,buildingArt.size()));
        float targetW=Math.max(120f,Math.min(300f,b.w*.72f*s));
        float targetH=targetW*art.getHeight()/(float)Math.max(1,art.getWidth());
        if(targetH>330f*s){targetH=330f*s;targetW=targetH*art.getWidth()/(float)Math.max(1,art.getHeight());}
        p.setColor(0x48000000);c.drawOval(new RectF(x-targetW*.43f,y-5*s,x+targetW*.43f,y+17*s),p);
        c.drawBitmap(art,null,new RectF(x-targetW*.5f,y-targetH,x+targetW*.5f,y),p);
    }

    private void drawTree(Canvas c, Tree t, float cx, float cy, float ox, float oy, float s) {
        float x=screenX(t.x,t.y,cx,cy,ox,s),y=screenY(t.x,t.y,cx,cy,oy,s),r=t.r*s;
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
    private boolean nearRoad(float x,float y,float pad){
        return Math.abs((y/760f)-Math.round(y/760f))*760f<(ROAD_W*.5f)+pad || Math.abs((x/820f)-Math.round(x/820f))*820f<(ROAD_W*.5f)+pad;
    }
    private boolean nearBuilding(float x,float y,float pad){for(Building b:buildings)if(x>b.x-pad&&x<b.x+b.w+pad&&y>b.y-pad&&y<b.y+b.h+pad)return true;return false;}

    static final class NamedBitmap { final String name; final Bitmap bitmap; NamedBitmap(String n,Bitmap b){name=n;bitmap=b;} }
    static final class Building {final float x,y,w,h;final int kind;Building(float x,float y,float w,float h,int kind){this.x=x;this.y=y;this.w=w;this.h=h;this.kind=kind;}}
    static final class Tree {final float x,y,r;final int kind;Tree(float x,float y,float r,int kind){this.x=x;this.y=y;this.r=r;this.kind=kind;}}
    static final class DepthItem {final float x,y;final int kind;final Object object;DepthItem(float x,float y,int kind,Object object){this.x=x;this.y=y;this.kind=kind;this.object=object;}}
}
