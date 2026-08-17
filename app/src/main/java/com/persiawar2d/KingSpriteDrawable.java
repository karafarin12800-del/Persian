package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.BitmapRegionDecoder;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Rect;
import android.graphics.drawable.Drawable;
import java.io.InputStream;

/** Player sprite renderer: 6 actions x 4 directions, 3 frames per 1024x1024 action cell. */
public final class KingSpriteDrawable extends Drawable {
    public static final int ACTION_IDLE=0, ACTION_WALK=1, ACTION_RUN=2, ACTION_ATTACK=3, ACTION_HURT=4, ACTION_DIE=5;
    public static final int FRAME_COUNT=3;
    private static final int ACTION_COUNT=6, DIRECTION_COUNT=4;
    private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final BitmapRegionDecoder decoder;
    private final Bitmap[][][] frames=new Bitmap[DIRECTION_COUNT][ACTION_COUNT][FRAME_COUNT];
    private Bitmap frame;

    public KingSpriteDrawable(Context context){
        BitmapRegionDecoder d=null;
        try(InputStream in=context.getAssets().open("player/king_sprite_sheet.png")){d=BitmapRegionDecoder.newInstance(in,false);}catch(Exception ignored){}
        decoder=d;setAlpha(255);setState(0,ACTION_IDLE,0);
    }

    public void setState(int direction,int action,int frameIndex){
        direction=clamp(direction,0,DIRECTION_COUNT-1);action=clamp(action,0,ACTION_COUNT-1);frameIndex=clamp(frameIndex,0,FRAME_COUNT-1);
        if(decoder==null)return;
        if(frames[direction][action][frameIndex]==null)decodeFrame(direction,action,frameIndex);
        frame=frames[direction][action][frameIndex];invalidateSelf();
    }
    public void setState(int direction,int frameIndex){setState(direction,ACTION_WALK,frameIndex);}

    private void decodeFrame(int direction,int action,int frameIndex){
        int sw=decoder.getWidth(),sh=decoder.getHeight();
        int cellW=sw/ACTION_COUNT,cellH=sh/DIRECTION_COUNT;
        int left=action*cellW+Math.round(frameIndex*cellW/(float)FRAME_COUNT);
        int right=action*cellW+Math.round((frameIndex+1)*cellW/(float)FRAME_COUNT);
        Rect region=new Rect(left,direction*cellH,right,direction*cellH+cellH);
        BitmapFactory.Options o=new BitmapFactory.Options();o.inScaled=false;o.inPreferredConfig=Bitmap.Config.ARGB_8888;
        Bitmap raw=decoder.decodeRegion(region,o);if(raw==null)return;
        Bitmap clean=removeEdgeBlackMatte(raw);if(clean!=raw&&!raw.isRecycled())raw.recycle();
        Rect b=foregroundBounds(clean);
        if(b==null||b.width()<8||b.height()<20){frames[direction][action][frameIndex]=clean;return;}
        int pad=14;int l=Math.max(0,b.left-pad),t=Math.max(0,b.top-pad),r=Math.min(clean.getWidth(),b.right+pad),bot=Math.min(clean.getHeight(),b.bottom+pad);
        Bitmap cropped=Bitmap.createBitmap(clean,l,t,r-l,bot-t);if(clean!=cropped&&!clean.isRecycled())clean.recycle();
        frames[direction][action][frameIndex]=cropped;
    }

    private Bitmap removeEdgeBlackMatte(Bitmap src){
        Bitmap b=src.copy(Bitmap.Config.ARGB_8888,true);if(b==null)return src;
        int w=b.getWidth(),h=b.getHeight();int[] px=new int[w*h];b.getPixels(px,0,w,0,0,w,h);
        boolean[] cut=new boolean[px.length];int[] q=new int[px.length];int head=0,tail=0;
        for(int x=0;x<w;x++){int a=x,z=(h-1)*w+x;if(isMatte(px[a])){cut[a]=true;q[tail++]=a;}if(isMatte(px[z])&&!cut[z]){cut[z]=true;q[tail++]=z;}}
        for(int y=1;y<h-1;y++){int a=y*w,z=a+w-1;if(isMatte(px[a])&&!cut[a]){cut[a]=true;q[tail++]=a;}if(isMatte(px[z])&&!cut[z]){cut[z]=true;q[tail++]=z;}}
        while(head<tail){int i=q[head++],x=i%w,y=i/w;
            if(x>0){int n=i-1;if(!cut[n]&&isMatte(px[n])){cut[n]=true;q[tail++]=n;}}
            if(x+1<w){int n=i+1;if(!cut[n]&&isMatte(px[n])){cut[n]=true;q[tail++]=n;}}
            if(y>0){int n=i-w;if(!cut[n]&&isMatte(px[n])){cut[n]=true;q[tail++]=n;}}
            if(y+1<h){int n=i+w;if(!cut[n]&&isMatte(px[n])){cut[n]=true;q[tail++]=n;}}
        }
        for(int i=0;i<px.length;i++)if(cut[i])px[i]=Color.TRANSPARENT;b.setPixels(px,0,w,0,0,w,h);return b;
    }
    private Rect foregroundBounds(Bitmap src){int w=src.getWidth(),h=src.getHeight();int[] px=new int[w*h];src.getPixels(px,0,w,0,0,w,h);int minX=w,minY=h,maxX=-1,maxY=-1;for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(isForeground(px[y*w+x])){if(x<minX)minX=x;if(x>maxX)maxX=x;if(y<minY)minY=y;if(y>maxY)maxY=y;}return maxX<0?null:new Rect(minX,minY,maxX+1,maxY+1);}
    private boolean isMatte(int c){return Color.alpha(c)>0&&Color.red(c)<28&&Color.green(c)<28&&Color.blue(c)<28;}
    private boolean isForeground(int c){return Color.alpha(c)>0&&!isMatte(c);}
    private int clamp(int v,int a,int b){return Math.max(a,Math.min(b,v));}
    @Override public void draw(Canvas c){if(frame!=null){paint.setAlpha(255);c.drawBitmap(frame,null,getBounds(),paint);}}
    @Override public void setAlpha(int a){paint.setAlpha(a);}@Override public int getAlpha(){return paint.getAlpha();}
    @Override public void setColorFilter(android.graphics.ColorFilter f){paint.setColorFilter(f);}@Override public int getOpacity(){return android.graphics.PixelFormat.TRANSLUCENT;}
    @Override public int getIntrinsicWidth(){return 342;}@Override public int getIntrinsicHeight(){return 1024;}
}
