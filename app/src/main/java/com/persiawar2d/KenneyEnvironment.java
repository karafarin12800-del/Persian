package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/** Reads the existing Kenney isometric-buildings ZIP. It does not create replacement art. */
public final class KenneyEnvironment {
    private final List<Bitmap> buildings=new ArrayList<>(),roads=new ArrayList<>(),trees=new ArrayList<>(),grass=new ArrayList<>();
    private boolean loaded;
    public void load(Context context){
        if(loaded)return;
        try(InputStream raw=context.getAssets().open("kenney_isometric-buildings.zip");ZipInputStream zip=new ZipInputStream(raw)){
            ZipEntry e;
            while((e=zip.getNextEntry())!=null){
                if(e.isDirectory()||!e.getName().toLowerCase(Locale.US).endsWith(".png"))continue;
                String n=e.getName().toLowerCase(Locale.US);Bitmap b=BitmapFactory.decodeStream(zip);if(b==null)continue;
                if(buildings.size()<24&&(n.contains("buildingtile_")||n.contains("buildingtiles_")))buildings.add(b);
                else if(roads.size()<6&&has(n,"road","street","path"))roads.add(b);
                else if(trees.size()<8&&has(n,"tree","pine","palm"))trees.add(b);
                else if(grass.size()<8&&has(n,"grass","bush","shrub","plant"))grass.add(b);
                else if(buildings.size()<24&&has(n,"house","building","home","shop","store","tower"))buildings.add(b);
                else b.recycle();
            }
        }catch(Exception ignored){}
        loaded=true;
    }
    private static boolean has(String s,String... keys){for(String k:keys)if(s.contains(k))return true;return false;}
    public boolean isLoaded(){return loaded;}
    public Bitmap building(int i){return pick(buildings,i);}public Bitmap road(int i){return pick(roads,i);}public Bitmap tree(int i){return pick(trees,i);}public Bitmap grass(int i){return pick(grass,i);}
    private static Bitmap pick(List<Bitmap> list,int i){return list.isEmpty()?null:list.get(Math.floorMod(i,list.size()));}
}
