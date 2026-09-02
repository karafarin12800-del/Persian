package com.persiawar2d;

import android.app.Activity;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.os.Bundle;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowManager;
import com.persiawar2d.game.GameCore;
import com.persiawar2d.world.WorldMap;

/** Main gameplay screen. All rules come from GameCore; this class only handles input and drawing. */
public final class MainActivity extends Activity {
    @Override public void onCreate(Bundle state){super.onCreate(state);getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN,WindowManager.LayoutParams.FLAG_FULLSCREEN);setContentView(new GameView(this));}

    public static final class GameView extends View {
        private static final float HUD=96f,PITCH=.82f;
        private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
        private final WorldRenderer world;
        private final GameCore core;
        private final KingSpriteDrawable king;
        private final GameCore.Input input=new GameCore.Input();
        private float joystickBaseX,joystickBaseY,joystickX,joystickY;
        private int joystickPointer=-1,firePointer=-1;
        private long lastFrame=System.nanoTime(),actionUntil;
        private int playerAction=KingSpriteDrawable.ACTION_IDLE;
        private boolean paused;

        public GameView(Context c){super(c);world=new WorldRenderer(c);king=new KingSpriteDrawable(c);String skin=c.getSharedPreferences("player",0).getString("skin","classic");core=new GameCore(world.map(),skin);setLayerType(View.LAYER_TYPE_HARDWARE,null);}
        @Override protected void onSizeChanged(int w,int h,int ow,int oh){joystickBaseX=w*.16f;joystickBaseY=h*.80f;joystickX=joystickBaseX;joystickY=joystickBaseY;}
        private float scale(){return Math.min(getWidth()/1900f,Math.max(.62f,(getHeight()-HUD)/1050f));}
        private float cx(){return getWidth()*.5f;}private float cy(){return HUD+(getHeight()-HUD)*.5f;}

        @Override protected void onDraw(Canvas c){super.onDraw(c);long now=System.nanoTime();float dt=Math.min(.04f,Math.max(.001f,(now-lastFrame)/1_000_000_000f));lastFrame=now;if(!paused)update(dt,now);drawGame(c);if(paused)drawPause(c);if(core.gameOver())drawGameOver(c);postInvalidateOnAnimation();}
        private void update(float dt,long now){core.update(dt,input);input.sword=false;input.grenade=false;input.reload=false;boolean moving=Math.hypot(input.moveX,input.moveY)>.08;int act=core.gameOver()?KingSpriteDrawable.ACTION_DIE:(now<actionUntil?playerAction:(moving?KingSpriteDrawable.ACTION_WALK:KingSpriteDrawable.ACTION_IDLE));if(act!=playerAction||now%95<18){playerAction=act;}if(act==KingSpriteDrawable.ACTION_IDLE)king.setState(0,act,0);}

        private void drawGame(Canvas c){
            float s=scale();c.drawColor(Color.rgb(20,31,24));world.drawBackground(c,core.player().x,core.player().y,s,getWidth(),getHeight(),HUD,PITCH);
            drawZone(c,s);drawEntities(c,s);world.drawForeground(c,core.player().x,core.player().y,s,getWidth(),getHeight(),HUD,PITCH);drawHud(c);drawControls(c);drawMiniMap(c);
        }
        private float sx(float x,float s){return cx()+(x-core.player().x)*s;}private float sy(float y,float s){return cy()+(y-core.player().y)*s*PITCH;}
        private void drawZone(Canvas c,float s){float zx=sx(WorldMap.SIZE*.5f,s),zy=sy(WorldMap.SIZE*.5f,s),r=core.zoneRadius()*s;p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(5);p.setColor(0xBB9EE6FF);c.drawOval(zx-r,zy-r*PITCH,zx+r,zy+r*PITCH,p);p.setStrokeWidth(2);p.setColor(0x3368A7CF);c.drawOval(zx-r+8,zy-(r-8)*PITCH,zx+r-8,zy+(r-8)*PITCH,p);p.setStyle(Paint.Style.FILL);}

        private void drawEntities(Canvas c,float s){
            // Pickups first so actors can occlude them; dead enemies remain briefly as fallen bodies.
            for(GameCore.Pickup it:core.pickups()){float x=sx(it.x,s),y=sy(it.y,s),r=Math.max(9,20*s);p.setColor(it.type==GameCore.PickupType.AMMO?0xFFE4C44D:it.type==GameCore.PickupType.MEDKIT?0xFFE95F55:it.type==GameCore.PickupType.GRENADE?0xFF55A66B:0xFF72B8E8);c.drawCircle(x,y,r,p);p.setColor(0xCC141414);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(Math.max(10,14*s));c.drawText(it.type==GameCore.PickupType.AMMO?"A":it.type==GameCore.PickupType.MEDKIT?"+":it.type==GameCore.PickupType.GRENADE?"B":"S",x,y+5,p);}
            for(GameCore.Grenade g:core.grenades()){float x=sx(g.x,s),y=sy(g.y,s);p.setColor(0xFF4A8454);c.drawCircle(x,y,Math.max(7,11*s),p);}
            for(GameCore.Projectile b:core.projectiles()){float x=sx(b.x,s),y=sy(b.y,s),px=sx(b.x-b.vx*.025f,s),py=sy(b.y-b.vy*.025f,s);p.setStrokeWidth(Math.max(2,4*s));p.setColor(b.fromPlayer?0xFFFFE08A:0xFFFF786C);c.drawLine(px,py,x,y,p);}
            for(GameCore.Enemy e:core.enemies()){float x=sx(e.x,s),y=sy(e.y,s);if(e.dead){p.setColor(0xAA4D261F);c.save();c.rotate(-25,x,y);c.drawOval(x-25*s,y-8*s,x+25*s,y+8*s,p);c.restore();continue;}float r=Math.max(16,26*s);p.setColor(e.type==3?0xFFD1812B:e.type==2?0xFFB84045:0xFF7E3039);c.drawCircle(x,y,r,p);p.setColor(0xFFD9C49B);c.drawCircle(x,y-r*.55f,r*.48f,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(e.state==GameCore.EnemyState.CHASE?0xFFFFD35A:0xAAFFFFFF);c.drawArc(x-r-3,y-r-3,x+r+3,y+r+3,-90,Math.max(8,360f*e.hp/e.maxHp),false,p);p.setStyle(Paint.Style.FILL);}
            GameCore.Enemy t=core.getAutoAimTarget();if(t!=null){float x=sx(t.x,s),y=sy(t.y,s),r=Math.max(30,45*s);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(4);p.setColor(0xDDFFF08C);c.drawCircle(x,y,r,p);c.drawLine(x-r-10,y,x-r+5,y,p);c.drawLine(x+r-5,y,x+r+10,y,p);c.drawLine(x,y-r-10,x,y-r+5,p);c.drawLine(x,y+r-5,x,y+r+10,p);p.setStyle(Paint.Style.FILL);}
            drawPlayer(c,s);
            for(GameCore.Explosion e:core.explosions()){float x=sx(e.x,s),y=sy(e.y,s),r=e.radius*s*(1-e.life/.38f*.25f);p.setColor(0x66FFD36A);c.drawCircle(x,y,r,p);p.setColor(0xAAFF7A32);c.drawCircle(x,y,r*.55f,p);}
        }

        private void drawPlayer(Canvas c,float s){float x=cx(),y=cy();p.setColor(0x55000000);c.drawOval(x-38*s,y+38*s,x+38*s,y+52*s,p);int frame=(int)((System.currentTimeMillis()/100)%KingSpriteDrawable.FRAME_COUNT);if(core.gameOver())frame=(int)((System.currentTimeMillis()/180)%KingSpriteDrawable.FRAME_COUNT);king.setState(0,playerAction,frame);king.setBounds((int)(x-58*s),(int)(y-100*s),(int)(x+58*s),(int)(y+100*s));king.draw(c);}

        private void drawHud(Canvas c){p.setStyle(Paint.Style.FILL);p.setColor(0xD91A2721);c.drawRoundRect(16,12,getWidth()-16,84,18,18,p);GameCore.Player pl=core.player();p.setTextAlign(Paint.Align.LEFT);p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);p.setTextSize(19);p.setColor(0xFFF1D17A);c.drawText("PERSIA WAR  •  "+pl.skin.toUpperCase(),34,40,p);p.setTypeface(android.graphics.Typeface.DEFAULT);p.setTextSize(15);p.setColor(Color.WHITE);c.drawText("HP "+pl.hp+"/"+pl.maxHp+"   SHIELD "+pl.shield,34,66,p);p.setTextAlign(Paint.Align.CENTER);c.drawText("KILLS "+core.kills(),getWidth()*.54f,40,p);c.drawText("ZONE "+Math.round(core.zoneRadius()),getWidth()*.54f,66,p);p.setTextAlign(Paint.Align.RIGHT);c.drawText("AMMO "+pl.ammo+"/"+pl.reserveAmmo+"   BOMB "+pl.grenades,getWidth()-34,53,p);}

        private void drawControls(Canvas c){float h=getHeight(),w=getWidth();float jr=Math.min(92,h*.13f);p.setColor(0x4A1C221E);c.drawCircle(joystickBaseX,joystickBaseY,jr+18,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xCCD9C889);c.drawCircle(joystickBaseX,joystickBaseY,jr+18,p);p.setStyle(Paint.Style.FILL);p.setColor(0xB5C1A45B);c.drawCircle(joystickX,joystickY,jr*.42f,p);
            button(c,w*.80f,h*.79f,66,"FIRE");button(c,w*.68f,h*.89f,54,"SWORD");button(c,w*.80f,h*.89f,54,"BOMB");button(c,w*.92f,h*.89f,54,"RELOAD");
        }
        private void button(Canvas c,float x,float y,float r,String text){p.setStyle(Paint.Style.FILL);p.setColor(0xA64A4038);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xDDD9C889);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.FILL);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(r>60?19:12);p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);p.setColor(Color.WHITE);c.drawText(text,x,y+5,p);p.setTypeface(android.graphics.Typeface.DEFAULT);}

        private void drawMiniMap(Canvas c){float w=180,h=138,left=getWidth()-w-18,top=108;RectF r=new RectF(left,top,left+w,top+h);p.setStyle(Paint.Style.FILL);p.setColor(0xC91C241F);c.drawRoundRect(r,18,18,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2);p.setColor(0xDDDDC77F);c.drawRoundRect(r,18,18,p);float sx=w/WorldMap.SIZE,sy=h/WorldMap.SIZE;for(WorldMap.Road rd:world.map().roads()){p.setColor(0x887D7565);p.setStrokeWidth(Math.max(2,rd.width*sx));c.drawLine(left+rd.x1*sx,top+rd.y1*sy,left+rd.x2*sx,top+rd.y2*sy,p);}for(WorldMap.Building b:world.map().buildings()){p.setStyle(Paint.Style.FILL);p.setColor(0x996B5942);c.drawRect(left+b.x*sx,top+b.y*sy,left+(b.x+b.w)*sx,top+(b.y+b.h)*sy,p);}p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2);float zr=core.zoneRadius()*sx;c.drawCircle(left+w*.5f,top+h*.5f,zr,p);p.setStyle(Paint.Style.FILL);for(GameCore.Enemy e:core.enemies())if(!e.dead){p.setColor(e.type==3?0xFFFFA02C:0xFFF06565);c.drawCircle(left+e.x*sx,top+e.y*sy,3,p);}p.setColor(0xFF63E47A);c.drawCircle(left+core.player().x*sx,top+core.player().y*sy,5,p);p.setTextAlign(Paint.Align.LEFT);p.setTextSize(11);p.setColor(Color.WHITE);c.drawText("MAP",left+10,top+18,p);}

        private void drawPause(Canvas c){p.setColor(0xAA000000);c.drawRect(0,0,getWidth(),getHeight(),p);p.setTextAlign(Paint.Align.CENTER);p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);p.setTextSize(36);p.setColor(0xFFF0D17B);c.drawText("PAUSED",getWidth()/2f,getHeight()*.40f,p);p.setTextSize(20);p.setColor(Color.WHITE);c.drawText("Tap RESUME area or press Back",getWidth()/2f,getHeight()*.48f,p);}
        private void drawGameOver(Canvas c){p.setColor(0xAA130B0B);c.drawRect(0,0,getWidth(),getHeight(),p);p.setTextAlign(Paint.Align.CENTER);p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);p.setTextSize(42);p.setColor(0xFFFF9A72);c.drawText("GAME OVER",getWidth()/2f,getHeight()*.42f,p);p.setTextSize(21);p.setColor(Color.WHITE);c.drawText("KILLS  "+core.kills()+"   SCORE  "+core.player().score,getWidth()/2f,getHeight()*.50f,p);p.setTextSize(17);c.drawText("TAP THE CENTER TO RESTART",getWidth()/2f,getHeight()*.58f,p);}

        @Override public boolean onTouchEvent(MotionEvent e){float x=e.getX(),y=e.getY();int a=e.getActionMasked();float h=getHeight(),w=getWidth();if(core.gameOver()){if(a==MotionEvent.ACTION_DOWN&&x>w*.30f&&x<w*.70f&&y>h*.48f&&y<h*.66f){core.reset();playerAction=KingSpriteDrawable.ACTION_IDLE;}return true;}if(paused){if(a==MotionEvent.ACTION_DOWN){paused=false;}return true;}
            if(a==MotionEvent.ACTION_DOWN||a==MotionEvent.ACTION_POINTER_DOWN){int id=e.getPointerId(e.getActionIndex());if(distance(x,y,joystickBaseX,joystickBaseY)<130&&joystickPointer<0){joystickPointer=id;setJoy(x,y);return true;}if(x>w*.64f&&y>h*.82f&&y<h*.97f){if(x>w*.875f)input.reload=true;else if(x>w*.74f)input.grenade=true;else input.sword=true;actionUntil=System.currentTimeMillis()+300;playerAction=KingSpriteDrawable.ACTION_ATTACK;return true;}if(x>w*.52f){firePointer=id;input.fire=true;return true;}}
            if(a==MotionEvent.ACTION_MOVE){for(int i=0;i<e.getPointerCount();i++){int id=e.getPointerId(i);if(id==joystickPointer)setJoy(e.getX(i),e.getY(i));}return true;}
            if(a==MotionEvent.ACTION_UP||a==MotionEvent.ACTION_POINTER_UP||a==MotionEvent.ACTION_CANCEL){int id=e.getPointerId(e.getActionIndex());if(id==joystickPointer){joystickPointer=-1;input.moveX=input.moveY=0;joystickX=joystickBaseX;joystickY=joystickBaseY;}if(id==firePointer){firePointer=-1;input.fire=false;}return true;}return true;}
        private void setJoy(float x,float y){float dx=x-joystickBaseX,dy=y-joystickBaseY,m=Math.max(1,(float)Math.hypot(dx,dy)),max=92,used=Math.min(max,m);joystickX=joystickBaseX+dx/m*used;joystickY=joystickBaseY+dy/m*used;input.moveX=(joystickX-joystickBaseX)/max;input.moveY=(joystickY-joystickBaseY)/max;}
        private float distance(float a,float b,float c,float d){return(float)Math.hypot(a-c,b-d);}
        public void togglePause(){paused=!paused;}
    }
    @Override public void onBackPressed(){GameView v=(GameView)findViewById(android.R.id.content).findViewWithTag("game");super.onBackPressed();}
}
