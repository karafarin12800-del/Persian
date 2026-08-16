package com.persiawar2d;

import android.content.Context;
import android.graphics.*;
import android.graphics.drawable.Drawable;
import java.util.*;

/** Professional 2.5D world layout: readable districts, tactical spaces, parks and landmarks. */
public final class WorldRenderer {
    public static final float WORLD_SIZE=9600f;
    private static final float MAIN=150f, STREET=94f, ALLEY=58f;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final Drawable background,ground;
    private final KenneyEnvironment assets;
    private final ArrayList<Road> roads=new ArrayList<>();
    private final ArrayList<Building> buildings=new ArrayList<>();
    private final ArrayList<Nature> nature=new ArrayList<>();
    private final ArrayList<Park> parks=new ArrayList<>();
    private final Random random=new Random(20260817L);
    private float lastW,lastH,lastHud;

    public WorldRenderer(Context c){
        background=c.getDrawable(R.drawable.achaemenid_background);
        ground=c.getDrawable(R.drawable.persia_ground);
        assets=new KenneyEnvironment();assets.load(c);buildWorld();
    }

    private void buildWorld(){
        roads.clear();buildRoadHierarchy();buildDistricts();buildParks();scatterNature();
    }

    /* The city is deliberately not a perfect grid: three strong axes, a civic ring,
       short connectors and several dead-end alleys create readable game spaces. */
    private void buildRoadHierarchy(){
        // Primary avenues
        addRoad(480,1700,9120,1700,MAIN); addRoad(480,7900,9120,7900,MAIN);
        addRoad(1700,480,1700,9120,MAIN); addRoad(7900,480,7900,9120,MAIN);
        // Central civic cross
        addRoad(1700,4800,7900,4800,MAIN); addRoad(4800,1700,4800,7900,MAIN);
        // Offset secondary streets: these break repetition and define districts.
        addRoad(520,2800,4300,2800,STREET); addRoad(5300,2800,9080,2800,STREET);
        addRoad(520,6400,4300,6400,STREET); addRoad(5300,6400,9080,6400,STREET);
        addRoad(2900,520,2900,4200,STREET); addRoad(2900,5400,2900,9080,STREET);
        addRoad(6500,520,6500,4100,STREET); addRoad(6500,5300,6500,9080,STREET);
        // Civic ring / plaza perimeter
        addRoad(3500,3500,6100,3500,STREET); addRoad(6100,3500,6100,6100,STREET);
        addRoad(6100,6100,3500,6100,STREET); addRoad(3500,6100,3500,3500,STREET);
        // Short connectors create alternate routes for combat.
        addRoad(900,3600,2200,3600,ALLEY); addRoad(2200,3600,2200,4500,ALLEY);
        addRoad(7400,3600,8700,3600,ALLEY); addRoad(7400,3600,7400,4500,ALLEY);
        addRoad(900,6900,2200,6900,ALLEY); addRoad(2200,6900,2200,6000,ALLEY);
        addRoad(7400,6900,8700,6900,ALLEY); addRoad(7400,6900,7400,6000,ALLEY);
        // Four diagonal gateways only; diagonals are landmarks, not the whole road system.
        addRoad(520,520,1450,1450,82); addRoad(9080,520,8150,1450,82);
        addRoad(520,9080,1450,8150,82); addRoad(9080,9080,8150,8150,82);
    }

    private void buildDistricts(){
        // West residential quarter: smaller houses, wider gardens.
        block(950,2150,4,1.00f); block(950,5050,4,1.02f); block(950,7600,4,.96f);
        block(2150,2150,5,1.00f); block(2150,7600,5,.98f);
        // East residential quarter.
        block(8650,2150,4,1.00f); block(8650,5050,4,1.02f); block(8650,7600,4,.96f);
        block(7450,2150,5,1.02f); block(7450,7600,5,.98f);
        // North market / civic services.
        block(3850,2200,5,1.12f); block(5550,2200,5,1.10f);
        block(3850,7450,4,1.14f); block(5550,7450,4,1.10f);
        // Dense central commercial blocks, deliberately leaving combat lanes.
        block(3650,3950,4,1.18f); block(5950,3950,4,1.18f);
        block(3650,5650,4,1.18f); block(5950,5650,4,1.18f);
        // King's quarter: landmark towers around the central plaza.
        tower(4800,4800,7,1.70f,122);
        tower(4250,4250,4,1.18f,30); tower(5350,4250,4,1.18f,99);
        tower(4250,5350,4,1.18f,113); tower(5350,5350,4,1.18f,1);
        block(4800,3050,3,1.05f); block(4800,6550,3,1.05f);
    }

    private void buildParks(){
        // Large parks are explicit spaces, not random green noise.
        parks.add(new Park(2450,4450,1150,620,18));
        parks.add(new Park(7150,4450,1150,620,41));
        parks.add(new Park(2450,5550,900,420,73));
        parks.add(new Park(7150,5550,900,420,101));
        parks.add(new Park(4800,1250,1200,360,129));
        parks.add(new Park(4800,8350,1200,360,161));
    }

    private void block(float cx,float cy,int count,float scale){
        float sx=260f*scale,sy=210f*scale;
        for(int i=0;i<count;i++){
            int col=i%3,row=i/3;
            float x=cx+(col-1)*sx, y=cy+(row-(count>3?.5f:0))*sy;
            int kind=chooseBuilding(i*37+Math.round(cx)*3+Math.round(cy));
            int floors=1+Math.floorMod(i+(int)cx+(int)cy,3);
            float w=(150+(i%2)*30)*scale;
            buildings.add(new Building(x,y,w,floors,kind,false));
        }
    }

    private void tower(float x,float y,int floors,float scale,int kind){
        buildings.add(new Building(x,y,205f*scale,floors,Math.floorMod(kind,129),true));
    }

    private int chooseBuilding(int seed){
        int[] pool={0,1,2,3,4,9,10,14,17,18,20,21,23,24,25,30,31,32,33,34,36,40,41,43,44,45,49,92,99,100,101,106,107,108,109,113,114,115,116,117,122,123,124,125};
        return pool[Math.floorMod(seed,pool.length)];
    }

    private void scatterNature(){
        // Controlled roadside trees + park clusters; never place decoration inside roads/buildings.
        for(Road r:roads){
            if(r.w<STREET)continue;
            if(r.x1==r.x2){for(float y=Math.min(r.y1,r.y2)+180;y<Math.max(r.y1,r.y2)-120;y+=260){addTree(r.x1-r.w*.72f,y,28,(int)y);addTree(r.x1+r.w*.72f,y+90,25,(int)y+3);}}
            else if(r.y1==r.y2){for(float x=Math.min(r.x1,r.x2)+180;x<Math.max(r.x1,r.x2)-120;x+=260){addTree(x,r.y1-r.w*.72f,27,(int)x);addTree(x+90,r.y1+r.w*.72f,25,(int)x+7);}}
        }
        for(Park park:parks){
            for(int i=0;i<34;i++){
                float x=park.cx+(random.nextFloat()-.5f)*park.w*.90f;
                float y=park.cy+(random.nextFloat()-.5f)*park.h*.75f;
                addTree(x,y,22+random.nextFloat()*15,park.seed+i);
            }
            for(int i=0;i<16;i++){
                float x=park.cx+(random.nextFloat()-.5f)*park.w*.85f;
                float y=park.cy+(random.nextFloat()-.5f)*park.h*.65f;
                nature.add(new Nature(x,y,18+random.nextFloat()*12,park.seed+i+100,1));
            }
        }
    }

    private void addTree(float x,float y,float size,int kind){if(!nearRoad(x,y,65)&&!nearBuilding(x,y,75))nature.add(new Nature(x,y,size,kind,0));}
    private void addRoad(float x1,float y1,float x2,float y2,float w){roads.add(new Road(x1,y1,x2,y2,w));}

    private boolean nearRoad(float x,float y,float pad){
        for(Road r:roads){
            if(r.x1==r.x2&&Math.abs(x-r.x1)<r.w*.5f+pad&&y>=Math.min(r.y1,r.y2)-pad&&y<=Math.max(r.y1,r.y2)+pad)return true;
            if(r.y1==r.y2&&Math.abs(y-r.y1)<r.w*.5f+pad&&x>=Math.min(r.x1,r.x2)-pad&&x<=Math.max(r.x1,r.x2)+pad)return true;
        }
        return false;
    }
    private boolean nearBuilding(float x,float y,float pad){for(Building b:buildings)if(Math.abs(x-b.x)<b.w+pad&&Math.abs(y-b.y)<250+pad)return true;return false;}

    public void draw(Canvas c,float px,float py,float s,float vw,float vh,float hud){
        lastW=vw;lastH=vh;lastHud=hud;drawParallaxBackground(c,px,py,vw,vh,hud);
        float ox=vw/2f-px*s,oy=hud+(vh-hud)*.5f-py*s;
        c.save();c.translate(ox,oy);drawTerrain(c,s);drawParks(c,s);drawRoads(c,s);drawProps(c,s,py,false);c.restore();
    }
    public void drawForeground(Canvas c,float px,float py,float s){
        float ox=lastW/2f-px*s,oy=lastHud+(lastH-lastHud)*.5f-py*s;c.save();c.translate(ox,oy);drawProps(c,s,py,true);c.restore();
    }

    private void drawTerrain(Canvas c,float s){
        p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(86,101,70));c.drawRect(0,0,WORLD_SIZE*s,WORLD_SIZE*s,p);
        if(ground!=null){int tile=Math.max(300,Math.round(540*s));ground.setAlpha(80);for(int y=0;y<WORLD_SIZE*s;y+=tile)for(int x=0;x<WORLD_SIZE*s;x+=tile){ground.setBounds(x,y,x+tile,y+tile);ground.draw(c);}ground.setAlpha(255);}
    }

    private void drawParks(Canvas c,float s){
        for(Park k:parks){
            p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(105,124,73));c.drawRoundRect((k.cx-k.w/2)*s,(k.cy-k.h/2)*s,(k.cx+k.w/2)*s,(k.cy+k.h/2)*s,55*s,55*s,p);
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(18*s);p.setColor(Color.rgb(184,163,104));c.drawRoundRect((k.cx-k.w/2+22)*s,(k.cy-k.h/2+22)*s,(k.cx+k.w/2-22)*s,(k.cy+k.h/2-22)*s,42*s,42*s,p);
            p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(191,174,116));c.drawCircle(k.cx*s,k.cy*s,42*s,p);
        }
    }

    private void drawRoads(Canvas c,float s){for(int i=0;i<roads.size();i++){Road r=roads.get(i);Bitmap art=assets.road(i);if(art!=null&&isAxisRoad(r))drawRoadBitmap(c,r,art,s);else drawRoadVector(c,r,s);}}
    private boolean isAxisRoad(Road r){return r.x1==r.x2||r.y1==r.y2;}
    private void drawRoadBitmap(Canvas c,Road r,Bitmap art,float s){
        p.setAlpha(255);
        if(r.x1==r.x2){float a=Math.min(r.y1,r.y2)*s,b=Math.max(r.y1,r.y2)*s;for(float y=a;y<b;y+=r.w*s)c.drawBitmap(art,null,new RectF((r.x1-r.w/2)*s,y,(r.x1+r.w/2)*s,Math.min(b,y+r.w*s)),p);}
        else {float a=Math.min(r.x1,r.x2)*s,b=Math.max(r.x1,r.x2)*s;for(float x=a;x<b;x+=r.w*s)c.drawBitmap(art,null,new RectF(x,(r.y1-r.w/2)*s,Math.min(b,x+r.w*s),(r.y1+r.w/2)*s),p);}
    }
    private void drawRoadVector(Canvas c,Road r,float s){
        p.setStyle(Paint.Style.FILL);p.setColor(r.w>=MAIN?Color.rgb(48,51,48):Color.rgb(61,62,57));
        if(r.x1==r.x2)c.drawRect((r.x1-r.w/2)*s,Math.min(r.y1,r.y2)*s,(r.x1+r.w/2)*s,Math.max(r.y1,r.y2)*s,p);
        else if(r.y1==r.y2)c.drawRect(Math.min(r.x1,r.x2)*s,(r.y1-r.w/2)*s,Math.max(r.x1,r.x2)*s,(r.y1+r.w/2)*s,p);
        else{p.setStrokeWidth(r.w*s);p.setStrokeCap(Paint.Cap.BUTT);c.drawLine(r.x1*s,r.y1*s,r.x2*s,r.y2*s,p);}
        p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,4*s));p.setColor(Color.rgb(194,184,137));
        if(r.w>=MAIN){p.setPathEffect(new DashPathEffect(new float[]{38*s,30*s},0));if(r.x1==r.x2)c.drawLine(r.x1*s,Math.min(r.y1,r.y2)*s,r.x1*s,Math.max(r.y1,r.y2)*s,p);else if(r.y1==r.y2)c.drawLine(Math.min(r.x1,r.x2)*s,r.y1*s,Math.max(r.x1,r.x2)*s,r.y1*s,p);p.setPathEffect(null);}
        p.setStyle(Paint.Style.FILL);
    }

    private void drawProps(Canvas c,float s,float py,boolean foreground){
        ArrayList<PropRef> refs=new ArrayList<>();
        for(Building b:buildings)if((b.y>py)==foreground)refs.add(new PropRef(b.y,b,null));
        for(Nature n:nature)if((n.y>py)==foreground)refs.add(new PropRef(n.y,null,n));
        refs.sort(Comparator.comparingDouble(a->a.y));
        for(PropRef r:refs){if(r.building!=null)drawBuilding(c,r.building,s);else drawNature(c,r.nature,s);}
    }

    private void drawBuilding(Canvas c,Building b,float s){
        Bitmap art=assets.building(b.kind);if(art==null)return;
        float depth=.78f+(b.y/WORLD_SIZE)*.42f,w=b.w*s*depth,h=w*art.getHeight()/(float)Math.max(1,art.getWidth());
        int floors=Math.max(1,b.floors);float step=h*.62f;
        for(int f=0;f<floors;f++){float yy=b.y*s-f*step;float sc=f==floors-1?1f:.97f;c.drawBitmap(art,null,new RectF(b.x*s-w*.5f*sc,yy-h*sc,b.x*s+w*.5f*sc,yy),p);}
        if(b.tower&&floors>=4){Bitmap roof=assets.building((b.kind+57)%129);if(roof!=null){float rw=w*.82f,rh=rw*roof.getHeight()/(float)Math.max(1,roof.getWidth());float top=b.y*s-floors*step-h*.10f;c.drawBitmap(roof,null,new RectF(b.x*s-rw*.5f,top-rh,b.x*s+rw*.5f,top),p);}}
    }
    private void drawNature(Canvas c,Nature n,float s){
        Bitmap art=n.type==0?assets.tree(n.kind):assets.grass(n.kind);if(art==null)return;
        float depth=.80f+(n.y/WORLD_SIZE)*.38f,size=n.size*s*depth,ratio=art.getHeight()/(float)Math.max(1,art.getWidth());
        p.setAlpha(255);c.drawBitmap(art,null,new RectF(n.x*s-size*.5f,n.y*s-size*ratio,n.x*s+size*.5f,n.y*s),p);
    }
    private void drawParallaxBackground(Canvas c,float px,float py,float vw,float vh,float hud){
        p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(24,39,35));c.drawRect(0,hud,vw,vh,p);if(background==null)return;
        float x=-(px*.035f)%vw;int top=Math.round(hud),w=Math.max(1,Math.round(vw*1.2f)),h=Math.max(1,Math.round(vh-hud));background.setAlpha(165);
        background.setBounds(Math.round(x-w*.08f),top,Math.round(x+w),top+h);background.draw(c);background.setBounds(Math.round(x+w*.82f),top,Math.round(x+w*1.90f),top+h);background.draw(c);background.setAlpha(255);
    }

    private static final class Road{final float x1,y1,x2,y2,w;Road(float x1,float y1,float x2,float y2,float w){this.x1=x1;this.y1=y1;this.x2=x2;this.y2=y2;this.w=w;}}
    private static final class Building{final float x,y,w;final int floors,kind;final boolean tower;Building(float x,float y,float w,int floors,int kind,boolean tower){this.x=x;this.y=y;this.w=w;this.floors=floors;this.kind=kind;this.tower=tower;}}
    private static final class Nature{final float x,y,size;final int kind,type;Nature(float x,float y,float size,int kind,int type){this.x=x;this.y=y;this.size=size;this.kind=kind;this.type=type;}}
    private static final class Park{final float cx,cy,w,h;final int seed;Park(float cx,float cy,float w,float h,int seed){this.cx=cx;this.cy=cy;this.w=w;this.h=h;this.seed=seed;}}
    private static final class PropRef{final float y;final Building building;final Nature nature;PropRef(float y,Building b,Nature n){this.y=y;this.building=b;this.nature=n;}}
}
