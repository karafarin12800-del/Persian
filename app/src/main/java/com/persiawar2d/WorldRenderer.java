package com.persiawar2d;

import android.content.Context;
import android.graphics.*;
import android.graphics.drawable.Drawable;
import java.util.*;

/**
 * Large 2.5D game-world renderer using only the project's real assets plus
 * vector geometry for terrain/road structure. No replacement bitmap art is generated.
 */
public final class WorldRenderer {
    public static final float WORLD_SIZE=9600f;
    private static final float ROAD_MAIN=150f, ROAD_SECONDARY=96f;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final Drawable background,ground;
    private final KenneyEnvironment assets;
    private final ArrayList<Road> roads=new ArrayList<>();
    private final ArrayList<Building> buildings=new ArrayList<>();
    private final ArrayList<Nature> nature=new ArrayList<>();
    private final Random random=new Random(20260817L);
    private float lastW,lastH,lastHud;

    public WorldRenderer(Context c){background=c.getDrawable(R.drawable.achaemenid_background);ground=c.getDrawable(R.drawable.persia_ground);assets=new KenneyEnvironment();assets.load(c);buildCity();}

    private void buildCity(){
        roads.add(new Road(450,1450,9150,1450,ROAD_MAIN));roads.add(new Road(450,4800,9150,4800,ROAD_MAIN));roads.add(new Road(450,8150,9150,8150,ROAD_MAIN));
        roads.add(new Road(1550,450,1550,9150,ROAD_MAIN));roads.add(new Road(4800,450,4800,9150,ROAD_MAIN));roads.add(new Road(8050,450,8050,9150,ROAD_MAIN));
        for(int y:new int[]{2350,3600,6000,7050})roads.add(new Road(650,y,8950,y,ROAD_SECONDARY));
        for(int x:new int[]{2700,3900,6000,7000})roads.add(new Road(x,650,x,8950,ROAD_SECONDARY));
        roads.add(new Road(700,700,3150,3150,110));roads.add(new Road(8900,700,6450,3150,110));roads.add(new Road(700,8900,3150,6450,110));roads.add(new Road(8900,8900,6450,6450,110));
        buildDistricts();scatterNature();
    }

    private void buildDistricts(){
        int[][] residential={{720,720,0},{2050,720,2},{2920,720,8},{5300,720,12},{6250,720,16},{8350,720,20},{720,2650,24},{2050,2650,28},{2920,2650,32},{5300,2650,36},{6250,2650,40},{8350,2650,44},{720,6250,48},{2050,6250,52},{2920,6250,56},{5300,6250,60},{6250,6250,64},{8350,6250,68},{720,8350,72},{2050,8350,76},{2920,8350,80},{5300,8350,84},{6250,8350,88},{8350,8350,92}};
        for(int[] s:residential)addBlock(s[0],s[1],3+(s[2]%3),1f);
        addBlock(3600,1750,5,1.12f);addBlock(5200,1750,4,1.08f);addBlock(3600,6100,5,1.10f);addBlock(5200,6100,4,1.12f);addBlock(7200,3600,5,1.12f);addBlock(7200,6000,4,1.10f);
        addPlaza(4800,4800,900);addTower(4800,4800,6,1.65f,122);addTower(4300,4450,4,1.20f,30);addTower(5300,4450,4,1.20f,99);addTower(4300,5150,4,1.20f,113);addTower(5300,5150,4,1.20f,1);
        addBlock(3650,4050,6,1.25f);addBlock(6000,4050,6,1.25f);addBlock(3650,5550,5,1.18f);addBlock(6000,5550,5,1.18f);
    }

    private void addBlock(float cx,float cy,int count,float scale){float spacing=230f*scale;for(int i=0;i<count;i++){float ox=((i%3)-1)*spacing,oy=((i/3)-.5f)*spacing;int kind=chooseBuilding(i+Math.round(cx)+Math.round(cy));int floors=1+Math.floorMod(i+(int)cx,3);float w=(160+(i%2)*28)*scale;buildings.add(new Building(cx+ox,cy+oy,w,floors,kind,false));}}
    private void addTower(float x,float y,int floors,float scale,int kind){buildings.add(new Building(x,y,190f*scale,floors,kind%129,true));}
    private void addPlaza(float cx,float cy,float r){roads.add(new Road(cx-r,cy-r,cx+r,cy-r,62));roads.add(new Road(cx-r,cy+r,cx+r,cy+r,62));roads.add(new Road(cx-r,cy-r,cx-r,cy+r,62));roads.add(new Road(cx+r,cy-r,cx+r,cy+r,62));}
    private int chooseBuilding(int seed){int[] a={0,1,2,3,4,9,10,14,17,18,20,21,23,24,25,30,31,32,33,34,36,40,41,43,44,45,49,92,99,100,101,106,107,108,109,113,114,115,116,117,122,123,124,125};return a[Math.floorMod(seed,a.length)];}

    private void scatterNature(){
        for(int i=0;i<360;i++){float x=300+random.nextFloat()*(WORLD_SIZE-600),y=300+random.nextFloat()*(WORLD_SIZE-600);if(nearRoad(x,y,125)||nearBuilding(x,y,160))continue;nature.add(new Nature(x,y,24+random.nextFloat()*20,i));}
        for(int qx:new int[]{3300,6300})for(int qy:new int[]{3300,6300})for(int i=0;i<42;i++){float x=qx+random.nextFloat()*800-400,y=qy+random.nextFloat()*800-400;if(!nearRoad(x,y,90))nature.add(new Nature(x,y,28+random.nextFloat()*18,10+i));}
    }
    private boolean nearRoad(float x,float y,float pad){for(Road r:roads){if(r.x1==r.x2&&Math.abs(x-r.x1)<r.w*.5f+pad&&y>=Math.min(r.y1,r.y2)-pad&&y<=Math.max(r.y1,r.y2)+pad)return true;if(r.y1==r.y2&&Math.abs(y-r.y1)<r.w*.5f+pad&&x>=Math.min(r.x1,r.x2)-pad&&x<=Math.max(r.x1,r.x2)+pad)return true;}return false;}
    private boolean nearBuilding(float x,float y,float pad){for(Building b:buildings)if(Math.abs(x-b.x)<b.w+pad&&Math.abs(y-b.y)<260+pad)return true;return false;}

    public void draw(Canvas c,float px,float py,float s,float vw,float vh,float hud){lastW=vw;lastH=vh;lastHud=hud;drawParallaxBackground(c,px,py,vw,vh,hud);float ox=vw/2f-px*s,oy=hud+(vh-hud)*.5f-py*s;c.save();c.translate(ox,oy);drawTerrain(c,s);drawRoads(c,s);drawProps(c,s,py,false);c.restore();}
    public void drawForeground(Canvas c,float px,float py,float s){float ox=lastW/2f-px*s,oy=lastHud+(lastH-lastHud)*.5f-py*s;c.save();c.translate(ox,oy);drawProps(c,s,py,true);c.restore();}
    private void drawProps(Canvas c,float s,float py,boolean foreground){ArrayList<PropRef> refs=new ArrayList<>();for(Building b:buildings)if((b.y>py)==foreground)refs.add(new PropRef(b.y,b,null));for(Nature n:nature)if((n.y>py)==foreground)refs.add(new PropRef(n.y,null,n));Collections.sort(refs,Comparator.comparingDouble(a->a.y));for(PropRef r:refs){if(r.building!=null)drawBuilding(c,r.building,s);else drawNature(c,r.nature,s);}}

    private void drawTerrain(Canvas c,float s){p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(92,104,72));c.drawRect(0,0,WORLD_SIZE*s,WORLD_SIZE*s,p);if(ground!=null){int tile=Math.max(280,Math.round(540*s));ground.setAlpha(70);for(int y=0;y<WORLD_SIZE*s;y+=tile)for(int x=0;x<WORLD_SIZE*s;x+=tile){ground.setBounds(x,y,x+tile,y+tile);ground.draw(c);}ground.setAlpha(255);}p.setColor(Color.argb(22,255,255,220));float tile=720*s;for(float y=0;y<WORLD_SIZE*s;y+=tile)for(float x=0;x<WORLD_SIZE*s;x+=tile)c.drawRect(x+3*s,y+3*s,x+tile-3*s,y+tile-3*s,p);}

    private void drawRoads(Canvas c,float s){for(int i=0;i<roads.size();i++){Road r=roads.get(i);Bitmap art=assets.road(i);if(art!=null)drawRoadBitmap(c,r,art,s);else drawRoadVector(c,r,s);}}
    private void drawRoadBitmap(Canvas c,Road r,Bitmap art,float s){p.setAlpha(255);if(r.x1==r.x2){float top=Math.min(r.y1,r.y2)*s,bottom=Math.max(r.y1,r.y2)*s;for(float y=top;y<bottom;y+=r.w*s)c.drawBitmap(art,null,new RectF((r.x1-r.w/2)*s,y,(r.x1+r.w/2)*s,Math.min(bottom,y+r.w*s)),p);}else if(r.y1==r.y2){float left=Math.min(r.x1,r.x2)*s,right=Math.max(r.x1,r.x2)*s;for(float x=left;x<right;x+=r.w*s)c.drawBitmap(art,null,new RectF(x,(r.y1-r.w/2)*s,Math.min(right,x+r.w*s),(r.y1+r.w/2)*s),p);}else drawRoadVector(c,r,s);}
    private void drawRoadVector(Canvas c,Road r,float s){p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(54,56,53));if(r.x1==r.x2)c.drawRect((r.x1-r.w/2)*s,Math.min(r.y1,r.y2)*s,(r.x1+r.w/2)*s,Math.max(r.y1,r.y2)*s,p);else if(r.y1==r.y2)c.drawRect(Math.min(r.x1,r.x2)*s,(r.y1-r.w/2)*s,Math.max(r.x1,r.x2)*s,(r.y1+r.w/2)*s,p);else{p.setStrokeWidth(r.w*s);p.setStrokeCap(Paint.Cap.BUTT);c.drawLine(r.x1*s,r.y1*s,r.x2*s,r.y2*s,p);}p.setColor(Color.rgb(125,127,118));p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(5*s);if(r.x1==r.x2)c.drawLine((r.x1-r.w/2+12)*s,Math.min(r.y1,r.y2)*s,(r.x1-r.w/2+12)*s,Math.max(r.y1,r.y2)*s,p);else if(r.y1==r.y2)c.drawLine(Math.min(r.x1,r.x2)*s,(r.y1-r.w/2+12)*s,Math.max(r.x1,r.x2)*s,(r.y1-r.w/2+12)*s,p);if(r.w>=ROAD_MAIN-1){p.setColor(Color.rgb(239,214,122));p.setStrokeWidth(5*s);p.setPathEffect(new DashPathEffect(new float[]{34*s,26*s},0));if(r.x1==r.x2)c.drawLine(r.x1*s,Math.min(r.y1,r.y2)*s,r.x1*s,Math.max(r.y1,r.y2)*s,p);else if(r.y1==r.y2)c.drawLine(Math.min(r.x1,r.x2)*s,r.y1*s,Math.max(r.x1,r.x2)*s,r.y1*s,p);p.setPathEffect(null);}p.setStyle(Paint.Style.FILL);}

    private void drawBuilding(Canvas c,Building b,float s){Bitmap art=assets.building(b.kind);if(art==null)return;float depth=.78f+(b.y/WORLD_SIZE)*.42f,w=b.w*s*depth,h=w*art.getHeight()/(float)Math.max(1,art.getWidth());int floors=Math.max(1,b.floors);float step=h*.64f;for(int f=0;f<floors;f++){float yy=b.y*s-f*step,sc=f==floors-1?1f:.96f;c.drawBitmap(art,null,new RectF(b.x*s-w*.5f*sc,yy-h*sc,b.x*s+w*.5f*sc,yy),p);}if(b.tower&&floors>=4){Bitmap roof=assets.building((b.kind+57)%129);if(roof!=null){float rw=w*.78f,rh=rw*roof.getHeight()/(float)Math.max(1,roof.getWidth()),top=b.y*s-floors*step-h*.15f;c.drawBitmap(roof,null,new RectF(b.x*s-rw*.5f,top-rh,b.x*s+rw*.5f,top),p);}}}
    private void drawNature(Canvas c,Nature n,float s){Bitmap art=(n.kind%3==0)?assets.tree(n.kind):assets.grass(n.kind);if(art==null)return;float depth=.78f+(n.y/WORLD_SIZE)*.42f,size=n.size*s*depth,ratio=art.getHeight()/(float)Math.max(1,art.getWidth());c.drawBitmap(art,null,new RectF(n.x*s-size*.5f,n.y*s-size*ratio,n.x*s+size*.5f,n.y*s),p);}
    private void drawParallaxBackground(Canvas c,float px,float py,float vw,float vh,float hud){p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(24,39,35));c.drawRect(0,hud,vw,vh,p);if(background==null)return;float x=-(px*.035f)%vw;int top=Math.round(hud),w=Math.round(vw*1.2f),h=Math.max(Math.round(vh-hud),1);background.setAlpha(170);background.setBounds(Math.round(x-w*.08f),top,Math.round(x+w),top+h);background.draw(c);background.setBounds(Math.round(x+w*.82f),top,Math.round(x+w*1.90f),top+h);background.draw(c);background.setAlpha(255);}

    private static final class Road{final float x1,y1,x2,y2,w;Road(float x1,float y1,float x2,float y2,float w){this.x1=x1;this.y1=y1;this.x2=x2;this.y2=y2;this.w=w;}}
    private static final class Building{final float x,y,w;final int floors,kind;final boolean tower;Building(float x,float y,float w,int floors,int kind,boolean tower){this.x=x;this.y=y;this.w=w;this.floors=floors;this.kind=kind;this.tower=tower;}}
    private static final class Nature{final float x,y,size;final int kind;Nature(float x,float y,float size,int kind){this.x=x;this.y=y;this.size=size;this.kind=kind;}}
    private static final class PropRef{final float y;final Building building;final Nature nature;PropRef(float y,Building b,Nature n){this.y=y;this.building=b;this.nature=n;}}
}
