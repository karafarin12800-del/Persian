package com.persiawar2d;

import android.content.Context;
import android.graphics.*;
import android.graphics.drawable.Drawable;
import java.io.InputStream;
import java.util.*;

/** Native Android 2.5D renderer for the 6000x6000 square world. */
public final class WorldRenderer {
    public static final float WORLD_SIZE=6000f;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Drawable background,ground;
    private final Bitmap worldTexture;
    private final Rect src=new Rect();
    private final RectF dst=new RectF();
    private final ArrayList<Building> buildings=new ArrayList<>();
    private final ArrayList<Shrub> shrubs=new ArrayList<>();
    private final ArrayList<Road> roads=new ArrayList<>();
    private final Random random=new Random(20260816L);

    public WorldRenderer(Context context){
        background=context.getDrawable(R.drawable.achaemenid_background); ground=context.getDrawable(R.drawable.persia_ground);
        Bitmap b=null;
        try(InputStream in=context.getAssets().open("references/world_texture_ref.jpg")){BitmapFactory.Options o=new BitmapFactory.Options();o.inScaled=false;b=BitmapFactory.decodeStream(in,null,o);}catch(Exception ignored){}
        worldTexture=b;if(b!=null)src.set(0,0,b.getWidth(),b.getHeight());buildLayout();
    }
    private void buildLayout(){
        roads.add(new Road(300,980,5700,980,150));roads.add(new Road(420,2850,5600,2850,125));roads.add(new Road(700,4650,5300,4650,140));roads.add(new Road(1200,300,1200,5700,130));roads.add(new Road(3380,250,3380,5750,150));roads.add(new Road(5050,600,5050,5400,120));roads.add(new Road(600,1600,2300,1600,70));roads.add(new Road(1900,1100,1900,2700,62));roads.add(new Road(2580,2950,2580,4500,64));roads.add(new Road(3650,1650,4900,1650,68));roads.add(new Road(4200,3150,4200,5050,70));roads.add(new Road(850,3900,2500,3900,66));roads.add(new Road(4700,900,4700,2500,60));
        int[][] spots={{430,520,360,280},{1530,520,430,300},{2250,520,470,320},{3900,520,500,330},{4550,620,380,260},{520,2050,420,320},{1500,2050,500,340},{2200,1900,380,280},{3600,2050,520,320},{4450,2100,420,310},{5200,1900,430,330},{500,3350,450,310},{1500,3250,420,300},{2850,3500,520,340},{3650,3500,450,290},{4700,3550,520,330},{5200,4200,390,300},{650,5000,500,300},{1750,5000,430,330},{2900,5000,460,310},{3850,5100,500,300},{4750,5100,420,330}};
        int i=0;for(int[] s:spots){buildings.add(new Building(s[0],s[1],s[2],s[3],i%6));if(i%3!=1)buildings.add(new Building(s[0]+s[2]+90,s[1]+35,210+(i%3)*55,190+(i%4)*30,(i+2)%6));i++;}
        for(int n=0;n<95;n++){float x=260+random.nextFloat()*5480,y=300+random.nextFloat()*5400;if(!nearRoad(x,y,95))shrubs.add(new Shrub(x,y,18+random.nextFloat()*22));}
    }
    private boolean nearRoad(float x,float y,float pad){for(Road r:roads){if(r.x1==r.x2&&Math.abs(x-r.x1)<r.w/2+pad&&y>=Math.min(r.y1,r.y2)-pad&&y<=Math.max(r.y1,r.y2)+pad)return true;if(r.y1==r.y2&&Math.abs(y-r.y1)<r.w/2+pad&&x>=Math.min(r.x1,r.x2)-pad&&x<=Math.max(r.x1,r.x2)+pad)return true;}return false;}
    public void draw(Canvas c,float playerX,float playerY,float cameraScale,float viewW,float viewH,float hudH){
        p.setStyle(Paint.Style.FILL);p.setAlpha(255);p.setColor(Color.rgb(38,58,47));c.drawRect(0,0,viewW,viewH,p);float s=cameraScale,ox=viewW/2f-playerX*s,oy=(viewH+hudH)/2f-playerY*s;c.save();c.translate(ox,oy);drawReferenceWorld(c,s);drawRoads(c,s);for(Shrub sh:shrubs)drawShrub(c,sh,playerX,playerY,s);for(Building b:buildings)drawBuilding(c,b,playerX,playerY,s);c.restore();
    }
    private void drawReferenceWorld(Canvas c,float s){
        if(worldTexture!=null){dst.set(0,0,WORLD_SIZE*s,WORLD_SIZE*s);c.drawBitmap(worldTexture,src,dst,p);return;}
        if(background!=null){background.setBounds(0,0,Math.round(WORLD_SIZE*s),Math.round(WORLD_SIZE*s));background.setAlpha(255);background.draw(c);}if(ground!=null){ground.setAlpha(70);int tile=Math.max(320,Math.round(620*s));for(int y=0;y<WORLD_SIZE*s;y+=tile)for(int x=0;x<WORLD_SIZE*s;x+=tile){ground.setBounds(x,y,x+tile,y+tile);ground.draw(c);}ground.setAlpha(255);}
    }
    private void drawRoads(Canvas c,float s){for(Road r:roads){p.setAlpha(80);p.setColor(Color.rgb(50,50,45));if(r.x1==r.x2)c.drawRect((r.x1-r.w/2)*s,Math.min(r.y1,r.y2)*s,(r.x1+r.w/2)*s,Math.max(r.y1,r.y2)*s,p);else c.drawRect(Math.min(r.x1,r.x2)*s,(r.y1-r.w/2)*s,Math.max(r.x1,r.x2)*s,(r.y1+r.w/2)*s,p);p.setAlpha(130);p.setColor(Color.rgb(235,220,170));p.setStrokeWidth(Math.max(2,r.w*.025f*s));if(r.x1==r.x2)c.drawLine(r.x1*s,Math.min(r.y1,r.y2)*s,r.x1*s,Math.max(r.y1,r.y2)*s,p);else c.drawLine(Math.min(r.x1,r.x2)*s,r.y1*s,Math.max(r.x1,r.x2)*s,r.y1*s,p);}p.setAlpha(255);}
    private void drawBuilding(Canvas c,Building b,float px,float py,float s){float fade=blocked(b,px,py)?0.24f:0.92f;p.setAlpha((int)(255*fade));float x=b.x*s,y=b.y*s,w=b.w*s,lift=Math.max(18*s,b.h*.22f*s);int[] walls={Color.rgb(157,123,82),Color.rgb(139,103,70),Color.rgb(174,139,94),Color.rgb(121,91,65),Color.rgb(188,151,101),Color.rgb(146,112,76)};p.setColor(walls[b.kind%walls.length]);c.drawRect(x,y-lift,x+w,y,p);p.setColor(Color.rgb(92,65,46));Path roof=new Path();roof.moveTo(x-10*s,y-lift);roof.lineTo(x+w*.5f,y-lift-34*s);roof.lineTo(x+w+10*s,y-lift);roof.close();c.drawPath(roof,p);p.setColor(Color.rgb(219,184,119));int cols=Math.max(2,(int)(b.w/95));for(int j=0;j<cols;j++){float wx=x+18*s+j*(w-36*s)/Math.max(1,cols-1);c.drawRect(wx,y-lift+30*s,wx+22*s,y-lift+70*s,p);}p.setColor(Color.rgb(72,52,39));c.drawRect(x+w*.42f,y-55*s,x+w*.58f,y,p);p.setAlpha(255);}
    private boolean blocked(Building b,float px,float py){float bx=b.x+b.w*.5f,by=b.y+b.h*.5f,camX=px,camY=py-700,dx=px-camX,dy=py-camY,len2=dx*dx+dy*dy;if(len2<1)return false;float t=((bx-camX)*dx+(by-camY)*dy)/len2;if(t<0||t>1)return false;float cx=camX+t*dx,cy=camY+t*dy;return cx>b.x-30&&cx<b.x+b.w+30&&cy>b.y-30&&cy<b.y+b.h+30&&Math.abs(cy-by)<b.h*.9f;}
    private void drawShrub(Canvas c,Shrub sh,float px,float py,float s){float fade=distance(sh.x,sh.y,px,py)<420&&blockedPoint(sh.x,sh.y,px,py)?0.22f:0.9f;p.setAlpha((int)(255*fade));float x=sh.x*s,y=sh.y*s,r=sh.r*s;p.setColor(Color.rgb(49,94,53));c.drawCircle(x,y,r,p);p.setColor(Color.rgb(77,126,66));c.drawCircle(x-r*.35f,y-r*.25f,r*.65f,p);p.setAlpha(255);}
    private boolean blockedPoint(float x,float y,float px,float py){return Math.abs(x-px)<80&&y<py;}
    private float distance(float a,float b,float c,float d){return(float)Math.hypot(a-c,b-d);}
    static final class Building{final float x,y,w,h;final int kind;Building(float x,float y,float w,float h,int kind){this.x=x;this.y=y;this.w=w;this.h=h;this.kind=kind;}}
    static final class Shrub{final float x,y,r;Shrub(float x,float y,float r){this.x=x;this.y=y;this.r=r;}}
    static final class Road{final float x1,y1,x2,y2,w;Road(float x1,float y1,float x2,float y2,float w){this.x1=x1;this.y1=y1;this.x2=x2;this.y2=y2;this.w=w;}}
}
