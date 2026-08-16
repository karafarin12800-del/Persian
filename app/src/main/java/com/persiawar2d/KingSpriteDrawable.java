package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.BitmapRegionDecoder;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Rect;
import android.graphics.drawable.Drawable;
import java.io.InputStream;

/**
 * Uses the user's original 6144x4096 artwork.
 * Sheet layout: 6 animation frames x 4 directions.
 * Rows: down, left, right, up.
 */
public final class KingSpriteDrawable extends Drawable {
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final BitmapRegionDecoder decoder;
    private final Rect source = new Rect();
    private Bitmap frame;
    private int direction=0,frameIndex=0;

    public KingSpriteDrawable(Context context){
        BitmapRegionDecoder d=null;
        try(InputStream in=context.getAssets().open("player/king_sprite_sheet.png")){
            d=BitmapRegionDecoder.newInstance(in,false);
        }catch(Exception ignored){}
        decoder=d;
        setState(0,0);
        setAlpha(255);
    }

    public void setState(int direction,int frameIndex){
        direction=Math.max(0,Math.min(3,direction));
        frameIndex=Math.max(0,Math.min(5,frameIndex));
        if(decoder==null)return;
        if(this.direction==direction && this.frameIndex==frameIndex && frame!=null)return;
        this.direction=direction;this.frameIndex=frameIndex;
        int fw=decoder.getWidth()/6;
        int fh=decoder.getHeight()/4;
        source.set(frameIndex*fw,direction*fh,(frameIndex+1)*fw,(direction+1)*fh);
        Bitmap old=frame;
        BitmapFactory.Options o=new BitmapFactory.Options();
        o.inScaled=false;
        frame=decoder.decodeRegion(source,o);
        if(old!=null&&!old.isRecycled())old.recycle();
        invalidateSelf();
    }

    @Override public void draw(Canvas canvas){
        if(frame==null)return;
        Rect b=getBounds();
        paint.setAlpha(255); // never render the player's artwork semi-transparent
        canvas.drawBitmap(frame,null,b,paint);
    }
    @Override public void setAlpha(int alpha){paint.setAlpha(alpha);}
    @Override public int getAlpha(){return paint.getAlpha();}
    @Override public void setColorFilter(android.graphics.ColorFilter filter){paint.setColorFilter(filter);}
    @Override public int getOpacity(){return android.graphics.PixelFormat.TRANSLUCENT;}
    @Override public int getIntrinsicWidth(){return 1024;}
    @Override public int getIntrinsicHeight(){return 1024;}
}
