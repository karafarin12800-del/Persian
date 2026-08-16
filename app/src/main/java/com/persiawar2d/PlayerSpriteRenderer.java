package com.persiawar2d;

import android.content.Context;
import android.content.res.AssetManager;
import android.graphics.*;
import java.io.InputStream;

/** Loads and animates the supplied king sprite sheet from app assets. */
public final class PlayerSpriteRenderer {
    private static final int IDLE=0, WALK=1, RUN=2, ATTACK=3, HURT=4, DIE=5;
    private static final int[] FRAME_COUNTS={3,3,2,3};
    private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final Bitmap[][][] frames=new Bitmap[4][6][3];
    private long stateStarted;
    private int lastState=-1;
    private boolean loaded;

    public boolean load(Context context){
        if(loaded)return true;
        try{
            AssetManager am=context.getAssets();
            BitmapFactory.Options bounds=new BitmapFactory.Options();
            bounds.inJustDecodeBounds=true;
            InputStream meta=am.open("player_king_sheet.png");
            BitmapFactory.decodeStream(meta,null,bounds);
            meta.close();
            if(bounds.outWidth<=0||bounds.outHeight<=0)return false;
            int sample=1;
            while(bounds.outWidth/sample>1700||bounds.outHeight/sample>1200)sample*=2;
            BitmapFactory.Options opts=new BitmapFactory.Options();
            opts.inSampleSize=sample;
            opts.inPreferredConfig=Bitmap.Config.ARGB_8888;
            InputStream in=am.open("player_king_sheet.png");
            Bitmap sheet=BitmapFactory.decodeStream(in,null,opts);
            in.close();
            if(sheet==null)return false;
            buildFrames(sheet);
            sheet.recycle();
            loaded=true;
            return true;
        }catch(Exception ignored){return false;}
    }

    public void resetAnimation(){lastState=-1;stateStarted=0;}

    public boolean draw(Canvas c,float x,float baseline,float width,int direction,int state,float speed){
        if(!loaded)return false;
        int dir=Math.max(0,Math.min(3,direction));
        int st=Math.max(IDLE,Math.min(DIE,state));
        if(st!=lastState){lastState=st;stateStarted=System.currentTimeMillis();}
        int count=FRAME_COUNTS[dir];
        long elapsed=System.currentTimeMillis()-stateStarted;
        int frame=(st==DIE||st==HURT)?Math.min(count-1,(int)(elapsed/150L)):
                (int)((elapsed/Math.max(70L,(long)(130f/Math.max(.5f,speed))))%count);
        Bitmap bitmap=frames[dir][st][frame];
        if(bitmap==null)return false;
        float h=width*(bitmap.getHeight()/(float)Math.max(1,bitmap.getWidth()));
        RectF dst=new RectF(x-width*.5f,baseline-h,x+width*.5f,baseline);
        c.drawBitmap(bitmap,null,dst,paint);
        return true;
    }

    private void buildFrames(Bitmap sheet){
        float sx=sheet.getWidth()/1536f,sy=sheet.getHeight()/1024f;
        int[][][] xs={
            {{90,212,335,461},{90,212,335,461},{90,212,335,461},{90,212,335,461},{90,212,335,461},{90,212,335,461}},
            {{486,605,725,844},{486,605,725,844},{486,605,725,844},{486,605,725,844},{486,605,725,844},{486,605,725,844}},
            {{866,1004,1148},{866,1004,1148},{866,1004,1148},{866,1004,1148},{866,1004,1148},{866,1004,1148}},
            {{1171,1291,1409,1525},{1171,1291,1409,1525},{1171,1291,1409,1525},{1171,1291,1409,1525},{1171,1291,1409,1525},{1171,1291,1409,1525}}
        };
        int[] y0={43,199,355,507,663,821};
        int[] y1={189,344,498,654,812,967};
        for(int dir=0;dir<4;dir++)for(int state=0;state<6;state++)for(int f=0;f<FRAME_COUNTS[dir];f++){
            int left=Math.round(xs[dir][state][f]*sx)+2;
            int right=Math.round(xs[dir][state][f+1]*sx)-2;
            int top=Math.round(y0[state]*sy)+2;
            int bottom=Math.round(y1[state]*sy)-2;
            left=Math.max(0,Math.min(left,sheet.getWidth()-1));
            right=Math.max(left+1,Math.min(right,sheet.getWidth()));
            top=Math.max(0,Math.min(top,sheet.getHeight()-1));
            bottom=Math.max(top+1,Math.min(bottom,sheet.getHeight()));
            Bitmap frame=Bitmap.createBitmap(sheet,left,top,right-left,bottom-top).copy(Bitmap.Config.ARGB_8888,true);
            makeBackgroundTransparent(frame);
            frames[dir][state][f]=frame;
        }
    }

    private static boolean isBackground(int color){
        int a=Color.alpha(color),r=Color.red(color),g=Color.green(color),b=Color.blue(color);
        return a>0&&r<38&&g<38&&b<38;
    }

    private static void makeBackgroundTransparent(Bitmap bitmap){
        int w=bitmap.getWidth(),h=bitmap.getHeight(),n=w*h;
        int[] pixels=new int[n];bitmap.getPixels(pixels,0,w,0,0,w,h);
        boolean[] seen=new boolean[n];
        int[] stack=new int[Math.max(32,n*2+16)];
        int sp=0;

        for(int x=0;x<w;x++){
            int a=x,b=(h-1)*w+x;
            if(isBackground(pixels[a])&&!seen[a]){seen[a]=true;stack[sp++]=a;}
            if(isBackground(pixels[b])&&!seen[b]){seen[b]=true;stack[sp++]=b;}
        }
        for(int y=1;y<h-1;y++){
            int l=y*w,r=l+w-1;
            if(isBackground(pixels[l])&&!seen[l]){seen[l]=true;stack[sp++]=l;}
            if(isBackground(pixels[r])&&!seen[r]){seen[r]=true;stack[sp++]=r;}
        }

        while(sp>0){
            int idx=stack[--sp];
            pixels[idx]&=0x00FFFFFF;
            int x=idx%w;
            if(x>0){int q=idx-1;if(!seen[q]&&isBackground(pixels[q])){seen[q]=true;stack[sp++]=q;}}
            if(x<w-1){int q=idx+1;if(!seen[q]&&isBackground(pixels[q])){seen[q]=true;stack[sp++]=q;}}
            if(idx>=w){int q=idx-w;if(!seen[q]&&isBackground(pixels[q])){seen[q]=true;stack[sp++]=q;}}
            if(idx<n-w){int q=idx+w;if(!seen[q]&&isBackground(pixels[q])){seen[q]=true;stack[sp++]=q;}}
        }
        bitmap.setPixels(pixels,0,w,0,0,w,h);
    }

    public static int stateFor(boolean moving,boolean running,boolean attacking,boolean hurt,boolean dead){
        if(dead)return DIE;if(hurt)return HURT;if(attacking)return ATTACK;if(running)return RUN;if(moving)return WALK;return IDLE;
    }
}
