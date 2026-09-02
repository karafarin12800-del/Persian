package com.persiawar2d.world;

import android.graphics.RectF;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Random;

/**
 * Authoritative gameplay map. Rendering reads this data; gameplay collision and
 * line-of-sight also read the same geometry so visual and logical worlds cannot diverge.
 */
public final class WorldMap {
    public static final float SIZE = 6000f;

    public static final class Building {
        public final float x, y, w, h;
        public final int style;
        public Building(float x, float y, float w, float h, int style) {
            this.x = x; this.y = y; this.w = w; this.h = h; this.style = style;
        }
        public RectF rect(float pad) { return new RectF(x-pad, y-pad, x+w+pad, y+h+pad); }
    }

    public static final class Road {
        public final float x1, y1, x2, y2, width;
        public final boolean horizontal;
        public Road(float x1, float y1, float x2, float y2, float width, boolean horizontal) {
            this.x1=x1; this.y1=y1; this.x2=x2; this.y2=y2; this.width=width; this.horizontal=horizontal;
        }
    }

    public static final class Vehicle {
        public final float x, y, w, h, angle;
        public Vehicle(float x, float y, float w, float h, float angle) {
            this.x=x; this.y=y; this.w=w; this.h=h; this.angle=angle;
        }
    }

    public static final class Fence {
        public final float x1, y1, x2, y2;
        public Fence(float x1, float y1, float x2, float y2) { this.x1=x1; this.y1=y1; this.x2=x2; this.y2=y2; }
    }

    public static final class Prop {
        public final float x, y, size;
        public final int kind;
        public Prop(float x, float y, float size, int kind) { this.x=x; this.y=y; this.size=size; this.kind=kind; }
    }

    private final ArrayList<Building> buildings = new ArrayList<>();
    private final ArrayList<Road> roads = new ArrayList<>();
    private final ArrayList<Vehicle> vehicles = new ArrayList<>();
    private final ArrayList<Fence> fences = new ArrayList<>();
    private final ArrayList<Prop> trees = new ArrayList<>();
    private final ArrayList<Prop> bushes = new ArrayList<>();
    private final ArrayList<Prop> grass = new ArrayList<>();
    private final Random random = new Random(20260902L);

    public WorldMap() { build(); }

    private void build() {
        float[] mainX = {1000, 3000, 5000};
        float[] mainY = {900, 2850, 4800};
        for (float y : mainY) roads.add(new Road(300,y,5700,y,170,true));
        for (float x : mainX) roads.add(new Road(x,300,x,5700,165,false));

        // Secondary roads keep the large 6000x6000 map navigable and create many tactical blocks.
        addRoad(420,1350,1000,1350,82,true); addRoad(1000,1500,1650,1500,78,true);
        addRoad(1650,1500,1650,900,78,false); addRoad(2050,1150,2050,2850,84,false);
        addRoad(3000,1250,3600,1250,78,true); addRoad(3600,1250,3600,900,78,false);
        addRoad(4050,1600,5000,1600,82,true); addRoad(4450,1600,4450,2850,80,false);
        addRoad(350,2300,1000,2300,78,true); addRoad(1000,2150,1450,2150,76,true);
        addRoad(1450,2150,1450,2850,76,false); addRoad(1750,2400,1750,2850,78,false);
        addRoad(2350,2850,2350,3550,82,false); addRoad(3000,2350,3650,2350,76,true);
        addRoad(3650,2350,3650,2850,76,false); addRoad(4050,2550,5000,2550,78,true);
        addRoad(5400,2200,5400,2850,78,false); addRoad(430,3650,1000,3650,82,true);
        addRoad(1000,3900,1550,3900,76,true); addRoad(1550,3900,1550,4800,76,false);
        addRoad(1900,4100,1900,4800,78,false); addRoad(2200,3500,3000,3500,82,true);
        addRoad(3000,3850,3650,3850,78,true); addRoad(3650,3850,3650,4800,78,false);
        addRoad(4100,3550,5000,3550,82,true); addRoad(4550,3550,4550,4800,78,false);
        addRoad(5250,3300,5250,4800,80,false); addRoad(450,5350,1000,5350,80,true);
        addRoad(1250,5200,1250,5700,76,false); addRoad(1750,5400,3000,5400,82,true);
        addRoad(3450,5200,3450,5700,76,false); addRoad(3800,5400,5000,5400,82,true);
        addRoad(5450,5100,5450,5700,76,false);

        int[][] b={
            {380,390,430,280},{1320,400,430,290},{2080,390,500,300},{3220,400,480,310},{4180,380,450,300},{5220,380,360,330},
            {360,1030,450,300},{1260,1060,500,320},{2200,1040,470,310},{3220,1020,470,320},{4050,1040,440,300},{5220,1040,390,300},
            {350,1750,470,330},{1230,1680,420,300},{1760,1730,400,300},{2300,1700,470,310},{3190,1700,500,330},{3830,1720,430,300},{5130,1720,420,320},
            {360,3000,470,320},{1240,3040,440,290},{1750,3040,430,320},{2420,3040,430,300},{3190,3040,500,320},{3910,3050,420,300},{5120,3030,400,310},
            {360,3950,470,320},{1210,4030,470,310},{1790,4060,420,300},{2250,4000,480,320},{3190,4030,480,310},{3900,4010,440,300},{5150,4000,400,320},
            {360,4900,500,300},{1240,4930,440,320},{1800,4940,470,300},{2450,4920,450,310},{3190,4920,500,320},{3910,4930,440,300},{5140,4920,400,300},
            {420,5450,430,250},{1380,5450,420,250},{2200,5520,480,240},{3250,5480,450,250},{4030,5520,440,240},{5140,5450,380,250}
        };
        int style=0; for(int[] v:b) buildings.add(new Building(v[0],v[1],v[2],v[3],style++));

        // Tactical props: parked cars, perimeter fences, trees, bushes and grass.
        for(int i=0;i<44;i++) {
            boolean vertical=(i%3==0);
            float x=420 + random.nextInt(5160), y=420 + random.nextInt(5160);
            if(nearRoad(x,y,90)) {
                float angle=vertical?90:0;
                vehicles.add(new Vehicle(x,y,vertical?28:60,vertical?60:28,angle));
            }
        }
        for(int i=0;i<58;i++) {
            float x=250+random.nextFloat()*5500, y=250+random.nextFloat()*5500;
            if(nearRoad(x,y,95) || nearBuilding(x,y,55)) continue;
            float len=35+random.nextFloat()*75;
            if(i%2==0) fences.add(new Fence(x,y,x+len,y));
            else fences.add(new Fence(x,y,x,y+len));
        }
        for(int i=0;i<190;i++) {
            float x=180+random.nextFloat()*5640, y=180+random.nextFloat()*5640;
            if(nearRoad(x,y,100) || nearBuilding(x,y,95)) continue;
            trees.add(new Prop(x,y,24+random.nextFloat()*34,i%4));
        }
        for(int i=0;i<150;i++) {
            float x=160+random.nextFloat()*5680, y=160+random.nextFloat()*5680;
            if(nearRoad(x,y,70) || nearBuilding(x,y,60)) continue;
            bushes.add(new Prop(x,y,12+random.nextFloat()*18,i%3));
        }
        for(int i=0;i<520;i++) {
            float x=random.nextFloat()*SIZE, y=random.nextFloat()*SIZE;
            if(nearRoad(x,y,75) || nearBuilding(x,y,40)) continue;
            grass.add(new Prop(x,y,5+random.nextFloat()*12,i%4));
        }
    }

    private void addRoad(float x1,float y1,float x2,float y2,float width,boolean horizontal){roads.add(new Road(x1,y1,x2,y2,width,horizontal));}
    public boolean nearRoad(float x,float y,float pad){for(Road r:roads){if(r.horizontal){if(x>=r.x1-pad&&x<=r.x2+pad&&Math.abs(y-r.y1)<=r.width*.5f+pad)return true;}else if(y>=r.y1-pad&&y<=r.y2+pad&&Math.abs(x-r.x1)<=r.width*.5f+pad)return true;}return false;}
    public boolean nearBuilding(float x,float y,float pad){for(Building b:buildings){if(x>=b.x-pad&&x<=b.x+b.w+pad&&y>=b.y-pad&&y<=b.y+b.h+pad)return true;}return false;}

    public boolean isBlocked(float x,float y,float radius){
        if(x<60+radius||y<60+radius||x>SIZE-60-radius||y>SIZE-60-radius)return true;
        for(Building b:buildings) if(b.rect(radius).contains(x,y)) return true;
        for(Vehicle v:vehicles) if(v.x-v.w*.5f-radius<x&&x<v.x+v.w*.5f+radius&&v.y-v.h*.5f-radius<y&&y<v.y+v.h*.5f+radius)return true;
        for(Fence f:fences) if(pointSegmentDistance(x,y,f.x1,f.y1,f.x2,f.y2)<radius+7f)return true;
        return false;
    }

    public boolean hasLineOfSight(float x1,float y1,float x2,float y2){
        float d=(float)Math.hypot(x2-x1,y2-y1);int steps=Math.max(4,(int)(d/22f));
        for(int i=1;i<steps;i++){float t=i/(float)steps;float x=x1+(x2-x1)*t,y=y1+(y2-y1)*t;if(isSolidAt(x,y))return false;}
        return true;
    }
    private boolean isSolidAt(float x,float y){
        for(Building b:buildings) if(b.rect(1).contains(x,y)) return true;
        for(Vehicle v:vehicles) if(v.x-v.w*.5f<x&&x<v.x+v.w*.5f&&v.y-v.h*.5f<y&&y<v.y+v.h*.5f)return true;
        for(Fence f:fences) if(pointSegmentDistance(x,y,f.x1,f.y1,f.x2,f.y2)<5f)return true;
        return false;
    }
    private float pointSegmentDistance(float px,float py,float x1,float y1,float x2,float y2){float dx=x2-x1,dy=y2-y1;if(dx==0&&dy==0)return(float)Math.hypot(px-x1,py-y1);float t=((px-x1)*dx+(py-y1)*dy)/(dx*dx+dy*dy);t=Math.max(0,Math.min(1,t));return(float)Math.hypot(px-(x1+t*dx),py-(y1+t*dy));}

    public List<Building> buildings(){return Collections.unmodifiableList(buildings);}
    public List<Road> roads(){return Collections.unmodifiableList(roads);}
    public List<Vehicle> vehicles(){return Collections.unmodifiableList(vehicles);}
    public List<Fence> fences(){return Collections.unmodifiableList(fences);}
    public List<Prop> trees(){return Collections.unmodifiableList(trees);}
    public List<Prop> bushes(){return Collections.unmodifiableList(bushes);}
    public List<Prop> grass(){return Collections.unmodifiableList(grass);}
}
