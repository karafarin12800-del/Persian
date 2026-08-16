package com.persiawar2d;

import android.content.Context;
import android.graphics.*;
import java.io.InputStream;

/**
 * The playable world is one large 6000x6000 square. The artwork is a single
 * non-repeating square atlas supplied with the project, so the visible camera
 * window follows the player while the rest of the map stays off-screen.
 */
public final class WorldRenderer {
    public static final float WORLD_SIZE=6000f;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final Bitmap worldMap;
    private final Rect src=new Rect();
    private final RectF dst=new RectF();

    // Approximate building blocks used only for the obstruction fade. The
    // artwork itself is never replaced by vector houses/roads.
    private final RectF[] blockers = {
        r(430,520,900,900), r(1530,520,2050,900), r(2250,520,2800,940),
        r(3900,520,4460,980), r(4550,620,5000,930), r(520,2050,1050,2440),
        r(1500,2050,2100,2480), r(2200,1900,2700,2320), r(3600,2050,4250,2450),
        r(4450,2100,5000,2500), r(5200,1900,5700,2400), r(500,3350,1050,3720),
        r(1500,3250,2050,3620), r(2850,3500,3500,3920), r(3650,3500,4200,3900),
        r(4700,3550,5300,3970), r(5200,4200,5700,4570), r(650,5000,1250,5400),
        r(1750,5000,2300,5420), r(2900,5000,3500,5400), r(3850,5100,4450,5450),
        r(4750,5100,5300,5470)
    };

    public WorldRenderer(Context context){
        Bitmap b=null;
        try(InputStream in=context.getAssets().open("world_map_square_2048.png")){
            BitmapFactory.Options o=new BitmapFactory.Options();
            o.inScaled=false;
            b=BitmapFactory.decodeStream(in,null,o);
        }catch(Exception ignored){}
        worldMap=b;
        if(b!=null)src.set(0,0,b.getWidth(),b.getHeight());
    }

    public void draw(Canvas c,float playerX,float playerY,float scale,float viewW,float viewH,float hudH){
        c.drawColor(Color.rgb(112,150,78));
        if(worldMap==null)return;

        float ox=viewW*.5f-playerX*scale;
        float oy=hudH+(viewH-hudH)*.5f-playerY*scale;
        dst.set(ox,oy,ox+WORLD_SIZE*scale,oy+WORLD_SIZE*scale);
        p.setAlpha(255);
        c.drawBitmap(worldMap,src,dst,p);

        drawBuildingFade(c,playerX,playerY,scale,ox,oy);
    }

    private void drawBuildingFade(Canvas c,float px,float py,float scale,float ox,float oy){
        // Fade only objects between the camera/player line and the player.
        // This removes the old hard aim-line and gives the player a clear view.
        float camX=px,camY=py-900;
        float dx=px-camX,dy=py-camY,len2=Math.max(1,dx*dx+dy*dy);
        for(RectF b:blockers){
            float bx=b.centerX(),by=b.centerY();
            float t=((bx-camX)*dx+(by-camY)*dy)/len2;
            if(t<0||t>1)continue;
            float cx=camX+t*dx,cy=camY+t*dy;
            if(cx>b.left-45&&cx<b.right+45&&cy>b.top-45&&cy<b.bottom+45){
                RectF screen=new RectF(
                    ox+(b.left-18)*scale,
                    oy+(b.top-18)*scale,
                    ox+(b.right+18)*scale,
                    oy+(b.bottom+18)*scale);
                p.setStyle(Paint.Style.FILL);
                p.setColor(Color.argb(155,112,150,78));
                c.drawRoundRect(screen,18*scale,18*scale,p);
            }
        }
    }

    private static RectF r(float l,float t,float rr,float bb){return new RectF(l,t,rr,bb);}
    public boolean ready(){return worldMap!=null;}
}
