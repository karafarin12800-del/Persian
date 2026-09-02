package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import com.persiawar2d.world.WorldMap;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/** Visual-only renderer for the shared 6000x6000 gameplay map. */
public final class WorldRenderer {
    public static final float WORLD_SIZE=WorldMap.SIZE;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final WorldMap map;
    private final ArrayList<Bitmap> buildingArt=new ArrayList<>();

    public WorldRenderer(Context context){map=new WorldMap();loadBuildingArt(context);}
    public WorldMap map(){return map;}

    private void loadBuildingArt(Context context){
        try(InputStream raw=context.getAssets().open("original_packages/kenney_isometric-buildings.zip");ZipInputStream zin=new ZipInputStream(raw)){
            ZipEntry e;while((e=zin.getNextEntry())!=null){if(e.isDirectory())continue;String n=e.getName().toLowerCase();if(!n.endsWith(".png")||!n.contains("buildingtile"))continue;byte[] data=read(zin);Bitmap b=BitmapFactory.decodeByteArray(data,0,data.length);if(b!=null&&b.getWidth()>=48&&b.getHeight()>=48)buildingArt.add(b);}
        }catch(Exception ignored){}
    }
    private byte[] read(ZipInputStream z)throws Exception{byte[] buf=new byte[8192];java.io.ByteArrayOutputStream out=new java.io.ByteArrayOutputStream();int n;while((n=z.read(buf))>0)out.write(buf,0,n);return out.toByteArray();}

    public void drawBackground(Canvas c,float playerX,float playerY,float scale,float viewW,float viewH,float hudH,float pitch){
        p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(116,105,76));c.drawRect(0,hudH,viewW,viewH,p);
        float cx=viewW*.5f,cy=hudH+(viewH-hudH)*.5f;
        c.save();c.translate(cx,cy);c.scale(scale,scale*pitch);c.translate(-playerX,-playerY);
        drawGround(c);drawRoads(c);drawDecor(c,playerY,false);c.restore();
    }
    public void drawForeground(Canvas c,float playerX,float playerY,float scale,float viewW,float viewH,float hudH,float pitch){
        float cx=viewW*.5f,cy=hudH+(viewH-hudH)*.5f;
        c.save();c.translate(cx,cy);c.scale(scale,scale*pitch);c.translate(-playerX,-playerY);drawDecor(c,playerY,true);c.restore();
    }

    private void drawGround(Canvas c){
        p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(126,112,78));c.drawRect(0,0,WorldMap.SIZE,WorldMap.SIZE,p);
        p.setColor(0x173F5737);for(WorldMap.Prop g:map.bushes())c.drawCircle(g.x,g.y,g.size*.52f,p);
        p.setColor(0x315C743E);p.setStrokeWidth(2.2f);for(WorldMap.Prop g:map.grass()){c.drawLine(g.x,g.y,g.x+2,g.y-g.size,p);c.drawLine(g.x+4,g.y,g.x+7,g.y-g.size*.72f,p);}
    }
    private void drawRoads(Canvas c){
        for(WorldMap.Road r:map.roads()){
            p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(60,57,51));
            if(r.horizontal)c.drawRect(r.x1,r.y1-r.width*.5f,r.x2,r.y1+r.width*.5f,p);else c.drawRect(r.x1-r.width*.5f,r.y1,r.x1+r.width*.5f,r.y2,p);
            p.setColor(Color.rgb(99,91,73));
            if(r.horizontal){c.drawRect(r.x1,r.y1-r.width*.5f,r.x2,r.y1-r.width*.39f,p);c.drawRect(r.x1,r.y1+r.width*.39f,r.x2,r.y1+r.width*.5f,p);}
            else{c.drawRect(r.x1-r.width*.5f,r.y1,r.x1-r.width*.39f,r.y2,p);c.drawRect(r.x1+r.width*.39f,r.y1,r.x1+r.width*.5f,r.y2,p);}
            p.setColor(0x829A916F);for(float t=70;t<(r.horizontal?r.x2-r.x1:r.y2-r.y1)-30;t+=130){if(r.horizontal)c.drawRect(r.x1+t,r.y1-4,r.x1+t+65,r.y1+4,p);else c.drawRect(r.x1-4,r.y1+t,r.x1+4,r.y1+t+65,p);}
        }
    }
    private void drawDecor(Canvas c,float playerY,boolean front){
        for(WorldMap.Building b:map.buildings())if((b.y+b.h*.5f>playerY)==front)drawBuilding(c,b);
        for(WorldMap.Vehicle v:map.vehicles())if((v.y>playerY)==front)drawCar(c,v);
        for(WorldMap.Fence f:map.fences())if((Math.max(f.y1,f.y2)>playerY)==front)drawFence(c,f);
        for(WorldMap.Prop b:map.bushes())if((b.y>playerY)==front)drawBush(c,b);
        for(WorldMap.Prop t:map.trees())if((t.y>playerY)==front)drawTree(c,t);
    }
    private void drawBuilding(Canvas c,WorldMap.Building b){
        float cx=b.x+b.w*.5f,base=b.y+b.h*.96f;p.setColor(0x35000000);c.drawOval(cx-b.w*.42f,base-6,cx+b.w*.42f,base+16,p);
        if(!buildingArt.isEmpty()){
            Bitmap art=buildingArt.get(b.style%buildingArt.size());float dw=b.w*.94f,dh=dw*art.getHeight()/(float)Math.max(1,art.getWidth());float maxH=b.h*1.65f;if(dh>maxH){dh=maxH;dw=dh*art.getWidth()/(float)Math.max(1,art.getHeight());}
            c.drawBitmap(art,null,new RectF(cx-dw*.5f,base-dh,cx+dw*.5f,base),p);return;
        }
        p.setColor(Color.rgb(94,77,55));c.drawRect(b.x,b.y,b.x+b.w,b.y+b.h,p);p.setColor(Color.rgb(52,42,31));c.drawRect(b.x-6,b.y-10,b.x+b.w+6,b.y+5,p);p.setColor(0xFFCCBE77);for(int yy=25;yy<b.h-20;yy+=52)for(int xx=25;xx<b.w-20;xx+=58)c.drawRect(b.x+xx,b.y+yy,b.x+xx+22,b.y+yy+18,p);
    }
    private void drawTree(Canvas c,WorldMap.Prop t){float x=t.x,y=t.y,r=t.size;p.setColor(0x6D4B3527);c.drawRect(x-4,y-4,x+4,y+24,p);int[] g={0xFF315D36,0xFF3F713E,0xFF345F36,0xFF4A7842};p.setColor(g[t.kind%4]);c.drawCircle(x,y-r*.2f,r,p);p.setColor(0xFF5D8A4D);c.drawCircle(x-r*.36f,y-r*.4f,r*.56f,p);p.setColor(0xFF71995A);c.drawCircle(x+r*.3f,y-r*.35f,r*.46f,p);p.setColor(0x26000000);c.drawOval(x-r*.9f,y+12,x+r*.9f,y+27,p);}
    private void drawBush(Canvas c,WorldMap.Prop b){p.setColor(b.kind%3==0?0xFF456B3A:b.kind%3==1?0xFF557B43:0xFF3E6335);c.drawCircle(b.x,b.y,b.size,p);c.drawCircle(b.x-b.size*.45f,b.y+2,b.size*.65f,p);c.drawCircle(b.x+b.size*.4f,b.y+2,b.size*.6f,p);}
    private void drawCar(Canvas c,WorldMap.Vehicle v){c.save();c.rotate(v.angle,v.x,v.y);p.setColor(0x33000000);c.drawRoundRect(v.x-v.w*.5f+5,v.y-v.h*.5f+7,v.x+v.w*.5f+5,v.y+v.h*.5f+7,10,10,p);p.setColor(v.angle==0?0xFF6C4332:0xFF35596A);c.drawRoundRect(v.x-v.w*.5f,v.y-v.h*.5f,v.x+v.w*.5f,v.y+v.h*.5f,10,10,p);p.setColor(0xFF19242A);c.drawRoundRect(v.x-v.w*.18f,v.y-v.h*.30f,v.x+v.w*.18f,v.y+v.h*.30f,5,5,p);p.setColor(Color.DKGRAY);c.drawCircle(v.x-v.w*.3f,v.y-v.h*.5f,7,p);c.drawCircle(v.x+v.w*.3f,v.y-v.h*.5f,7,p);c.drawCircle(v.x-v.w*.3f,v.y+v.h*.5f,7,p);c.drawCircle(v.x+v.w*.3f,v.y+v.h*.5f,7,p);c.restore();}
    private void drawFence(Canvas c,WorldMap.Fence f){p.setColor(0xFF6C5C43);p.setStrokeWidth(8);c.drawLine(f.x1,f.y1,f.x2,f.y2,p);p.setColor(0xFFB5A27A);p.setStrokeWidth(3);float dx=f.x2-f.x1,dy=f.y2-f.y1,len=(float)Math.hypot(dx,dy);int n=Math.max(2,(int)(len/28));for(int i=0;i<=n;i++){float t=i/(float)n;float x=f.x1+dx*t,y=f.y1+dy*t;c.drawLine(x,y-10,x,y+10,p);}}
}
