package com.persiawar2d;

import java.util.ArrayList;
import java.util.List;

/** Deliberate 3D level layout: roads, combat lanes, houses, shops, warehouse and walls. */
public final class CityKit {
    public enum Type { HOUSE_1, HOUSE_2, WAREHOUSE, SHOP, WALL }
    public static final class Piece {
        public final Type type; public final float x,z,w,d,h; public final int panel;
        Piece(Type t,float x,float z,float w,float d,float h,int panel){this.type=t;this.x=x;this.z=z;this.w=w;this.d=d;this.h=h;this.panel=panel;}
    }
    private final ArrayList<Piece> pieces=new ArrayList<>();
    public CityKit(long ignoredSeed){generate();}
    public List<Piece> pieces(){return pieces;}
    private void generate(){
        int p=0;
        // West residential block.
        add(Type.HOUSE_1,-27,-24,5,5,3, p++); add(Type.HOUSE_2,-19,-24,5,5,5.5f,p++);
        add(Type.HOUSE_1,-27,-16,5,5,3,p++); add(Type.SHOP,-19,-16,5,5,3,p++);
        add(Type.HOUSE_2,-27,-8,5,5,5.5f,p++); add(Type.HOUSE_1,-19,-8,5,5,3,p++);
        // East residential block.
        add(Type.HOUSE_1,19,-24,5,5,3,p++); add(Type.HOUSE_2,27,-24,5,5,5.5f,p++);
        add(Type.SHOP,19,-16,5,5,3,p++); add(Type.HOUSE_1,27,-16,5,5,3,p++);
        add(Type.HOUSE_2,19,-8,5,5,5.5f,p++); add(Type.HOUSE_1,27,-8,5,5,3,p++);
        // North side: larger buildings and a warehouse.
        add(Type.WAREHOUSE,-25,22,9,6,3.5f,p++); add(Type.HOUSE_2,-13,24,5,5,5.5f,p++);
        add(Type.SHOP,13,24,5,5,3,p++); add(Type.WAREHOUSE,25,22,9,6,3.5f,p++);
        add(Type.HOUSE_1,-12,16,5,5,3,p++); add(Type.HOUSE_2,12,16,5,5,5.5f,p++);
        // South side with a deliberately damaged-looking sparse block.
        add(Type.HOUSE_1,-25,-30,5,4,3,p++); add(Type.HOUSE_1,25,-30,5,4,3,p++);
        add(Type.WALL,-17,-30,3,1,1.6f,p++); add(Type.WALL,17,-30,3,1,1.6f,p++);
        // Perimeter, leaving four wide entry lanes.
        for(int x=-32;x<=32;x+=4){if(Math.abs(x)>6){add(Type.WALL,x,-34,3.5f,.45f,1.6f,p++);add(Type.WALL,x,34,3.5f,.45f,1.6f,p++);}}
        for(int z=-30;z<=30;z+=4){if(Math.abs(z)>6){add(Type.WALL,-34,z,.45f,3.5f,1.6f,p++);add(Type.WALL,34,z,.45f,3.5f,1.6f,p++);}}
    }
    private void add(Type t,float x,float z,float w,float d,float h,int panel){pieces.add(new Piece(t,x,z,w,d,h,panel));}
}
