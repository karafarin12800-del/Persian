package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.Random;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/** City renderer. Uses only complete-looking buildingTiles from the bundled Kenney package. */
public final class WorldRenderer {
    public static final float WORLD_SIZE=6000f;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final ArrayList<Road> roads=new ArrayList<>();
    private final ArrayList<Building> buildings=new ArrayList<>();
    private final ArrayList<Tree> trees=new ArrayList<>();
    private final ArrayList<Mark> terrainMarks=new ArrayList<>();
    private final ArrayList<Bitmap> buildingArt=new ArrayList<>();
    private final Random random=new Random(20260817L);

    public WorldRenderer(Context context){loadRealBuildingArt(context);buildLayout();}

    private void loadRealBuildingArt(Context context){
        try(InputStream raw=context.getAssets().open("original_packages/kenney_isometric-buildings.zip");ZipInputStream zin=new ZipInputStream(raw)){
            ZipEntry e;
            while((e=zin.getNextEntry())!=null){
                if(e.isDirectory())continue;
                String n=e.getName().toLowerCase();
                // The package is a modular kit. Do NOT pick roof/wall fragments or arbitrary PNGs.
                if(!n.endsWith(".png")||!n.contains("buildingtile"))continue;
                int id=parseTileId(n);
                // Early entries are mostly isolated roof/floor pieces. Keep the building section.
                if(id<32)continue;
                byte[] data=readEntry(zin);
                Bitmap b=BitmapFactory.decodeByteArray(data,0,data.length);
                if(b!=null&&looksLikeBuilding(b))buildingArt.add(b);
            }
        }catch(Exception ignored){}
    }

    private int parseTileId(String name){
        int u=name.lastIndexOf('_'),d=name.lastIndexOf('.');
        if(u<0||d<u)return -1;
        try{return Integer.parseInt(name.substring(u+1,d));}catch(Exception e){return -1;}
    }

    private boolean looksLikeBuilding(Bitmap b){
        int w=b.getWidth(),h=b.getHeight();
        if(w<48||h<48)return false;
        int[] px=new int[w*h];b.getPixels(px,0,w,0,0,w,h);
        int minX=w,minY=h,maxX=-1,maxY=-1,count=0;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            int a=(px[y*w+x]>>>24)&255;
            if(a<40)continue;
            count++;minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);
        }
        if(maxX<0)return false;
        int bw=maxX-minX+1,bh=maxY-minY+1;
        return bw>=48&&bh>=48&&count>=900;
    }

    private byte[] readEntry(ZipInputStream z)throws Exception{byte[] buf=new byte[8192];java.io.ByteArrayOutputStream out=new java.io.ByteArrayOutputStream();int n;while((n=z.read(buf))>0)out.write(buf,0,n);return out.toByteArray();}

    private void buildLayout(){
        addRoad(300,900,5700,900,170,true);addRoad(300,2850,5700,2850,155,true);addRoad(300,4800,5700,4800,175,true);
        addRoad(1000,300,1000,5700,165,false);addRoad(3000,300,3000,5700,180,false);addRoad(5000,300,5000,5700,150,false);
        addRoad(420,1350,1000,1350,82,true);addRoad(1000,1500,1650,1500,78,true);addRoad(1650,1500,1650,900,78,false);addRoad(2050,1150,2050,2850,84,false);
        addRoad(3000,1250,3600,1250,78,true);addRoad(3600,1250,3600,900,78,false);addRoad(4050,1600,5000,1600,82,true);addRoad(4450,1600,4450,2850,80,false);
        addRoad(350,2300,1000,2300,78,true);addRoad(1000,2150,1450,2150,76,true);addRoad(1450,2150,1450,2850,76,false);addRoad(1750,2400,1750,2850,78,false);
        addRoad(2350,2850,2350,3550,82,false);addRoad(3000,2350,3650,2350,76,true);addRoad(3650,2350,3650,2850,76,false);addRoad(4050,2550,5000,2550,78,true);addRoad(5400,2200,5400,2850,78,false);
        addRoad(430,3650,1000,3650,82,true);addRoad(1000,3900,1550,3900,76,true);addRoad(1550,3900,1550,4800,76,false);addRoad(1900,4100,1900,4800,78,false);
        addRoad(2200,3500,3000,3500,82,true);addRoad(3000,3850,3650,3850,78,true);addRoad(3650,3850,3650,4800,78,false);addRoad(4100,3550,5000,3550,82,true);addRoad(4550,3550,4550,4800,78,false);addRoad(5250,3300,5250,4800,80,false);
        addRoad(450,5350,1000,5350,80,true);addRoad(1250,5200,1250,5700,76,false);addRoad(1750,5400,3000,5400,82,true);addRoad(3450,5200,3450,5700,76,false);addRoad(3800,5400,5000,5400,82,true);addRoad(5450,5100,5450,5700,76,false);

        // Buildings sit in clean rectangular city blocks instead of being randomly rotated/skewed.
        int[][] b={{380,390,430,280},{1320,400,430,290},{2080,390,500,300},{3220,400,480,310},{4180,380,450,300},{5220,380,360,330},
                {360,1030,450,300},{1260,1060,500,320},{2200,1040,470,310},{3220,1020,470,320},{4050,1040,440,300},{5220,1040,390,300},
                {350,1750,470,330},{1230,1680,420,300},{1760,1730,400,300},{2300,1700,470,310},{3190,1700,500,330},{3830,1720,430,300},{5130,1720,420,320},
                {360,3000,470,320},{1240,3040,440,290},{1750,3040,430,320},{2420,3040,430,300},{3190,3040,500,320},{3910,3050,420,300},{5120,3030,400,310},
                {360,3950,470,320},{1210,4030,470,310},{1790,4060,420,300},{2250,4000,480,320},{3190,4030,480,310},{3900,4010,440,300},{5150,4000,400,320},
                {360,4900,500,300},{1240,4930,440,320},{1800,4940,470,300},{2450,4920,450,310},{3190,4920,500,320},{3910,4930,440,300},{5140,4920,400,300},
                {420,5450,430,250},{1380,5450,420,250},{2200,5520,480,240},{3250,5480,450,250},{4030,5520,440,240},{5140,5450,380,250}};
        int i=0;for(int[] v:b)buildings.add(new Building(v[0],v[1],v[2],v[3],i++,0.8f));
        for(int n=0;n<130;n++){float x=220+random.nextFloat()*5560f,y=220+random.nextFloat()*5560f;if(!nearRoad(x,y,115)&&!nearBuilding(x,y,85))trees.add(new Tree(x,y,22+random.nextFloat()*20,n%4));}
        for(int n=0;n<420;n++){float x=random.nextFloat()*WORLD_SIZE,y=random.nextFloat()*WORLD_SIZE;if(!nearRoad(x,y,95))terrainMarks.add(new Mark(x,y,18+random.nextFloat()*70,n%5));}
    }
    private void addRoad(float x1,float y1,float x2,float y2,float width,boolean horizontal){if(horizontal)roads.add(new Road(Math.min(x1,x2),y1,Math.max(x1,x2),y2,width,true));else roads.add(new Road(x1,Math.min(y1,y2),x2,Math.max(y1,y2),width,false));}
    private boolean nearRoad(float x,float y,float pad){for(Road r:roads){if(r.horizontal){if(x>=r.x1-pad&&x<=r.x2+pad&&Math.abs(y-r.y1)<=r.width*.5f+pad)return true;}else if(y>=r.y1-pad&&y<=r.y2+pad&&Math.abs(x-r.x1)<=r.width*.5f+pad)return true;}return false;}
    private boolean nearBuilding(float x,float y,float pad){for(Building b:buildings)if(x>=b.x-pad&&x<=b.x+b.w+pad&&y>=b.y-pad&&y<=b.y+b.h+pad)return true;return false;}
    public boolean isBlocked(float x,float y,float radius){if(x<80||y<80||x>WORLD_SIZE-80||y>WORLD_SIZE-80)return true;for(Building b:buildings){float l=b.x-radius,r=b.x+b.w+radius,t=b.y-radius,bot=b.y+b.h+radius;if(x>l&&x<r&&y>t&&y<bot)return true;}return false;}

    public void draw(Canvas c,float playerX,float playerY,float scale,float viewW,float viewH,float hudH){p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(38,55,43));c.drawRect(0,0,viewW,viewH,p);float ox=viewW*.5f-playerX*scale,oy=hudH+(viewH-hudH)*.5f-playerY*scale;c.save();c.translate(ox,oy);drawGround(c,scale);drawRoads(c,scale);drawBehindDecor(c,playerY,scale);c.restore();}
    public void drawForeground(Canvas c,float playerX,float playerY,float scale,float viewW,float viewH,float hudH){float ox=viewW*.5f-playerX*scale,oy=hudH+(viewH-hudH)*.5f-playerY*scale;c.save();c.translate(ox,oy);for(Tree t:trees)if(t.y>playerY)drawTree(c,t,scale);for(Building b:buildings)if(b.y+b.h>playerY)drawBuilding(c,b,scale);c.restore();}
    private void drawGround(Canvas c,float s){
        p.setStyle(Paint.Style.FILL);p.setColor(0xFF7B684D);c.drawRect(0,0,WORLD_SIZE*s,WORLD_SIZE*s,p);
        for(int y=0;y<WORLD_SIZE;y+=180)for(int x=0;x<WORLD_SIZE;x+=180){p.setColor(((x/180+y/180)%2==0)?0x123F2F20:0x0FEEE0C0);c.drawRect(x*s,y*s,(x+178)*s,(y+178)*s,p);}
        for(int y=260;y<WORLD_SIZE;y+=620)for(int x=240;x<WORLD_SIZE;x+=620){p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2*s);p.setColor(0x2A3D2E22);c.drawCircle(x*s,y*s,28*s,p);c.drawCircle(x*s,y*s,12*s,p);c.drawLine((x-20)*s,y*s,(x+20)*s,y*s,p);c.drawLine(x*s,(y-20)*s,x*s,(y+20)*s,p);}
        p.setStyle(Paint.Style.FILL);
    }
    private void drawRoads(Canvas c,float s){
        for(Road r:roads){p.setStyle(Paint.Style.FILL);p.setColor(0xFF302E2A);
            if(r.horizontal)c.drawRect(r.x1*s,(r.y1-r.width*.5f)*s,r.x2*s,(r.y1+r.width*.5f)*s,p);else c.drawRect((r.x1-r.width*.5f)*s,r.y1*s,(r.x1+r.width*.5f)*s,r.y2*s,p);
            p.setColor(0xFF9A825B);if(r.horizontal){c.drawRect(r.x1*s,(r.y1-r.width*.5f)*s,r.x2*s,(r.y1-r.width*.43f)*s,p);c.drawRect(r.x1*s,(r.y1+r.width*.43f)*s,r.x2*s,(r.y1+r.width*.5f)*s,p);}
            else{c.drawRect((r.x1-r.width*.5f)*s,r.y1*s,(r.x1-r.width*.43f)*s,r.y2*s,p);c.drawRect((r.x1+r.width*.43f)*s,r.y1*s,(r.x1+r.width*.5f)*s,r.y2*s,p);}
            p.setColor(0xA8D7C78B);float len=r.horizontal?r.x2-r.x1:r.y2-r.y1;for(float t=55;t<len-35;t+=125){if(r.horizontal)c.drawRoundRect((r.x1+t)*s,(r.y1-3)*s,(r.x1+t+58)*s,(r.y1+3)*s,3*s,3*s,p);else c.drawRoundRect((r.x1-3)*s,(r.y1+t)*s,(r.x1+3)*s,(r.y1+t+58)*s,3*s,3*s,p);}}
        int[] ys={900,2850,4800},xs={1000,3000,5000};p.setColor(0x9CC9B984);for(int y:ys)for(int x:xs)for(int k=-3;k<=3;k++){c.drawRect((x+k*24-6)*s,(y-92)*s,(x+k*24+6)*s,(y-48)*s,p);c.drawRect((x-92)*s,(y+k*24-6)*s,(x-48)*s,(y+k*24+6)*s,p);}
    }
    private void drawBehindDecor(Canvas c,float playerY,float s){for(Tree t:trees)if(t.y<=playerY)drawTree(c,t,s);for(Building b:buildings)if(b.y+b.h<=playerY)drawBuilding(c,b,s);}

    private void drawBuilding(Canvas c,Building b,float s){
        float x=b.x*s,y=b.y*s,w=b.w*s,h=b.h*s,depth=22*s;int[] facade={0xFFB37D55,0xFFC09561,0xFF9F6D4D,0xFFD0A86C,0xFFA97955};int base=facade[b.kind%facade.length];
        p.setStyle(Paint.Style.FILL);p.setColor(0x50000000);c.drawRoundRect(x+18*s,y+h-8*s,x+w+30*s,y+h+24*s,12*s,12*s,p);
        p.setColor(base);c.drawRoundRect(x,y+depth,x+w,y+h,10*s,10*s,p);p.setColor(0xFF6D5540);c.drawRoundRect(x-8*s,y+depth-10*s,x+w+8*s,y+depth+8*s,6*s,6*s,p);p.setColor(0xFF8E6D4B);c.drawRect(x+10*s,y+2*s,x+w-10*s,y+depth,p);
        p.setColor(0xFFD6AE63);c.drawRect(x+10*s,y+h*.34f,x+w-10*s,y+h*.39f,p);
        for(float fx=x+28*s;fx<x+w-18*s;fx+=42*s){p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2*s);p.setColor(0xB06F4D35);c.drawCircle(fx,y+h*.365f,8*s,p);c.drawLine(fx-7*s,y+h*.365f,fx+7*s,y+h*.365f,p);c.drawLine(fx,y+h*.365f-7*s,fx,y+h*.365f+7*s,p);}
        int cols=Math.max(2,(int)(w/(92*s)));float gap=(w-48*s)/cols;for(int i=0;i<cols;i++){float wx=x+24*s+i*gap,wy=y+h*.48f;p.setStyle(Paint.Style.FILL);p.setColor(0xFF253C42);c.drawRoundRect(wx,wy,wx+30*s,wy+58*s,5*s,5*s,p);p.setColor(0xFFB98D4C);c.drawRect(wx-3*s,wy-4*s,wx+33*s,wy+3*s,p);c.drawRect(wx-3*s,wy+56*s,wx+33*s,wy+63*s,p);}
        float doorW=Math.min(62*s,w*.18f),doorX=x+w*.5f-doorW*.5f,doorY=y+h*.58f;p.setColor(0xFF40352E);c.drawRoundRect(doorX,doorY,doorX+doorW,y+h,9*s,9*s,p);p.setColor(0xFFD5B36B);c.drawRect(doorX-7*s,doorY-4*s,doorX+doorW+7*s,doorY+3*s,p);
        for(float cx:new float[]{x+18*s,x+w-18*s}){p.setColor(0xFFD0B27A);c.drawRoundRect(cx-5*s,doorY-5*s,cx+5*s,y+h-6*s,4*s,4*s,p);p.setColor(0xFFE0C58D);c.drawOval(cx-10*s,doorY-13*s,cx+10*s,doorY-2*s,p);}p.setStyle(Paint.Style.FILL);
    }
    private void drawTree(Canvas c,Tree t,float s){float x=t.x*s,y=t.y*s,r=t.r*s;p.setStyle(Paint.Style.FILL);p.setColor(0x6A4D3928);c.drawRect(x-4*s,y-2*s,x+4*s,y+35*s,p);int[] greens={0xFF365E39,0xFF3E6C40,0xFF2E5333,0xFF477445};p.setColor(greens[t.kind]);c.drawCircle(x,y-r*.25f,r,p);p.setColor(0xFF56814C);c.drawCircle(x-r*.35f,y-r*.42f,r*.55f,p);p.setColor(0xFF6A9554);c.drawCircle(x+r*.30f,y-r*.38f,r*.45f,p);p.setColor(0x32000000);c.drawOval(x-r*.9f,y+12*s,x+r*.9f,y+28*s,p);}

    static final class Road{final float x1,y1,x2,y2,width;final boolean horizontal;Road(float x1,float y1,float x2,float y2,float width,boolean horizontal){this.x1=x1;this.y1=y1;this.x2=x2;this.y2=y2;this.width=width;this.horizontal=horizontal;}}
    static final class Building{final float x,y,w,h,depth;final int kind;Building(float x,float y,float w,float h,int kind,float depth){this.x=x;this.y=y;this.w=w;this.h=h;this.kind=kind;this.depth=depth;}}
    static final class Tree{final float x,y,r;final int kind;Tree(float x,float y,float r,int kind){this.x=x;this.y=y;this.r=r;this.kind=kind;}}
    static final class Mark{final float x,y,size;final int kind;Mark(float x,float y,float size,int kind){this.x=x;this.y=y;this.size=size;this.kind=kind;}}
}
