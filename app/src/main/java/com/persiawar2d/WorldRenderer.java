package com.persiawar2d;

import android.content.Context;
import android.graphics.*;
import java.io.InputStream;

/**
 * 6000x6000 square world. The camera is centered on the player; only the
 * nearby portion of the square is rendered. If the full raster map asset is
 * present it is used directly. The fallback is generated from the same
 * Achaemenid/cartoon reference palette so the APK never opens on a blank map.
 */
public final class WorldRenderer {
    public static final float WORLD_SIZE=6000f;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final Bitmap worldMap;
    private final Rect src=new Rect();
    private final RectF dst=new RectF();
    private final RectF[] blockers={r(430,520,900,900),r(1530,520,2050,900),r(2250,520,2800,940),r(3900,520,4460,980),r(4550,620,5000,930),r(520,2050,1050,2440),r(1500,2050,2100,2480),r(2200,1900,2700,2320),r(3600,2050,4250,2450),r(4450,2100,5000,2500),r(5200,1900,5700,2400),r(500,3350,1050,3720),r(1500,3250,2050,3620),r(2850,3500,3500,3920),r(3650,3500,4200,3900),r(4700,3550,5300,3970),r(5200,4200,5700,4570),r(650,5000,1250,5400),r(1750,5000,2300,5420),r(2900,5000,3500,5400),r(3850,5100,4450,5450),r(4750,5100,5300,5470)};

    public WorldRenderer(Context context){
        Bitmap b=loadMap(context);worldMap=b!=null?b:generateReferenceStyleMap();src.set(0,0,worldMap.getWidth(),worldMap.getHeight());
    }
    private Bitmap loadMap(Context context){
        String[] names={"world_map_square_2048.png","references/world_map_square_2048.png"};
        for(String name:names)try(InputStream in=context.getAssets().open(name)){BitmapFactory.Options o=new BitmapFactory.Options();o.inScaled=false;Bitmap b=BitmapFactory.decodeStream(in,null,o);if(b!=null)return b;}catch(Exception ignored){}
        return null;
    }
    private Bitmap generateReferenceStyleMap(){
        final int n=1536;final float k=n/1024f;Bitmap b=Bitmap.createBitmap(n,n,Bitmap.Config.ARGB_8888);Canvas c=new Canvas(b);c.scale(k,k);
        p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(112,150,78));c.drawRect(0,0,1024,1024,p);
        p.setColor(Color.rgb(104,143,73));for(int i=-1024;i<2048;i+=210)c.drawRect(i,0,i+90,1024,p);
        p.setColor(Color.rgb(122,157,82));for(int i=-900;i<1900;i+=360)c.drawRect(i,0,i+35,1024,p);
        p.setColor(Color.rgb(176,169,144));for(int x:new int[]{250,650,910})c.drawRect(x-10,0,x+10,1024,p);for(int y:new int[]{250,620,860})c.drawRect(0,y-10,1024,y+10,p);
        p.setColor(Color.rgb(64,68,69));for(int x:new int[]{250,650,910})c.drawRect(x-7,0,x+7,1024,p);for(int y:new int[]{250,620,860})c.drawRect(0,y-7,1024,y+7,p);
        int[][] houses={{70,70,190,145},{330,55,470,145},{700,60,900,160},{65,340,205,430},{300,320,455,415},{535,330,690,420},{760,345,925,435},{75,675,210,760},{310,670,470,765},{545,690,705,780},{770,680,925,770},{410,475,570,555}};for(int[] q:houses)drawHouse(c,q[0],q[1],q[2],q[3]);
        int[][] trees={{220,205},{480,205},{720,215},{930,225},{220,520},{700,525},{940,535},{245,820},{500,815},{720,820}};for(int[] t:trees)drawTree(c,t[0],t[1]);
        return b;
    }
    private void drawHouse(Canvas c,int l,int t,int rr,int bb){p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(105,78,58));Path roof=new Path();roof.moveTo(l-8,t+20);roof.lineTo((l+rr)/2f,t-12);roof.lineTo(rr+8,t+20);roof.close();c.drawPath(roof,p);p.setColor(Color.rgb(208,190,150));c.drawRect(l,t+20,rr,bb,p);p.setColor(Color.rgb(222,210,167));c.drawRect(l+10,t+42,l+25,t+67,p);c.drawRect(rr-25,t+42,rr-10,t+67,p);p.setColor(Color.rgb(76,53,42));c.drawRect((l+rr)/2f-20,bb-55,(l+rr)/2f+20,bb,p);p.setColor(Color.rgb(236,207,126));c.drawRect(l+30,t+26,l+70,t+32,p);}
    private void drawTree(Canvas c,int x,int y){p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(91,69,48));c.drawRect(x-5,y+15,x+5,y+35,p);p.setColor(Color.rgb(49,104,48));c.drawCircle(x,y,23,p);p.setColor(Color.rgb(83,141,65));c.drawCircle(x-7,y-7,16,p);}
    public void draw(Canvas c,float playerX,float playerY,float scale,float viewW,float viewH,float hudH){c.drawColor(Color.rgb(112,150,78));float ox=viewW*.5f-playerX*scale,oy=hudH+(viewH-hudH)*.5f-playerY*scale;dst.set(ox,oy,ox+WORLD_SIZE*scale,oy+WORLD_SIZE*scale);p.setAlpha(255);c.drawBitmap(worldMap,src,dst,p);drawBuildingFade(c,playerX,playerY,scale,ox,oy);}
    private void drawBuildingFade(Canvas c,float px,float py,float scale,float ox,float oy){float camX=px,camY=py-900,dx=px-camX,dy=py-camY,len2=Math.max(1,dx*dx+dy*dy);for(RectF b:blockers){float bx=b.centerX(),by=b.centerY(),t=((bx-camX)*dx+(by-camY)*dy)/len2;if(t<0||t>1)continue;float cx=camX+t*dx,cy=camY+t*dy;if(cx>b.left-45&&cx<b.right+45&&cy>b.top-45&&cy<b.bottom+45){RectF s=new RectF(ox+(b.left-18)*scale,oy+(b.top-18)*scale,ox+(b.right+18)*scale,oy+(b.bottom+18)*scale);p.setStyle(Paint.Style.FILL);p.setColor(Color.argb(155,112,150,78));c.drawRoundRect(s,18*scale,18*scale,p);}}}
    private static RectF r(float l,float t,float rr,float bb){return new RectF(l,t,rr,bb);}
    public boolean ready(){return worldMap!=null;}
}
