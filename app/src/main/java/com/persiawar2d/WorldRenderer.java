package com.persiawar2d;

import android.content.Context;
import android.graphics.*;
import java.io.InputStream;

/**
 * 6000x6000 square world. Only the camera window around the player is shown.
 * The map is intentionally non-repeating and uses small 2.5D Achaemenid-style
 * houses, roads and trees so one building can never fill the whole screen.
 */
public final class WorldRenderer {
    public static final float WORLD_SIZE=6000f;
    private static final int MAP_BITMAP=1536;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final Bitmap worldMap;
    private final Rect src=new Rect();
    private final RectF dst=new RectF();

    public WorldRenderer(Context context){
        Bitmap loaded=loadMap(context);
        worldMap=loaded!=null?loaded:generateMap();
        src.set(0,0,worldMap.getWidth(),worldMap.getHeight());
    }

    private Bitmap loadMap(Context context){
        String[] names={"world_map_square_2048.png","references/world_map_square_2048.png"};
        for(String name:names){
            try(InputStream in=context.getAssets().open(name)){
                BitmapFactory.Options o=new BitmapFactory.Options();
                o.inScaled=false;
                Bitmap b=BitmapFactory.decodeStream(in,null,o);
                if(b!=null && b.getWidth()>512 && b.getHeight()>512)return b;
            }catch(Exception ignored){}
        }
        return null;
    }

    private Bitmap generateMap(){
        final int n=MAP_BITMAP;
        Bitmap b=Bitmap.createBitmap(n,n,Bitmap.Config.ARGB_8888);
        Canvas c=new Canvas(b);
        float k=n/1024f;
        c.scale(k,k);
        p.setStyle(Paint.Style.FILL);
        p.setAlpha(255);

        p.setColor(Color.rgb(104,143,73)); c.drawRect(0,0,1024,1024,p);
        p.setColor(Color.rgb(112,150,78));
        for(int i=-1024;i<2048;i+=230){
            Path band=new Path();
            band.moveTo(i,0); band.lineTo(i+120,0);
            band.lineTo(i+40,1024); band.lineTo(i-80,1024); band.close();
            c.drawPath(band,p);
        }
        p.setColor(Color.argb(26,235,220,170));
        for(int x=90;x<1024;x+=170) for(int y=90;y<1024;y+=180){
            Path s=new Path(); s.moveTo(x,y-12);s.lineTo(x+5,y-5);s.lineTo(x+16,y);s.lineTo(x+5,y+5);s.lineTo(x,y+16);s.lineTo(x-5,y+5);s.lineTo(x-16,y);s.lineTo(x-5,y-5);s.close();c.drawPath(s,p);
        }

        p.setColor(Color.rgb(176,169,144));
        for(int x:new int[]{250,650,910}) c.drawRect(x-14,0,x+14,1024,p);
        for(int y:new int[]{250,620,860}) c.drawRect(0,y-14,1024,y+14,p);
        p.setColor(Color.rgb(62,64,60));
        for(int x:new int[]{250,650,910}) c.drawRect(x-8,0,x+8,1024,p);
        for(int y:new int[]{250,620,860}) c.drawRect(0,y-8,1024,y+8,p);
        p.setColor(Color.rgb(91,82,66));
        c.drawRect(0,246,1024,254,p); c.drawRect(646,0,654,1024,p);

        int[][] houses={
            {70,70,135,110},{355,65,430,110},{730,70,815,120},
            {70,330,145,375},{315,315,395,360},{540,335,620,380},{790,330,875,375},
            {70,680,145,725},{320,675,400,720},{560,690,640,735},{800,675,885,720},
            {440,470,520,515},{930,470,990,505},{175,470,235,505}
        };
        for(int[] h:houses)drawHouse(c,h[0],h[1],h[2],h[3]);

        int[][] trees={{205,190},{475,190},{720,190},{945,190},{205,520},{710,520},{945,520},{220,810},{505,810},{735,815},{945,815}};
        for(int[] t:trees)drawTree(c,t[0],t[1]);
        return b;
    }

    private void drawHouse(Canvas c,int l,int t,int r,int b){
        p.setStyle(Paint.Style.FILL);
        p.setColor(Color.rgb(103,76,57));
        Path roof=new Path(); roof.moveTo(l-5,t+12); roof.lineTo((l+r)/2f,t-8); roof.lineTo(r+5,t+12); roof.close(); c.drawPath(roof,p);
        p.setColor(Color.rgb(198,172,126)); c.drawRect(l,t+12,r,b,p);
        p.setColor(Color.rgb(232,211,158)); c.drawRect(l+7,t+22,l+14,t+38,p); c.drawRect(r-14,t+22,r-7,t+38,p);
        p.setColor(Color.rgb(75,52,41)); c.drawRect((l+r)/2f-9,b-25,(l+r)/2f+9,b,p);
        p.setColor(Color.rgb(229,192,104)); c.drawRect(l+18,t+15,l+42,t+18,p);
    }

    private void drawTree(Canvas c,int x,int y){
        p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(92,69,46));c.drawRect(x-3,y+12,x+3,y+22,p);
        p.setColor(Color.rgb(44,96,43));c.drawCircle(x,y,13,p);
        p.setColor(Color.rgb(83,139,62));c.drawCircle(x-4,y-4,9,p);
    }

    public void draw(Canvas c,float playerX,float playerY,float scale,float viewW,float viewH,float hudH){
        c.drawColor(Color.rgb(104,143,73));
        float ox=viewW*.5f-playerX*scale;
        float oy=hudH+(viewH-hudH)*.5f-playerY*scale;
        dst.set(ox,oy,ox+WORLD_SIZE*scale,oy+WORLD_SIZE*scale);
        p.setAlpha(255);c.drawBitmap(worldMap,src,dst,p);
    }

    public boolean ready(){return worldMap!=null;}
}
