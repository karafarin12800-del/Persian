package com.persiawar2d;

import android.app.Activity;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.os.Bundle;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowManager;
import android.opengl.GLSurfaceView;
import android.widget.FrameLayout;
import com.persiawar2d.game.GameCore;
import com.persiawar2d.world.WorldMap;

/** Gameplay screen: OpenGL world + responsive overlay input/HUD. GameCore is unchanged. */
public final class MainActivity extends Activity {
    private GameScreen screen;
    @Override public void onCreate(Bundle state){super.onCreate(state);getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN,WindowManager.LayoutParams.FLAG_FULLSCREEN);screen=new GameScreen(this);setContentView(screen);}
    @Override public void onBackPressed(){if(screen!=null)screen.togglePause();}

    public static final class GameScreen extends FrameLayout {
        private final GLSurfaceView gl; private final World3DRenderer renderer; private final Overlay overlay; private final GameCore.Input input=new GameCore.Input(); private boolean paused;
        public GameScreen(Context c){
            super(c);setWillNotDraw(false);String skin=c.getSharedPreferences("player",0).getString("skin","classic");GameCore core=new GameCore(new WorldMap(),skin);
            renderer=new World3DRenderer(core);renderer.bindInput(input);gl=new GLSurfaceView(c);gl.setEGLContextClientVersion(2);gl.setRenderer(renderer);gl.setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);addView(gl,new FrameLayout.LayoutParams(-1,-1));
            overlay=new Overlay(c,renderer,input);addView(overlay,new FrameLayout.LayoutParams(-1,-1));
        }
        public void togglePause(){paused=!paused;renderer.setPaused(paused);overlay.setPaused(paused);}
    }

    private static final class Overlay extends View {
        private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);private final World3DRenderer renderer;private final GameCore.Input input;private final KingSpriteDrawable king;private final float d;
        private float baseX,baseY,knobX,knobY;private boolean joyActive,paused;private int joyPointer=-1,firePointer=-1;private int action=KingSpriteDrawable.ACTION_IDLE;
        Overlay(Context c,World3DRenderer renderer,GameCore.Input input){super(c);this.renderer=renderer;this.input=input;king=new KingSpriteDrawable(c);d=getResources().getDisplayMetrics().density;setLayerType(View.LAYER_TYPE_HARDWARE,null);}
        private float dp(float v){return v*d;}private float hudH(){return dp(74);}
        @Override protected void onDraw(Canvas c){super.onDraw(c);GameCore core=renderer.core();synchronized(core){drawHud(c,core);drawMiniMap(c,core);drawZoneWarning(c,core);drawPlayer(c,core);if(core.gameOver())drawOver(c,core);}if(joyActive)drawJoystick(c);drawButtons(c);if(paused)drawPause(c);postInvalidateOnAnimation();}
        private void drawHud(Canvas c,GameCore core){float w=getWidth();p.setStyle(Paint.Style.FILL);p.setColor(0xE219241F);c.drawRoundRect(dp(10),dp(8),w-dp(10),hudH(),dp(14),dp(14),p);GameCore.Player pl=core.player();p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.LEFT);p.setTextSize(dp(15));p.setColor(0xFFF1D17A);c.drawText("PERSIA WAR 3D",dp(24),dp(31),p);p.setTypeface(android.graphics.Typeface.DEFAULT);p.setTextSize(dp(13));p.setColor(Color.WHITE);c.drawText("HP "+pl.hp+"/"+pl.maxHp+"   SHIELD "+pl.shield,dp(24),dp(55),p);p.setTextAlign(Paint.Align.CENTER);c.drawText("KILLS "+core.kills(),w*.54f,dp(31),p);c.drawText("ZONE "+Math.round(core.zoneRadius()),w*.54f,dp(55),p);p.setTextAlign(Paint.Align.RIGHT);p.setTextSize(dp(14));c.drawText("AMMO "+pl.ammo+"/"+pl.reserveAmmo+"   BOMB "+pl.grenades,w-dp(24),dp(43),p);}
        private void drawMiniMap(Canvas c,GameCore core){float w=getWidth(),mw=Math.min(dp(350),w*.30f),mh=mw*.76f,left=w-mw-dp(16),top=hudH()+dp(12);RectF r=new RectF(left,top,left+mw,top+mh);p.setStyle(Paint.Style.FILL);p.setColor(0xD91A211D);c.drawRoundRect(r,dp(14),dp(14),p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(dp(2));p.setColor(0xFFDCC77B);c.drawRoundRect(r,dp(14),dp(14),p);float mx=mw/WorldMap.SIZE,my=mh/WorldMap.SIZE;WorldMap map=core.world();for(WorldMap.Road rd:map.roads()){p.setColor(0x88776F61);p.setStrokeWidth(Math.max(dp(1.5f),rd.width*mx));c.drawLine(left+rd.x1*mx,top+rd.y1*my,left+rd.x2*mx,top+rd.y2*my,p);}p.setStyle(Paint.Style.FILL);for(WorldMap.Building b:map.buildings()){p.setColor(0xAA664F39);c.drawRect(left+b.x*mx,top+b.y*my,left+(b.x+b.w)*mx,top+(b.y+b.h)*my,p);}p.setStyle(Paint.Style.STROKE);p.setColor(0xBB9EE6FF);p.setStrokeWidth(dp(2));c.drawCircle(left+WorldMap.SIZE*.5f*mx,top+WorldMap.SIZE*.5f*my,core.zoneRadius()*mx,p);p.setStyle(Paint.Style.FILL);for(GameCore.Enemy e:core.enemies())if(!e.dead){p.setColor(e.type==3?0xFFFFA02C:0xFFF06565);c.drawCircle(left+e.x*mx,top+e.y*my,dp(2.8f),p);}p.setColor(0xFF63E47A);c.drawCircle(left+core.player().x*mx,top+core.player().y*my,dp(5),p);p.setColor(Color.WHITE);p.setTextAlign(Paint.Align.LEFT);p.setTextSize(dp(11));c.drawText("TACTICAL MAP",left+dp(10),top+dp(18),p);}
        private void drawZoneWarning(Canvas c,GameCore core){if(core.zoneRadius()<1500){float cx=getWidth()*.5f,cy=hudH()+(getHeight()-hudH())*.52f;p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(dp(3));p.setColor(0x55FF6D5B);c.drawOval(cx-dp(360),cy-dp(205),cx+dp(360),cy+dp(205),p);}}
        private void drawPlayer(Canvas c,GameCore core){float cx=getWidth()*.5f,cy=hudH()+(getHeight()-hudH())*.54f,scale=Math.max(.72f,Math.min(1.0f,getWidth()/1500f));int frame=(int)((System.currentTimeMillis()/(action==KingSpriteDrawable.ACTION_ATTACK?90:105))%KingSpriteDrawable.FRAME_COUNT);if(core.gameOver())action=KingSpriteDrawable.ACTION_DIE;king.setState(0,action,frame);float half=dp(42)*scale;king.setBounds((int)(cx-half),(int)(cy-half*1.9f),(int)(cx+half),(int)(cy+half*1.9f));king.draw(c);}
        private void drawJoystick(Canvas c){float r=dp(72);p.setStyle(Paint.Style.FILL);p.setColor(0x3D151A18);c.drawCircle(baseX,baseY,r,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(dp(3));p.setColor(0xD7D9C889);c.drawCircle(baseX,baseY,r,p);p.setStyle(Paint.Style.FILL);p.setColor(0xB5C1A45B);c.drawCircle(knobX,knobY,r*.43f,p);}
        private void drawButtons(Canvas c){float w=getWidth(),h=getHeight(),fireR=dp(58),small=dp(48);button(c,w-dp(110),h-dp(205),fireR,"FIRE",0xB84A4038);button(c,w-dp(238),h-dp(80),small,"SWORD",0xA04A4038);button(c,w-dp(134),h-dp(80),small,"BOMB",0xA04A4038);button(c,w-dp(30)-small,h-dp(80),small,"RELOAD",0xA04A4038);}
        private void button(Canvas c,float x,float y,float r,String text,int fill){p.setStyle(Paint.Style.FILL);p.setColor(fill);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(dp(3));p.setColor(0xDDD9C889);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.FILL);p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(text.equals("FIRE")?dp(15):dp(10));p.setColor(Color.WHITE);c.drawText(text,x,y+dp(4),p);}
        private void drawPause(Canvas c){p.setStyle(Paint.Style.FILL);p.setColor(0xAA000000);c.drawRect(0,0,getWidth(),getHeight(),p);p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(dp(34));p.setColor(0xFFF0D17B);c.drawText("PAUSED",getWidth()*.5f,getHeight()*.43f,p);p.setTextSize(dp(17));p.setColor(Color.WHITE);c.drawText("TAP TO RESUME",getWidth()*.5f,getHeight()*.52f,p);}
        private void drawOver(Canvas c,GameCore core){p.setStyle(Paint.Style.FILL);p.setColor(0xB0130B0B);c.drawRect(0,0,getWidth(),getHeight(),p);p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(dp(40));p.setColor(0xFFFF9A72);c.drawText("GAME OVER",getWidth()*.5f,getHeight()*.43f,p);p.setTextSize(dp(19));p.setColor(Color.WHITE);c.drawText("KILLS "+core.kills()+"   SCORE "+core.player().score,getWidth()*.5f,getHeight()*.51f,p);c.drawText("TAP CENTER TO RESTART",getWidth()*.5f,getHeight()*.59f,p);}
        private boolean inCircle(float x,float y,float cx,float cy,float r){return Math.hypot(x-cx,y-cy)<=r;}
        @Override public boolean onTouchEvent(MotionEvent e){float x=e.getX(),y=e.getY(),w=getWidth(),h=getHeight();int a=e.getActionMasked(),id=e.getPointerId(e.getActionIndex());GameCore core=renderer.core();if(core.gameOver()){if(a==MotionEvent.ACTION_DOWN&&x>w*.30f&&x<w*.70f&&y>h*.47f&&y<h*.70f){synchronized(core){core.reset();}action=KingSpriteDrawable.ACTION_IDLE;}return true;}if(paused){if(a==MotionEvent.ACTION_DOWN){paused=false;renderer.setPaused(false);}return true;}
            float fireX=w-dp(110),fireY=h-dp(205),fireR=dp(72),small=dp(60),smallY=h-dp(80),swordX=w-dp(238),bombX=w-dp(134),reloadX=w-dp(30)-dp(48);
            if(a==MotionEvent.ACTION_DOWN||a==MotionEvent.ACTION_POINTER_DOWN){if(x<w*.48f&&y>hudH()+dp(4)&&!joyActive){joyActive=true;joyPointer=id;baseX=knobX=x;baseY=knobY=y;synchronized(input){input.moveX=0;input.moveY=0;}return true;}synchronized(input){if(inCircle(x,y,fireX,fireY,fireR)){firePointer=id;input.fire=true;action=KingSpriteDrawable.ACTION_ATTACK;return true;}if(inCircle(x,y,swordX,smallY,small+dp(10))){input.sword=true;action=KingSpriteDrawable.ACTION_ATTACK;return true;}if(inCircle(x,y,bombX,smallY,small+dp(10))){input.grenade=true;action=KingSpriteDrawable.ACTION_ATTACK;return true;}if(inCircle(x,y,reloadX,smallY,small+dp(10))){input.reload=true;return true;}}}
            if(a==MotionEvent.ACTION_MOVE){for(int i=0;i<e.getPointerCount();i++){if(e.getPointerId(i)==joyPointer){float mx=e.getX(i),my=e.getY(i),dx=mx-baseX,dy=my-baseY,max=dp(72),len=(float)Math.hypot(dx,dy),u=Math.min(max,len);if(len>0){knobX=baseX+dx/len*u;knobY=baseY+dy/len*u;}synchronized(input){input.moveX=(knobX-baseX)/max;input.moveY=(knobY-baseY)/max;}}}return true;}
            if(a==MotionEvent.ACTION_UP||a==MotionEvent.ACTION_POINTER_UP||a==MotionEvent.ACTION_CANCEL){if(id==joyPointer){joyPointer=-1;joyActive=false;synchronized(input){input.moveX=0;input.moveY=0;}}if(id==firePointer){firePointer=-1;synchronized(input){input.fire=false;}}synchronized(input){input.sword=false;input.grenade=false;input.reload=false;}return true;}return true;}
        void setPaused(boolean value){paused=value;if(value){synchronized(input){input.moveX=input.moveY=0;input.fire=false;input.sword=input.grenade=input.reload=false;}}}
    }
}
