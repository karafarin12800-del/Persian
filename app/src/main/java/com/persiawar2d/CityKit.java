package com.persiawar2d;

import java.util.ArrayList;
import java.util.List;
import java.util.Random;

/** Reusable 3D city kit: footprints, roads, walls and building facade selections. */
public final class CityKit {
    public enum Type { HOUSE_1, HOUSE_2, WAREHOUSE, SHOP, WALL }
    public static final class Piece {
        public final Type type; public final float x,z,w,d,h; public final int panel;
        Piece(Type t,float x,float z,float w,float d,float h,int panel){this.type=t;this.x=x;this.z=z;this.w=w;this.d=d;this.h=h;this.panel=panel;}
    }
    private final ArrayList<Piece> pieces=new ArrayList<>();
    public CityKit(long seed){generate(seed);}
    public List<Piece> pieces(){return pieces;}
    private void generate(long seed){
        Random r=new Random(seed);
        for(int z=-32;z<=32;z+=8) for(int x=-32;x<=32;x+=8){
            if(Math.abs(x)<8&&Math.abs(z)<8) continue;
            int roll=r.nextInt(100); Type t=roll<58?Type.HOUSE_1:roll<78?Type.HOUSE_2:roll<88?Type.SHOP:roll<95?Type.WAREHOUSE:Type.WALL;
            float w=t==Type.WAREHOUSE?5.5f:3.4f, d=t==Type.WAREHOUSE?4.5f:3.4f, h=t==Type.HOUSE_2?5.5f:t==Type.WAREHOUSE?3.5f:t==Type.WALL?1.8f:3.0f;
            pieces.add(new Piece(t,x,z,w,d,h,r.nextInt(12)));
        }
        // Perimeter walls and controlled open combat lanes.
        for(int x=-36;x<=36;x+=4){pieces.add(new Piece(Type.WALL,x,-36,3.5f,.45f,1.8f,r.nextInt(12)));pieces.add(new Piece(Type.WALL,x,36,3.5f,.45f,1.8f,r.nextInt(12)));}
        for(int z=-32;z<=32;z+=4){pieces.add(new Piece(Type.WALL,-36,z,.45f,3.5f,1.8f,r.nextInt(12)));pieces.add(new Piece(Type.WALL,36,z,.45f,3.5f,1.8f,r.nextInt(12)));}
    }
}
