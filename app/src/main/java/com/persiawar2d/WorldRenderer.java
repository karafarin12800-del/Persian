package com.persiawar2d;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import java.util.ArrayList;
import java.util.Random;

/**
 * Hand-authored 2.5D city renderer.
 * The map is orthogonal, fully connected and uses independent terrain, road,
 * building and vegetation primitives instead of one stretched background image.
 */
public final class WorldRenderer {
    public static final float WORLD_SIZE = 6000f;
    private final Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final ArrayList<Road> roads = new ArrayList<>();
    private final ArrayList<Building> buildings = new ArrayList<>();
    private final ArrayList<Tree> trees = new ArrayList<>();
    private final ArrayList<Mark> terrainMarks = new ArrayList<>();
    private final Random random = new Random(20260817L);

    public WorldRenderer(Context context) {
        buildLayout();
    }

    private void buildLayout() {
        addRoad(300, 900, 5700, 900, 170, true);
        addRoad(300, 2850, 5700, 2850, 155, true);
        addRoad(300, 4800, 5700, 4800, 175, true);
        addRoad(1000, 300, 1000, 5700, 165, false);
        addRoad(3000, 300, 3000, 5700, 180, false);
        addRoad(5000, 300, 5000, 5700, 150, false);

        addRoad(420, 1350, 1000, 1350, 82, true);
        addRoad(1000, 1500, 1650, 1500, 78, true);
        addRoad(1650, 1500, 1650, 900, 78, false);
        addRoad(2050, 1150, 2050, 2850, 84, false);
        addRoad(3000, 1250, 3600, 1250, 78, true);
        addRoad(3600, 1250, 3600, 900, 78, false);
        addRoad(4050, 1600, 5000, 1600, 82, true);
        addRoad(4450, 1600, 4450, 2850, 80, false);
        addRoad(350, 2300, 1000, 2300, 78, true);
        addRoad(1000, 2150, 1450, 2150, 76, true);
        addRoad(1450, 2150, 1450, 2850, 76, false);
        addRoad(1750, 2400, 1750, 2850, 78, false);
        addRoad(2350, 2850, 2350, 3550, 82, false);
        addRoad(3000, 2350, 3650, 2350, 76, true);
        addRoad(3650, 2350, 3650, 2850, 76, false);
        addRoad(4050, 2550, 5000, 2550, 78, true);
        addRoad(5400, 2200, 5400, 2850, 78, false);
        addRoad(430, 3650, 1000, 3650, 82, true);
        addRoad(1000, 3900, 1550, 3900, 76, true);
        addRoad(1550, 3900, 1550, 4800, 76, false);
        addRoad(1900, 4100, 1900, 4800, 78, false);
        addRoad(2200, 3500, 3000, 3500, 82, true);
        addRoad(3000, 3850, 3650, 3850, 78, true);
        addRoad(3650, 3850, 3650, 4800, 78, false);
        addRoad(4100, 3550, 5000, 3550, 82, true);
        addRoad(4550, 3550, 4550, 4800, 78, false);
        addRoad(5250, 3300, 5250, 4800, 80, false);
        addRoad(450, 5350, 1000, 5350, 80, true);
        addRoad(1250, 5200, 1250, 5700, 76, false);
        addRoad(1750, 5400, 3000, 5400, 82, true);
        addRoad(3450, 5200, 3450, 5700, 76, false);
        addRoad(3800, 5400, 5000, 5400, 82, true);
        addRoad(5450, 5100, 5450, 5700, 76, false);

        int[][] b = {
                {380,390,430,280},{1320,400,430,290},{2080,390,500,300},{3220,400,480,310},{4180,380,450,300},{5220,380,360,330},
                {360,1030,450,300},{1260,1060,500,320},{2200,1040,470,310},{3220,1020,470,320},{4050,1040,440,300},{5220,1040,390,300},
                {350,1750,470,330},{1230,1680,420,300},{1760,1730,400,300},{2300,1700,470,310},{3190,1700,500,330},{3830,1720,430,300},{5130,1720,420,320},
                {360,3000,470,320},{1240,3040,440,290},{1750,3040,430,320},{2420,3040,430,300},{3190,3040,500,320},{3910,3050,420,300},{5120,3030,400,310},
                {360,3950,470,320},{1210,4030,470,310},{1790,4060,420,300},{2250,4000,480,320},{3190,4030,480,310},{3900,4010,440,300},{5150,4000,400,320},
                {360,4900,500,300},{1240,4930,440,320},{1800,4940,470,300},{2450,4920,450,310},{3190,4920,500,320},{3910,4930,440,300},{5140,4920,400,300},
                {420,5450,430,250},{1380,5450,420,250},{2200,5520,480,240},{3250,5480,450,250},{4030,5520,440,240},{5140,5450,380,250}
        };
        int i = 0;
        for (int[] v : b) {
            buildings.add(new Building(v[0], v[1], v[2], v[3], i % 7, 0.72f + (i % 4) * 0.09f));
            i++;
        }

        for (int n = 0; n < 160; n++) {
            float x = 220 + random.nextFloat() * 5560f;
            float y = 220 + random.nextFloat() * 5560f;
            if (!nearRoad(x, y, 115) && !nearBuilding(x, y, 85)) trees.add(new Tree(x, y, 22 + random.nextFloat() * 20, n % 4));
        }
        for (int n = 0; n < 520; n++) {
            float x = random.nextFloat() * WORLD_SIZE;
            float y = random.nextFloat() * WORLD_SIZE;
            if (!nearRoad(x, y, 95)) terrainMarks.add(new Mark(x, y, 18 + random.nextFloat() * 70, n % 5));
        }
    }

    private void addRoad(float x1, float y1, float x2, float y2, float width, boolean horizontal) {
        if (horizontal) roads.add(new Road(Math.min(x1, x2), y1, Math.max(x1, x2), y2, width, true));
        else roads.add(new Road(x1, Math.min(y1, y2), x2, Math.max(y1, y2), width, false));
    }

    private boolean nearRoad(float x, float y, float pad) {
        for (Road r : roads) {
            if (r.horizontal) {
                if (x >= r.x1 - pad && x <= r.x2 + pad && Math.abs(y - r.y1) <= r.width * .5f + pad) return true;
            } else if (y >= r.y1 - pad && y <= r.y2 + pad && Math.abs(x - r.x1) <= r.width * .5f + pad) return true;
        }
        return false;
    }

    private boolean nearBuilding(float x, float y, float pad) {
        for (Building b : buildings) {
            if (x >= b.x - pad && x <= b.x + b.w + pad && y >= b.y - pad && y <= b.y + b.h + pad) return true;
        }
        return false;
    }

    public boolean isBlocked(float x, float y, float radius) {
        if (x < 80 || y < 80 || x > WORLD_SIZE - 80 || y > WORLD_SIZE - 80) return true;
        for (Building b : buildings) {
            float left = b.x - radius, right = b.x + b.w + radius;
            float top = b.y - radius, bottom = b.y + b.h + radius;
            if (x > left && x < right && y > top && y < bottom) return true;
        }
        return false;
    }

    public void draw(Canvas c, float playerX, float playerY, float scale, float viewW, float viewH, float hudH) {
        p.setStyle(Paint.Style.FILL);
        p.setColor(Color.rgb(38,55,43));
        c.drawRect(0,0,viewW,viewH,p);
        float ox=viewW*.5f-playerX*scale;
        float oy=hudH+(viewH-hudH)*.5f-playerY*scale;
        c.save();
        c.translate(ox,oy);
        drawGround(c,scale);
        drawRoads(c,scale);
        drawBehindDecor(c,playerY,scale);
        c.restore();
    }

    public void drawForeground(Canvas c,float playerX,float playerY,float scale,float viewW,float viewH,float hudH) {
        float ox=viewW*.5f-playerX*scale;
        float oy=hudH+(viewH-hudH)*.5f-playerY*scale;
        c.save();
        c.translate(ox,oy);
        for(Tree t:trees) if(t.y>playerY) drawTree(c,t,scale);
        for(Building b:buildings) if(b.y+b.h>playerY) drawBuilding(c,b,scale);
        c.restore();
    }

    private void drawGround(Canvas c,float s) {
        p.setStyle(Paint.Style.FILL);
        p.setColor(Color.rgb(127,113,80));
        c.drawRect(0,0,WORLD_SIZE*s,WORLD_SIZE*s,p);
        int[] colors={0x1B46583D,0x1E6A5940,0x163F4B37,0x1C907B52,0x1834312A};
        for(Mark m:terrainMarks){
            float x=m.x*s,y=m.y*s,r=m.size*s;
            p.setColor(colors[m.kind]);
            c.drawOval(x-r,y-r*.55f,x+r,y+r*.55f,p);
        }
        p.setColor(0x1A3A3227);
        for(int y=180;y<WORLD_SIZE;y+=310) for(int x=140;x<WORLD_SIZE;x+=360){
            c.drawCircle(x*s,(y+((x/360)%3)*32)*s,10*s,p);
            c.drawCircle((x+20)*s,(y-9)*s,6*s,p);
        }
    }

    private void drawRoads(Canvas c,float s) {
        for(Road r:roads){
            p.setStyle(Paint.Style.FILL);
            p.setColor(Color.rgb(61,58,51));
            if(r.horizontal)c.drawRect(r.x1*s,(r.y1-r.width*.5f)*s,r.x2*s,(r.y1+r.width*.5f)*s,p);
            else c.drawRect((r.x1-r.width*.5f)*s,r.y1*s,(r.x1+r.width*.5f)*s,r.y2*s,p);
            p.setColor(Color.rgb(100,93,76));
            if(r.horizontal){
                c.drawRect(r.x1*s,(r.y1-r.width*.5f)*s,r.x2*s,(r.y1-r.width*.38f)*s,p);
                c.drawRect(r.x1*s,(r.y1+r.width*.38f)*s,r.x2*s,(r.y1+r.width*.5f)*s,p);
            }else{
                c.drawRect((r.x1-r.width*.5f)*s,r.y1*s,(r.x1-r.width*.38f)*s,r.y2*s,p);
                c.drawRect((r.x1+r.width*.38f)*s,r.y1*s,(r.x1+r.width*.5f)*s,r.y2*s,p);
            }
            p.setColor(0x889B916E);
            for(float t=70;t<(r.horizontal?r.x2-r.x1:r.y2-r.y1)-30;t+=130){
                if(r.horizontal)c.drawRect((r.x1+t)*s,(r.y1-3)*s,(r.x1+t+65)*s,(r.y1+3)*s,p);
                else c.drawRect((r.x1-3)*s,(r.y1+t)*s,(r.x1+3)*s,(r.y1+t+65)*s,p);
            }
        }
        p.setColor(0x789C9678);
        int[] ys={900,2850,4800}, xs={1000,3000,5000};
        for(int y:ys) for(int x:xs) for(int k=-3;k<=3;k++) c.drawRect((x+k*22-6)*s,(y-95)*s,(x+k*22+6)*s,(y+95)*s,p);
    }

    private void drawBehindDecor(Canvas c,float playerY,float s) {
        for(Tree t:trees) if(t.y<=playerY) drawTree(c,t,s);
        for(Building b:buildings) if(b.y+b.h<=playerY) drawBuilding(c,b,s);
    }

    private void drawBuilding(Canvas c,Building b,float s) {
        float x=b.x*s,y=b.y*s,w=b.w*s,h=b.h*s;
        float lift=Math.max(55*s,b.depth*105*s);
        int[] wall={Color.rgb(159,124,81),Color.rgb(144,105,69),Color.rgb(177,142,93),Color.rgb(121,90,64),Color.rgb(186,151,100),Color.rgb(151,113,74),Color.rgb(170,130,84)};
        p.setStyle(Paint.Style.FILL);
        p.setColor(0x35000000); c.drawOval(x-10*s,y+h-6*s,x+w+10*s,y+h+24*s,p);
        // Vertical wall mass matches the exact footprint used for collision.
        p.setColor(Color.rgb(82,61,46)); c.drawRect(x,y-lift,x+w,y+h,p);
        p.setColor(wall[b.kind%wall.length]); c.drawRect(x+5*s,y-lift+5*s,x+w-5*s,y+h-4*s,p);

        Path roof=new Path();
        roof.moveTo(x-14*s,y-lift);
        roof.lineTo(x+w*.5f,y-lift-30*s);
        roof.lineTo(x+w+14*s,y-lift);
        roof.lineTo(x+w,y-lift+16*s);
        roof.lineTo(x,y-lift+16*s);
        roof.close();
        p.setColor(Color.rgb(87+b.kind*5,62+b.kind*3,47)); c.drawPath(roof,p);

        int cols=Math.max(2,(int)(b.w/105));
        for(int j=0;j<cols;j++){
            float wx=x+22*s+j*Math.max(1,(w-44*s)/Math.max(1,cols-1));
            p.setColor(Color.rgb(226,193,131));
            c.drawRect(wx-8*s,y-lift+30*s,wx+12*s,y-lift+64*s,p);
            p.setColor(0x664C3D2D); c.drawRect(wx-8*s,y-lift+30*s,wx+12*s,y-lift+33*s,p);
        }
        p.setColor(Color.rgb(73,52,39));
        c.drawRect(x+w*.43f,y+h-64*s,x+w*.57f,y+h,p);
        if((b.kind&1)==0){p.setColor(Color.rgb(199,155,82));c.drawRect(x+w*.12f,y-lift-2*s,x+w*.35f,y-lift+10*s,p);}
    }

    private void drawTree(Canvas c,Tree t,float s) {
        float x=t.x*s,y=t.y*s,r=t.r*s;
        p.setStyle(Paint.Style.FILL);
        p.setColor(0x6A4D3928); c.drawRect(x-4*s,y-2*s,x+4*s,y+35*s,p);
        int[] greens={0xFF365E39,0xFF3E6C40,0xFF2E5333,0xFF477445};
        p.setColor(greens[t.kind]); c.drawCircle(x,y-r*.25f,r,p);
        p.setColor(0xFF56814C); c.drawCircle(x-r*.35f,y-r*.42f,r*.55f,p);
        p.setColor(0xFF6A9554); c.drawCircle(x+r*.30f,y-r*.38f,r*.45f,p);
        p.setColor(0x32000000); c.drawOval(x-r*.9f,y+12*s,x+r*.9f,y+28*s,p);
    }

    static final class Road { final float x1,y1,x2,y2,width; final boolean horizontal; Road(float x1,float y1,float x2,float y2,float width,boolean horizontal){this.x1=x1;this.y1=y1;this.x2=x2;this.y2=y2;this.width=width;this.horizontal=horizontal;} }
    static final class Building { final float x,y,w,h,depth; final int kind; Building(float x,float y,float w,float h,int kind,float depth){this.x=x;this.y=y;this.w=w;this.h=h;this.kind=kind;this.depth=depth;} }
    static final class Tree { final float x,y,r; final int kind; Tree(float x,float y,float r,int kind){this.x=x;this.y=y;this.r=r;this.kind=kind;} }
    static final class Mark { final float x,y,size; final int kind; Mark(float x,float y,float size,int kind){this.x=x;this.y=y;this.size=size;this.kind=kind;} }
}
