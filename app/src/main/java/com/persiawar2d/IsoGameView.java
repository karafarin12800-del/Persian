package com.persiawar2d;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.RectF;
import android.graphics.drawable.Drawable;

/** Main gameplay view using the new isometric renderer while retaining existing gameplay state. */
public final class IsoGameView extends MainActivity.GameView {
    private final IsoWorldRenderer iso;

    public IsoGameView(Context context) {
        super(context);
        iso = new IsoWorldRenderer(context);
    }

    @Override protected void onDraw(Canvas c) {
        long now = System.currentTimeMillis();
        float dt = Math.min(.033f, Math.max(.001f, (now-lastFrameAt)/1000f));
        lastFrameAt = now;
        tick(now, dt);
        float s = cameraScale();
        float centerX = getWidth()*.5f;
        float centerY = HUD_H + (getHeight()-HUD_H)*.48f;
        iso.draw(c, px, py, centerX, centerY, s, HUD_H);

        drawActors(c, s, centerX, centerY);
        drawHud(c);
        drawControls(c);
        if (gameOver) drawGameOver(c);
        postInvalidateOnAnimation();
    }

    private float sx(float x, float y, float centerX, float s) {
        return iso.screenX(x,y,px,py,centerX,s);
    }
    private float sy(float x, float y, float centerY, float s) {
        return iso.screenY(x,y,px,py,centerY,s);
    }

    private void drawActors(Canvas c, float s, float centerX, float centerY) {
        // Ground objects are depth sorted so buildings can naturally cover actors.
        java.util.ArrayList<Actor> actors = new java.util.ArrayList<>();
        for(Pickup q:pickups) actors.add(new Actor(q.x,q.y,0,q));
        for(ThrownGrenade g:thrownGrenades) actors.add(new Actor(g.x,g.y,1,g));
        for(Bullet b:bullets) actors.add(new Actor(b.x,b.y,2,b));
        for(Enemy e:enemies) if(e.hp>0) actors.add(new Actor(e.x,e.y,3,e));
        actors.add(new Actor(px,py,4,null));
        actors.sort((a,b)->{int d=Float.compare(a.x+a.y,b.x+b.y);return d!=0?d:Integer.compare(a.layer,b.layer);});
        for(Actor a:actors){
            if(a.object instanceof Pickup) drawIsoPickup(c,(Pickup)a.object,s,centerX,centerY);
            else if(a.object instanceof ThrownGrenade) drawIsoGrenade(c,(ThrownGrenade)a.object,s,centerX,centerY);
            else if(a.object instanceof Bullet) drawIsoBullet(c,(Bullet)a.object,s,centerX,centerY);
            else if(a.object instanceof Enemy) drawIsoEnemy(c,(Enemy)a.object,s,centerX,centerY);
            else drawIsoPlayer(c,s,centerX,centerY);
        }
        if(System.currentTimeMillis()<explosionUntil){
            float x=sx(explosionX,explosionY,centerX,s),y=sy(explosionX,explosionY,centerY,s);
            float left=Math.max(0,explosionUntil-System.currentTimeMillis()),a=left/360f;
            p.setColor(((int)(120*a)<<24)|0xF2B84B);c.drawCircle(x,y,110*s*(1f-a*.35f),p);
            p.setColor(((int)(170*a)<<24)|0xFFE5A1);c.drawCircle(x,y,58*s*(1f-a*.2f),p);
        }
    }

    private void drawIsoPlayer(Canvas c,float s,float cx,float cy){
        float x=sx(px,py,cx,s), y=sy(px,py,cy,s);
        p.setStyle(android.graphics.Paint.Style.FILL);p.setColor(0x55000000);c.drawOval(new RectF(x-38*s,y-4*s,x+38*s,y+12*s),p);
        king.setState(playerDir,playerAction,playerFrame);king.setAlpha(255);
        int w=Math.max(76,Math.round(116*s)),h=Math.max(140,Math.round(210*s));
        king.setBounds((int)(x-w*.5f),(int)(y-h),(int)(x+w*.5f),(int)y);king.draw(c);
        if(shield>0){p.setStyle(android.graphics.Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,3*s));p.setColor(0xAA52DFFF);c.drawOval(x-52*s,y-78*s,x+52*s,y+10*s,p);p.setStyle(android.graphics.Paint.Style.FILL);}
    }

    private void drawIsoEnemy(Canvas c,Enemy e,float s,float cx,float cy){
        if(enemyArt==null)return;float x=sx(e.x,e.y,cx,s),y=sy(e.x,e.y,cy,s);int size=Math.round((e.type==3?136:(e.type==2?122:110))*s);
        enemyArt.setAlpha(255);enemyArt.setBounds((int)(x-size*.5f),(int)(y-size),(int)(x+size*.5f),(int)y);enemyArt.draw(c);
        float bw=64*s,bh=Math.max(5,7*s);p.setColor(0xB4141414);c.drawRoundRect(x-bw*.5f,y-size-16*s,x+bw*.5f,y-size-16*s+bh,bh,bh,p);float max=e.type==3?120:(e.type==2?70:45);p.setColor(Color.rgb(196,55,45));c.drawRoundRect(x-bw*.5f,y-size-16*s,x-bw*.5f+bw*Math.max(0,e.hp/max),y-size-16*s+bh,bh,bh,p);
    }
    private void drawIsoBullet(Canvas c,Bullet b,float s,float cx,float cy){float x=sx(b.x,b.y,cx,s),y=sy(b.x,b.y,cy,s);p.setColor(b.player?Color.rgb(255,218,87):Color.rgb(255,80,65));c.drawCircle(x,y,Math.max(3,5*s),p);}
    private void drawIsoGrenade(Canvas c,ThrownGrenade g,float s,float cx,float cy){float x=sx(g.x,g.y,cx,s),y=sy(g.x,g.y,cy,s);p.setColor(Color.rgb(61,99,62));c.drawCircle(x,y-10*s,10*s,p);}
    private void drawIsoPickup(Canvas c,Pickup q,float s,float cx,float cy){float x=sx(q.x,q.y,cx,s),y=sy(q.x,q.y,cy,s);p.setColor(0x44000000);c.drawOval(new RectF(x-22*s,y-2*s,x+22*s,y+10*s),p);p.setColor(q.type==Pickup.AMMO?0xffd9b146:q.type==Pickup.GRENADE?0xff3d693e:0xffcd3f3a);c.drawRoundRect(x-16*s,y-24*s,x+16*s,y,7*s,7*s,p);}

    @Override void setAimFromScreen(float sx,float sy){
        float[] w=iso.screenToWorld(sx,sy,px,py,getWidth()*.5f,HUD_H+(getHeight()-HUD_H)*.48f,cameraScale());
        aimX=w[0];aimY=w[1];
    }
    static final class Actor{final float x,y;final int layer;final Object object;Actor(float x,float y,int layer,Object object){this.x=x;this.y=y;this.layer=layer;this.object=object;}}
}
