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
import java.util.ArrayList;
import java.util.Comparator;

/** Exact player-frame extraction: 6 actions x 4 directions, 3 poses inside each 1024x1024 cell. */
public final class KingSpriteDrawable extends Drawable {
    public static final int ACTION_IDLE=0, ACTION_WALK=1, ACTION_RUN=2, ACTION_ATTACK=3, ACTION_HURT=4, ACTION_DIE=5;
    public static final int FRAME_COUNT=3;
    private static final int ACTION_COUNT=6, DIRECTION_COUNT=4;
    private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final BitmapRegionDecoder decoder;
    private final Bitmap[][][] frames=new Bitmap[DIRECTION_COUNT][ACTION_COUNT][FRAME_COUNT];
    private final Rect source=new Rect();
    private Bitmap frame;

    public KingSpriteDrawable(Context context){
        BitmapRegionDecoder d=null;
        try(InputStream in=context.getAssets().open("player/king_sprite_sheet.png")){d=BitmapRegionDecoder.newInstance(in,false);}catch(Exception ignored){}
        decoder=d; setAlpha(255); setState(0,ACTION_IDLE,0);
    }

    public void setState(int direction,int action,int frameIndex){
        direction=clamp(direction,0,DIRECTION_COUNT-1); action=clamp(action,0,ACTION_COUNT-1); frameIndex=clamp(frameIndex,0,FRAME_COUNT-1);
        if(decoder==null)return;
        if(frames[direction][action][frameIndex]==null)decodeCell(direction,action);
        frame=frames[direction][action][frameIndex]; invalidateSelf();
    }
    public void setState(int direction,int frameIndex){setState(direction,ACTION_WALK,frameIndex);}

    private void decodeCell(int direction,int action){
        int sw=decoder.getWidth(),sh=decoder.getHeight();
        int l=Math.round(action*sw/(float)ACTION_COUNT),r=Math.round((action+1)*sw/(float)ACTION_COUNT);
        int t=Math.round(direction*sh/(float)DIRECTION_COUNT),b=Math.round((direction+1)*sh/(float)DIRECTION_COUNT);
        source.set(l,t,r,b);
        BitmapFactory.Options o=new BitmapFactory.Options(); o.inScaled=false; o.inPreferredConfig=Bitmap.Config.ARGB_8888;
        Bitmap cell=decoder.decodeRegion(source,o); if(cell==null)return;
        Bitmap clean=removeEdgeBlackMatte(cell); if(clean!=cell&&!cell.isRecycled())cell.recycle();
        ArrayList<Rect> poses=findPoseBands(clean);
        if(poses.size()!=FRAME_COUNT){
            // Never cut a neighboring pose into a fake frame. If detection fails, keep one clean pose.
            Rect one=boundingForeground(clean); if(one==null)one=new Rect(0,0,clean.getWidth(),clean.getHeight());
            poses.clear(); poses.add(one); poses.add(one); poses.add(one);
        }
        int maxW=1,maxH=1; for(Rect q:poses){maxW=Math.max(maxW,q.width());maxH=Math.max(maxH,q.height());}
        maxW=Math.min(1024,maxW+24); maxH=Math.min(1024,maxH+24);
        for(int i=0;i<3;i++){
            Rect q=poses.get(i); Bitmap out=Bitmap.createBitmap(maxW,maxH,Bitmap.Config.ARGB_8888); Canvas c=new Canvas(out);
            int dx=(maxW-q.width())/2,dy=maxH-q.height()-8; c.drawBitmap(clean,q,new Rect(dx,dy,dx+q.width(),dy+q.height()),paint); frames[direction][action][i]=out;
        }
        if(!clean.isRecycled())clean.recycle();
    }

    /** Groups foreground x-columns into the three actual poses; no 341px grid assumption. */
    private ArrayList<Rect> findPoseBands(Bitmap src){
        int w=src.getWidth(),h=src.getHeight(); int[] px=new int[w*h]; src.getPixels(px,0,w,0,0,w,h);
        int[] count=new int[w];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(isForeground(px[y*w+x]))count[x]++;
        ArrayList<Rect> bands=new ArrayList<>(); int start=-1,last=-1,gap=0;
        for(int x=0;x<w;x++){
            boolean active=count[x]>2;
            if(active){if(start<0)start=x;last=x;gap=0;}
            else if(start>=0){gap++;if(gap>18){bands.add(new Rect(start,0,last+1,h));start=-1;last=-1;gap=0;}}
        }
        if(start>=0)bands.add(new Rect(start,0,last+1,h));
        ArrayList<Rect> cleaned=new ArrayList<>();
        for(Rect band:bands){Rect q=tighten(src,band);if(q!=null&&q.width()>30&&q.height()>50)cleaned.add(q);}
        if(cleaned.size()>3){cleaned.sort(new Comparator<Rect>(){@Override public int compare(Rect a,Rect b){return Integer.compare(b.width()*b.height(),a.width()*a.height());}});while(cleaned.size()>3)cleaned.remove(cleaned.size()-1);}
        cleaned.sort(new Comparator<Rect>(){@Override public int compare(Rect a,Rect b){return Integer.compare(a.centerX(),b.centerX());}});
        return cleaned;
    }

    private Rect tighten(Bitmap src,Rect band){
        int w=src.getWidth(),h=src.getHeight(),minX=band.right,minY=h,maxX=-1,maxY=-1;
        for(int y=0;y<h;y++)for(int x=band.left;x<band.right;x++)if(isForeground(src.getPixel(x,y))){minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);}
        return maxX<0?null:new Rect(minX,minY,maxX+1,maxY+1);
    }
    private Rect boundingForeground(Bitmap src){return tighten(src,new Rect(0,0,src.getWidth(),src.getHeight()));}

    private Bitmap removeEdgeBlackMatte(Bitmap src){
        Bitmap b=src.copy(Bitmap.Config.ARGB_8888,true);if(b==null)return src;int w=b.getWidth(),h=b.getHeight();int[] px=new int[w*h];b.getPixels(px,0,w,0,0,w,h);
        boolean[] cut=new boolean[px.length];int[] q=new int[px.length];int head=0,tail=0;
        for(int x=0;x<w;x++){if(isMatte(px[x])){cut[x]=true;q[tail++]=x;}int i=(h-1)*w+x;if(!cut[i]&&isMatte(px[i])){cut[i]=true;q[tail++]=i;}}
        for(int y=1;y<h-1;y++){int a=y*w,z=a+w-1;if(isMatte(px[a])&&!cut[a]){cut[a]=true;q[tail++]=a;}if(isMatte(px[z])&&!cut[z]){cut[z]=true;q[tail++]=z;}}
        while(head<tail){int i=q[head++],x=i%w,y=i/w;if(x>0){int n=i-1;if(!cut[n]&&isMatte(px[n])){cut[n]=true;q[tail++]=n;}}if(x+1<w){int n=i+1;if(!cut[n]&&isMatte(px[n])){cut[n]=true;q[tail++]=n;}}if(y>0){int n=i-w;if(!cut[n]&&isMatte(px[n])){cut[n]=true;q[tail++]=n;}}if(y+1<h){int n=i+w;if(!cut[n]&&isMatte(px[n])){cut[n]=true;q[tail++]=n;}}}
        for(int i=0;i<px.length;i++)if(cut[i])px[i]=Color.TRANSPARENT;b.setPixels(px,0,w,0,0,w,h);return b;
    }
    private boolean isMatte(int c){return Color.alpha(c)>0&&Color.red(c)<18&&Color.green(c)<18&&Color.blue(c)<18;}
    private boolean isForeground(int c){return Color.alpha(c)>0&&(Color.red(c)>18||Color.green(c)>18||Color.blue(c)>18);}
    private int clamp(int v,int a,int b){return Math.max(a,Math.min(b,v));}
    @Override public void draw(Canvas c){if(frame!=null){paint.setAlpha(255);c.drawBitmap(frame,null,getBounds(),paint);}}
    @Override public void setAlpha(int a){paint.setAlpha(a);} @Override public int getAlpha(){return paint.getAlpha();}
    @Override public void setColorFilter(android.graphics.ColorFilter f){paint.setColorFilter(f);} @Override public int getOpacity(){return android.graphics.PixelFormat.TRANSLUCENT;}
    @Override public int getIntrinsicWidth(){return 342;} @Override public int getIntrinsicHeight(){return 1024;}
}
