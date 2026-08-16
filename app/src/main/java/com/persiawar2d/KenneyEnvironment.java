package com.persiawar2d;

import android.content.res.AssetManager;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import java.io.InputStream;
import java.util.*;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/** Loads only real project assets; no replacement bitmap art is generated. */
public final class KenneyEnvironment {
    private final List<Bitmap> buildings=new ArrayList<>();
    private final List<Bitmap> roads=new ArrayList<>();
    private final List<Bitmap> trees=new ArrayList<>();
    private final List<Bitmap> grass=new ArrayList<>();
    private boolean loaded;
    private static final Set<Integer> ROAD_TILES=new HashSet<>(Arrays.asList(23,30,31,38,46,54,74,80,81,82,87,88,89,94,95,96,97,101,102,103,104,105,107,108,109,110,111,112,113,114,115,117,118,119,120,121,122,123,124,125,126,127));

    public void load(android.content.Context context){
        if(loaded)return;
        AssetManager am=context.getAssets();
        loadZip(am,"kenney_isometric-buildings.zip",true);
        loadZip(am,"kenney_isometric-landscape.zip",false);
        loadZip(am,"isometric-plant-pack.zip",false);
        scanAssets(am,"");
        loaded=true;
    }

    private void scanAssets(AssetManager am,String dir){
        try{
            String[] names=am.list(dir);if(names==null)return;
            for(String name:names){
                String path=dir.length()==0?name:dir+"/"+name;
                String low=path.toLowerCase(Locale.US);
                String[] child=null;try{child=am.list(path);}catch(Exception ignored){}
                if(child!=null&&child.length>0){scanAssets(am,path);continue;}
                if(!low.endsWith(".png"))continue;
                Bitmap b=decode(am,path);if(b==null)continue;
                if(roads.size()<24&&contains(low,"road","street","asphalt","path","intersection","tile_road"))roads.add(b);
                else if(trees.size()<24&&contains(low,"tree","pine","palm","plant","vegetation"))trees.add(b);
                else if(grass.size()<24&&contains(low,"grass","bush","shrub","flower"))grass.add(b);
                else b.recycle();
            }
        }catch(Exception ignored){}
    }

    private void loadZip(AssetManager am,String name,boolean buildingZip){
        try(InputStream raw=am.open(name);ZipInputStream zip=new ZipInputStream(raw)){
            ZipEntry e;
            while((e=zip.getNextEntry())!=null){
                if(e.isDirectory()||!e.getName().toLowerCase(Locale.US).endsWith(".png"))continue;
                String n=e.getName().toLowerCase(Locale.US);
                if(!buildingZip&&n.contains("landscapetiles_")){
                    int idx=parseTileIndex(n);
                    if(!ROAD_TILES.contains(idx))continue;
                    Bitmap b=BitmapFactory.decodeStream(zip);if(b!=null&&roads.size()<24)roads.add(b);else if(b!=null)b.recycle();
                    continue;
                }
                if(!buildingZip&&n.contains("isometric tiles/")){
                    boolean tree=contains(n,"tree","pine","palm","bamboo","shrub");
                    boolean g=contains(n,"grass","grasses","weed","flower","plant");
                    if(!tree&&!g)continue;
                    Bitmap b=BitmapFactory.decodeStream(zip);if(b==null)continue;
                    if(tree&&trees.size()<24)trees.add(b);else if(g&&grass.size()<24)grass.add(b);else b.recycle();
                    continue;
                }
                Bitmap b=BitmapFactory.decodeStream(zip);if(b==null)continue;
                if(buildingZip){
                    if(n.contains("buildingtile_")||n.contains("buildingtiles_"))buildings.add(b);else b.recycle();
                }else if(roads.size()<24&&contains(n,"road","street","asphalt","path","intersection"))roads.add(b);
                else if(trees.size()<24&&contains(n,"tree","pine","palm","vegetation"))trees.add(b);
                else if(grass.size()<24&&contains(n,"grass","bush","shrub","flower","plant"))grass.add(b);
                else b.recycle();
            }
        }catch(Exception ignored){}
    }

    private int parseTileIndex(String n){int p=n.lastIndexOf('_'),dot=n.lastIndexOf('.');if(p<0||dot<=p)return -1;try{return Integer.parseInt(n.substring(p+1,dot));}catch(Exception ignored){return -1;}}
    private Bitmap decode(AssetManager am,String path){try(InputStream in=am.open(path)){return BitmapFactory.decodeStream(in);}catch(Exception ignored){return null;}}
    private static boolean contains(String s,String...keys){for(String k:keys)if(s.contains(k))return true;return false;}
    public boolean isLoaded(){return loaded;}
    public Bitmap building(int i){return pick(buildings,i);}
    public Bitmap road(int i){return pick(roads,i);}
    public Bitmap tree(int i){return pick(trees,i);}
    public Bitmap grass(int i){return pick(grass,i);}
    private static Bitmap pick(List<Bitmap> list,int i){return list.isEmpty()?null:list.get(Math.floorMod(i,list.size()));}
}
