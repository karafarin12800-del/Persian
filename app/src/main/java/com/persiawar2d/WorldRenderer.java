package com.persiawar2d;

import android.content.Context;
import android.graphics.*;
import java.io.InputStream;

/**
 * Real-asset-only 6000x6000 square world renderer.
 * The camera window around the player is shown; procedural green-map fallback
 * is deliberately disabled so missing visual assets can never silently turn
 * into artificial roads/houses.
 */
public final class WorldRenderer {
    public static final float WORLD_SIZE=6000f;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final Bitmap worldMap;
    private final Rect src=new Rect();
    private final RectF dst=new RectF();

    public WorldRenderer(Context context){
        worldMap=loadMap(context);
        if(worldMap!=null)src.set(0,0,worldMap.getWidth(),worldMap.getHeight());
    }

    private Bitmap loadMap(Context context){
        String[] names={
            "world_texture_real_square.png",
            "world_map_square_2048.png",
            "references/1000121867.png",
            "references/1000121907.png",
            "references/world_texture_ref.jpg"
        };
        for(String name:names){
            try(InputStream in=context.getAssets().open(name)){
                BitmapFactory.Options o=new BitmapFactory.Options();
                o.inScaled=false;
                Bitmap b=BitmapFactory.decodeStream(in,null,o);
                if(b!=null && b.getWidth()>64 && b.getHeight()>64)return b;
            }catch(Exception ignored){}
        }
        return null;
    }

    public void draw(Canvas c,float playerX,float playerY,float scale,float viewW,float viewH,float hudH){
        c.drawColor(Color.rgb(18,24,20));
        if(worldMap==null)return;
        float ox=viewW*.5f-playerX*scale;
        float oy=hudH+(viewH-hudH)*.5f-playerY*scale;
        dst.set(ox,oy,ox+WORLD_SIZE*scale,oy+WORLD_SIZE*scale);
        p.setAlpha(255);
        c.drawBitmap(worldMap,src,dst,p);
    }

    public boolean ready(){return worldMap!=null;}
}
