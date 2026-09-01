package com.persiawar2d;

import java.util.ArrayList;
import java.util.List;

/** Fixed Level 12 city plan: readable districts, roads, cover and landmark spaces. */
public final class CityKit {
    public enum Type { HOUSE_1, HOUSE_2, WAREHOUSE, SHOP, WALL, ROAD, SIDEWALK, TREE, ROCK, CAR, LOOT, RUBBLE }
    public static final class Piece {
        public final Type type; public final float x,z,w,d,h; public final int panel;
        Piece(Type t,float x,float z,float w,float d,float h,int panel){this.type=t;this.x=x;this.z=z;this.w=w;this.d=d;this.h=h;this.panel=panel;}
    }
    private final ArrayList<Piece> pieces=new ArrayList<>();
    public CityKit(long ignoredSeed){generate();}
    public List<Piece> pieces(){return pieces;}
    private void generate(){
        int p=0;
        road(0,0,8,68); road(0,0,68,8); road(-15,0,5,68); road(15,0,5,68); road(0,-15,68,5); road(0,15,68,5);
        sidewalk(0,-4.7f,68,.8f); sidewalk(0,4.7f,68,.8f); sidewalk(-4.7f,0,.8f,68); sidewalk(4.7f,0,.8f,68);
        sidewalk(0,-17.8f,68,.8f); sidewalk(0,17.8f,68,.8f); sidewalk(-17.8f,0,.8f,68); sidewalk(17.8f,0,.8f,68);

        // NW residential district.
        add(Type.HOUSE_1,-27,27,7,6,3.2f,p++); add(Type.HOUSE_2,-27,19,7,6,5.2f,p++);
        add(Type.HOUSE_1,-27,11,7,5.5f,3.2f,p++); add(Type.SHOP,-20,27,6,6,3.2f,p++);
        add(Type.HOUSE_2,-11,27,6,6,5.2f,p++); add(Type.HOUSE_1,-11,19,6,6,3.2f,p++);
        add(Type.SHOP,-11,11,6,5.5f,3.2f,p++);

        // NE mixed district.
        add(Type.HOUSE_2,27,27,7,6,5.2f,p++); add(Type.HOUSE_1,27,19,7,6,3.2f,p++);
        add(Type.SHOP,20,11,6,5.5f,3.2f,p++); add(Type.HOUSE_1,11,27,6,6,3.2f,p++);
        add(Type.HOUSE_2,11,19,6,6,5.2f,p++); add(Type.HOUSE_1,27,11,7,5.5f,3.2f,p++);

        // SW neighborhood and damaged block.
        add(Type.HOUSE_1,-27,-27,7,6,3.2f,p++); add(Type.HOUSE_2,-27,-19,7,6,5.2f,p++);
        add(Type.HOUSE_1,-11,-27,6,6,3.2f,p++); add(Type.SHOP,-20,-19,6,6,3.2f,p++);
        add(Type.HOUSE_1,-11,-19,6,6,3.2f,p++); add(Type.RUBBLE,-26,-11,7,4,1.1f,p++);
        add(Type.RUBBLE,-18,-11,4,3,1.0f,p++);

        // SE industrial district.
        add(Type.WAREHOUSE,25,-26,10,7,4.0f,p++); add(Type.WAREHOUSE,25,-17,10,7,4.0f,p++);
        add(Type.SHOP,11,-26,6,6,3.2f,p++); add(Type.HOUSE_1,11,-17,6,6,3.2f,p++);
        add(Type.WALL,18,-12,1,8,2.0f,p++); add(Type.WALL,31,-12,1,8,2.0f,p++);

        // Cover, vehicles and loot around combat lanes.
        tree(-30,4); tree(-23,8); tree(-9,8); tree(9,8); tree(23,8); tree(30,4);
        tree(-30,-4); tree(-9,-8); tree(9,-8); tree(30,-4);
        rock(-5,27); rock(5,-27); rock(-31,-8); rock(31,8);
        car(-7,10,0); car(7,-10,90); car(-7,-23,0); car(7,23,90); car(23,-7,90);
        loot(-4,11); loot(4,-11); loot(-22,5); loot(22,-5); loot(0,27); loot(0,-27);

        // Perimeter with four wide entries.
        for(int x=-32;x<=32;x+=4){if(Math.abs(x)>7){add(Type.WALL,x,-34,3.5f,.45f,1.7f,p++);add(Type.WALL,x,34,3.5f,.45f,1.7f,p++);}}
        for(int z=-30;z<=30;z+=4){if(Math.abs(z)>7){add(Type.WALL,-34,z,.45f,3.5f,1.7f,p++);add(Type.WALL,34,z,.45f,3.5f,1.7f,p++);}}
    }
    private void road(float x,float z,float w,float d){add(Type.ROAD,x,z,w,d,.04f,0);}
    private void sidewalk(float x,float z,float w,float d){add(Type.SIDEWALK,x,z,w,d,.08f,0);}
    private void tree(float x,float z){add(Type.TREE,x,z,1.5f,1.5f,3.2f,0);}
    private void rock(float x,float z){add(Type.ROCK,x,z,1.7f,1.2f,.8f,0);}
    private void car(float x,float z,float r){add(Type.CAR,x,z,2.8f,1.5f,.9f,(int)r);}
    private void loot(float x,float z){add(Type.LOOT,x,z,1.0f,1.0f,.55f,0);}
    private void add(Type t,float x,float z,float w,float d,float h,int panel){pieces.add(new Piece(t,x,z,w,d,h,panel));}
}
