package com.persiawar2d;

import android.app.Activity;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.DashPathEffect;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.os.Bundle;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowManager;
import com.persiawar2d.game.GameCore;
import com.persiawar2d.world.WorldMap;

/** Responsive 2.5D top-down gameplay view for Android. */
public final class MainActivity extends Activity {
    private GameView gameView;

    @Override public void onCreate(Bundle state){
        super.onCreate(state);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN,WindowManager.LayoutParams.FLAG_FULLSCREEN);
        getWindow().getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_FULLSCREEN|View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY|View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                        |View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN|View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION|View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
        gameView=new GameView(this);
        setContentView(gameView);
    }

    @Override public void onBackPressed(){if(gameView!=null)gameView.togglePause();}

    public static final class GameView extends View {
        private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
        private final Path path=new Path();
        private final WorldMap world=new WorldMap();
        private final GameCore.Input input=new GameCore.Input();
        private final GameCore core;
        private long lastNanos=System.nanoTime();
        private float joyBaseX,joyBaseY,joyX,joyY;
        private int joyPointer=-1,firePointer=-1,aimPointer=-1;
        private boolean paused;
        private float aimTouchX,aimTouchY;

        private static final float HUD=82f;
        private static final float PITCH=.72f;

        public GameView(Context context){
            super(context);
            String skin=context.getSharedPreferences("player",0).getString("skin","classic");
            core=new GameCore(world,skin);
            setFocusable(true);
            setLayerType(View.LAYER_TYPE_HARDWARE,null);
        }

        private float sc(){return Math.min(getWidth()/2400f,Math.max(.23f,(getHeight()-HUD)/1600f));}
        private float cx(){return getWidth()*.5f;}
        private float cy(){return HUD+(getHeight()-HUD)*.52f;}
        private float sx(float x){return cx()+(x-core.player().x)*sc();}
        private float sy(float y){return cy()+(y-core.player().y)*sc()*PITCH;}
        private float minDim(){return Math.min(getWidth(),getHeight());}

        @Override protected void onSizeChanged(int w,int h,int ow,int oh){
            joyBaseX=w*.15f;joyBaseY=h*.80f;joyX=joyBaseX;joyY=joyBaseY;
        }

        @Override protected void onDraw(Canvas c){
            long now=System.nanoTime();
            float dt=Math.min(.045f,Math.max(.001f,(now-lastNanos)/1e9f));
            lastNanos=now;
            if(!paused){core.update(dt,input);input.sword=false;input.grenade=false;input.reload=false;}
            c.drawColor(Color.rgb(25,31,25));
            synchronized(core){
                drawGround(c);
                drawRoads(c);
                drawBuildings(c);
                drawVehicles(c);
                drawFences(c);
                drawNature(c);
                drawCombat(c);
                drawZone(c);
                drawMiniMap(c);
                drawHud(c);
            }
            drawControls(c);
            if(paused)drawPause(c);
            if(core.gameOver())drawGameOver(c);
            postInvalidateOnAnimation();
        }

        private void drawGround(Canvas c){
            float s=sc();
            p.setStyle(Paint.Style.FILL);
            p.setColor(Color.rgb(74,66,51));
            c.drawRect(0,HUD,getWidth(),getHeight(),p);

            p.setStrokeWidth(1);
            p.setColor(0x142F3A2D);
            float step=240;
            float startX=(float)Math.floor((core.player().x-1400)/step)*step;
            float startY=(float)Math.floor((core.player().y-900)/step)*step;
            for(float x=startX;x<core.player().x+1400;x+=step)c.drawLine(sx(x),HUD,sx(x),getHeight(),p);
            for(float y=startY;y<core.player().y+900;y+=step)c.drawLine(0,sy(y),getWidth(),sy(y),p);

            p.setColor(0x1DCEB06A);
            for(int i=0;i<world.grass().size();i+=3){
                WorldMap.Prop g=world.grass().get(i);
                if(Math.abs(g.x-core.player().x)>2100||Math.abs(g.y-core.player().y)>1250)continue;
                c.drawCircle(sx(g.x),sy(g.y),Math.max(1.2f,g.size*s*.12f),p);
            }
        }

        private void drawRoads(Canvas c){
            float s=sc();
            for(WorldMap.Road r:world.roads()){
                p.setStyle(Paint.Style.FILL);
                p.setColor(0xFF2D302D);
                if(r.horizontal){
                    c.drawRect(sx(r.x1),sy(r.y1-r.width*.5f),sx(r.x2),sy(r.y1+r.width*.5f),p);
                    p.setColor(0xFFB08B55);
                    c.drawRect(sx(r.x1),sy(r.y1-r.width*.5f+8),sx(r.x2),sy(r.y1-r.width*.5f+12),p);
                    p.setColor(0x66E9D493);
                    p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,4*s));
                    p.setPathEffect(new DashPathEffect(new float[]{18*s,22*s},0));
                    c.drawLine(sx(r.x1),sy(r.y1),sx(r.x2),sy(r.y2),p);
                    p.setPathEffect(null);
                }else{
                    c.drawRect(sx(r.x1-r.width*.5f),sy(r.y1),sx(r.x1+r.width*.5f),sy(r.y2),p);
                    p.setColor(0xFFB08B55);
                    c.drawRect(sx(r.x1-r.width*.5f+8),sy(r.y1),sx(r.x1-r.width*.5f+12),sy(r.y2),p);
                    p.setColor(0x66E9D493);
                    p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,4*s));
                    p.setPathEffect(new DashPathEffect(new float[]{18*s,22*s},0));
                    c.drawLine(sx(r.x1),sy(r.y1),sx(r.x2),sy(r.y2),p);
                    p.setPathEffect(null);
                }
                p.setStyle(Paint.Style.FILL);
            }
        }

        private void drawBuildings(Canvas c){
            float s=sc();
            for(WorldMap.Building b:world.buildings()){
                if(Math.abs(b.x+b.w*.5f-core.player().x)>2400||Math.abs(b.y+b.h*.5f-core.player().y)>1350)continue;
                float l=sx(b.x),r=sx(b.x+b.w),base=sy(b.y+b.h),back=sy(b.y);
                float lift=(92+(b.style%5)*14)*s;
                p.setStyle(Paint.Style.FILL);
                p.setColor(0x44000000);
                c.drawRoundRect(new RectF(l+7,back+8,r+10,base+10),10*s,10*s,p);

                p.setColor((b.style%5==0)?0xFF5D4534:0xFF715440);
                c.drawRect(l,back-lift,r,base,p);

                p.setColor((b.style%3==0)?0xFF2D2018:0xFF3C2A1D);
                path.reset();
                path.moveTo(l-6*s,back-lift);path.lineTo((l+r)*.5f,back-lift-20*s);
                path.lineTo(r+6*s,back-lift);path.lineTo(r,back-lift+8*s);path.lineTo(l,back-lift+8*s);path.close();
                c.drawPath(path,p);

                p.setColor(0xFFD2A95E);
                if(b.style%6==0){
                    for(float x=l+20*s;x<r-12*s;x+=42*s){
                        c.drawRect(x,back-lift+6*s,x+8*s,base-18*s,p);
                        p.setColor(0xFF8E6A38);c.drawRect(x+2*s,back-lift+6*s,x+6*s,base-18*s,p);p.setColor(0xFFD2A95E);
                    }
                    p.setColor(0xFFCBB07A);
                    c.drawRect((l+r)*.5f-20*s,base-68*s,(l+r)*.5f+20*s,base,p);
                    p.setColor(0xFF5C3E27);c.drawRect((l+r)*.5f-13*s,base-60*s,(l+r)*.5f+13*s,base,p);
                }else{
                    int cols=Math.max(2,Math.min(4,(int)(b.w/115)));
                    for(int i=0;i<cols;i++){
                        float x=l+26*s+i*(r-l-52*s)/Math.max(1,cols-1);
                        float y=back-lift+(base-(back-lift))*.45f;
                        p.setColor(0xFFD6B66B);c.drawRoundRect(new RectF(x-8*s,y-10*s,x+8*s,y+10*s),3*s,3*s,p);
                        p.setColor(0xFF243238);c.drawRoundRect(new RectF(x-5*s,y-7*s,x+5*s,y+7*s),2*s,2*s,p);
                    }
                    p.setColor(0xFF9C7042);c.drawRect((l+r)*.5f-12*s,base-45*s,(l+r)*.5f+12*s,base,p);
                }
            }
        }

        private void drawVehicles(Canvas c){
            float s=sc();
            for(WorldMap.Vehicle v:world.vehicles()){
                if(Math.abs(v.x-core.player().x)>1900||Math.abs(v.y-core.player().y)>1100)continue;
                float x=sx(v.x),y=sy(v.y);
                c.save();c.rotate(v.angle,x,y);
                p.setStyle(Paint.Style.FILL);p.setColor(0xFF33454A);c.drawRoundRect(new RectF(x-v.w*s*.5f,y-v.h*s*.5f,x+v.w*s*.5f,y+v.h*s*.5f),8*s,8*s,p);
                p.setColor(0xFF132027);c.drawRoundRect(new RectF(x-v.w*s*.22f,y-v.h*s*.30f,x+v.w*s*.22f,y+v.h*s*.30f),4*s,4*s,p);
                p.setColor(0xFF8E6A42);
                c.drawRect(x-v.w*s*.42f,y-v.h*s*.48f,x-v.w*s*.34f,y-v.h*s*.28f,p);
                c.drawRect(x+v.w*s*.34f,y+v.h*s*.28f,x+v.w*s*.42f,y+v.h*s*.48f,p);
                c.restore();
            }
        }

        private void drawFences(Canvas c){
            float s=sc();
            for(WorldMap.Fence f:world.fences()){
                if(Math.abs((f.x1+f.x2)*.5f-core.player().x)>1900||Math.abs((f.y1+f.y2)*.5f-core.player().y)>1100)continue;
                float x1=sx(f.x1),y1=sy(f.y1),x2=sx(f.x2),y2=sy(f.y2);
                p.setColor(0xFF8B6A43);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(3,7*s));
                c.drawLine(x1,y1,x2,y2,p);
                p.setStyle(Paint.Style.FILL);
                c.drawCircle(x1,y1,5*s,p);c.drawCircle(x2,y2,5*s,p);
            }
        }

        private void drawNature(Canvas c){
            float s=sc();
            int treeStep=Math.max(1,world.trees().size()/72);
            for(int i=0;i<world.trees().size();i+=treeStep){
                WorldMap.Prop t=world.trees().get(i);
                if(Math.abs(t.x-core.player().x)>1900||Math.abs(t.y-core.player().y)>1100)continue;
                float x=sx(t.x),y=sy(t.y),r=(18+t.size*.58f)*s;
                p.setColor(0x44000000);c.drawOval(new RectF(x-r*.7f,y+r*.15f,x+r*.7f,y+r*.65f),p);
                p.setColor(0xFF70472E);c.drawRoundRect(new RectF(x-4*s,y-r*.12f,x+4*s,y+r*.42f),3*s,3*s,p);
                p.setColor(0xFF24563A);c.drawCircle(x,y-r*.15f,r,p);
                p.setColor(0xFF33744A);c.drawCircle(x-r*.35f,y-r*.35f,r*.68f,p);
                p.setColor(0xFF43895A);c.drawCircle(x+r*.32f,y-r*.38f,r*.58f,p);
            }
        }

        private void drawCombat(Canvas c){
            float s=sc();
            for(GameCore.Pickup item:core.pickups()){
                float x=sx(item.x),y=sy(item.y);
                if(x<-50||x>getWidth()+50||y<HUD-50||y>getHeight()+50)continue;
                p.setStyle(Paint.Style.FILL);p.setColor(0x55000000);c.drawCircle(x,y+8*s,16*s,p);
                p.setColor(item.type==GameCore.PickupType.AMMO?0xFFE1BA57:item.type==GameCore.PickupType.MEDKIT?0xFFDE5B57:item.type==GameCore.PickupType.GRENADE?0xFF5DA675:0xFF63A6D3);
                c.drawRoundRect(new RectF(x-14*s,y-14*s,x+14*s,y+14*s),5*s,5*s,p);
                p.setColor(Color.WHITE);p.setTextSize(Math.max(11,15*s));p.setTextAlign(Paint.Align.CENTER);p.setTypeface(PaintCompat.BOLD);c.drawText(
                        item.type==GameCore.PickupType.AMMO?"A":item.type==GameCore.PickupType.MEDKIT?"+":item.type==GameCore.PickupType.GRENADE?"B":"S",x,y+5*s,p);
                p.setTypeface(null);
            }

            for(GameCore.Grenade g:core.grenades()){
                float x=sx(g.x),y=sy(g.y);p.setColor(0xFF4B8A59);c.drawCircle(x,y,10*s,p);p.setColor(0xAAE4F2B4);c.drawCircle(x-3*s,y-3*s,3*s,p);
            }

            for(GameCore.Projectile b:core.projectiles()){
                float x=sx(b.x),y=sy(b.y),ox=sx(b.x-b.vx*.035f),oy=sy(b.y-b.vy*.035f);
                p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2.2f,4.5f*s));
                p.setColor(b.fromPlayer?0xFFFFD66E:0xFFFF5C58);c.drawLine(ox,oy,x,y,p);
                p.setStyle(Paint.Style.FILL);c.drawCircle(x,y,Math.max(2,4*s),p);
            }

            for(GameCore.Enemy e:core.enemies()){
                float x=sx(e.x),y=sy(e.y);if(x<-90||x>getWidth()+90||y<HUD-80||y>getHeight()+90)continue;
                if(e.dead){drawDeath(c,x,y,s,e);continue;}
                drawWarrior(c,x,y,s,e.type,e.hp<e.maxHp?0xFFD9A56A:e.state==GameCore.EnemyState.ATTACK?0xFFB8423E:0xFF8B3340,false);
                if(e.hp<e.maxHp){
                    p.setColor(0x66000000);c.drawRoundRect(new RectF(x-25*s,y-50*s,x+25*s,y-44*s),3*s,3*s,p);
                    p.setColor(0xFFD85952);c.drawRoundRect(new RectF(x-25*s,y-50*s,x-25*s+50*s*e.hp/e.maxHp,y-44*s),3*s,3*s,p);
                }
            }

            drawPlayer(c);
            for(GameCore.Explosion ex:core.explosions()){
                float x=sx(ex.x),y=sy(ex.y),progress=1-ex.life/.38f,r=ex.radius*s*(.18f+progress*.82f);
                p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(3,8*s*(1-progress)));p.setColor(0xFFFFA43B);c.drawCircle(x,y,r,p);
                p.setColor(0x55FFE36C);p.setStyle(Paint.Style.FILL);c.drawCircle(x,y,r*.58f,p);p.setStyle(Paint.Style.FILL);
            }

            drawAim(c);
        }

        private void drawDeath(Canvas c,float x,float y,float s,GameCore.Enemy e){
            p.setStyle(Paint.Style.FILL);p.setColor(0x88522A24);c.save();c.rotate(-30,x,y);
            c.drawRoundRect(new RectF(x-24*s,y-9*s,x+24*s,y+9*s),7*s,7*s,p);c.restore();
        }

        private void drawPlayer(Canvas c){
            float s=sc(),x=cx(),y=cy(),angle=(float)Math.toDegrees(Math.atan2(core.player().facingY,core.player().facingX));
            c.save();c.rotate(angle+90,x,y);
            drawWarrior(c,x,y,s,0,0xFFD4B35E,true);
            c.restore();
        }

        private void drawWarrior(Canvas c,float x,float y,float s,int type,int armor,boolean player){
            float k=player?1.12f:(type==3?1.28f:type==2?1.08f:.96f);
            p.setStyle(Paint.Style.FILL);
            p.setColor(0x52000000);c.drawOval(new RectF(x-22*s*k,y+22*s*k,x+22*s*k,y+34*s*k),p);

            p.setColor(player?0xFF4F6F66:(type==3?0xFF5B3030:type==2?0xFF493D55:0xFF6C3037));
            c.drawRoundRect(new RectF(x-17*s*k,y-13*s*k,x+17*s*k,y+26*s*k),8*s*k,8*s*k,p);

            p.setColor(armor);
            c.drawRect(x-14*s*k,y-4*s*k,x+14*s*k,y+9*s*k,p);

            p.setColor(player?0xFFD6B86E:0xFFE2C39A);
            c.drawCircle(x,y-29*s*k,14*s*k,p);

            p.setColor(0xFF34261C);
            c.drawRect(x-16*s*k,y-38*s*k,x+16*s*k,y-30*s*k,p);
            path.reset();path.moveTo(x-4*s*k,y-47*s*k);path.lineTo(x+3*s*k,y-68*s*k);path.lineTo(x+10*s*k,y-46*s*k);path.close();
            p.setColor(player?0xFFC99B4E:0xFF6C4830);c.drawPath(path,p);

            p.setColor(player?0xFFB9C7C5:0xFF8B96A0);
            c.drawRoundRect(new RectF(x-28*s*k,y-7*s*k,x-18*s*k,y+18*s*k),5*s*k,5*s*k,p);
            p.setColor(0xFFD0B56B);c.drawRect(x-26*s*k,y+3*s*k,x-20*s*k,y+16*s*k,p);

            p.setColor(type==2&&!player?0xFFD4B05B:0xFFB9C1C1);
            float weaponY=14*s*k;
            c.drawRect(x+13*s*k,y+weaponY,x+18*s*k,y+weaponY+34*s*k,p);
            p.setColor(0xFF5B3D28);c.drawRect(x+12*s*k,y+8*s*k,x+19*s*k,y+22*s*k,p);

            if(type==2&&!player){
                p.setColor(0xFF8F6236);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,2.5f*s));
                c.drawArc(new RectF(x+10*s*k,y-1*s*k,x+36*s*k,y+29*s*k),-70,140,false,p);p.setStyle(Paint.Style.FILL);
            }else if(type==3&&!player){
                p.setColor(0xFF9B6A3A);c.drawRoundRect(new RectF(x+14*s*k,y+1*s*k,x+22*s*k,y+42*s*k),3*s*k,3*s*k,p);
            }
        }

        private void drawZone(Canvas c){
            float s=sc(),zx=sx(WorldMap.SIZE*.5f),zy=sy(WorldMap.SIZE*.5f),rx=core.zoneRadius()*s,ry=rx*PITCH;
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(3,5*s));p.setColor(0x669AD8FF);c.drawOval(new RectF(zx-rx,zy-ry,zx+rx,zy+ry),p);
            if(core.zoneRadius()<1550){
                p.setStrokeWidth(Math.max(2,7*s));p.setColor(0x88F05A52);c.drawOval(new RectF(zx-rx,zy-ry,zx+rx,zy+ry),p);
            }
            p.setStyle(Paint.Style.FILL);
        }

        private void drawAim(Canvas c){
            GameCore.Enemy target=core.getAutoAimTarget();
            if(input.aimActive){
                float dx=input.aimX,dy=input.aimY,l=Math.max(1,(float)Math.hypot(dx,dy)),rad=minDim()*.23f;
                float tx=cx()+dx/l*rad,ty=cy()+dy/l*rad;
                p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,3*sc()));p.setColor(0xCCFFE08A);
                c.drawCircle(tx,ty,20*sc()+8,p);c.drawLine(tx-32*sc(),ty,tx-10*sc(),ty,p);c.drawLine(tx+10*sc(),ty,tx+32*sc(),ty,p);
                c.drawLine(tx,ty-32*sc(),tx,ty-10*sc(),p);c.drawLine(tx,ty+10*sc(),tx,ty+32*sc(),p);p.setStyle(Paint.Style.FILL);
            }else if(target!=null){
                float tx=sx(target.x),ty=sy(target.y),r=30*sc()+10;
                p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,3*sc()));p.setColor(0xCCFFE08A);
                c.drawCircle(tx,ty,r,p);c.drawLine(tx-r-9,ty,tx-r+3,ty,p);c.drawLine(tx+r-3,ty,tx+r+9,ty,p);
                c.drawLine(tx,ty-r-9,tx,ty-r+3,p);c.drawLine(tx,ty+r-3,tx,ty+r+9,ty,p);p.setStyle(Paint.Style.FILL);
            }
        }

        private void drawMiniMap(Canvas c){
            float size=Math.min(getWidth()*.24f,190),left=getWidth()-size-18,top=HUD+14;
            p.setStyle(Paint.Style.FILL);p.setColor(0xD91C241F);c.drawRoundRect(new RectF(left,top,left+size,top+size*.78f),16,16,p);
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2.5f);p.setColor(0xD7D9C889);c.drawRoundRect(new RectF(left,top,left+size,top+size*.78f),16,16,p);
            float mx=size/WorldMap.SIZE,my=size*.78f/WorldMap.SIZE;
            for(WorldMap.Road r:world.roads()){
                p.setColor(0x997D7565);p.setStrokeWidth(Math.max(1.5f,r.width*mx*.42f));
                c.drawLine(left+r.x1*mx,top+r.y1*my,left+r.x2*mx,top+r.y2*my,p);
            }
            p.setStyle(Paint.Style.FILL);
            for(WorldMap.Building b:world.buildings()){
                p.setColor(0xAA6B5542);c.drawRect(left+b.x*mx,top+b.y*my,left+(b.x+b.w)*mx,top+(b.y+b.h)*my,p);
            }
            p.setColor(0xFF63E47A);c.drawCircle(left+core.player().x*mx,top+core.player().y*my,5,p);
            for(GameCore.Enemy e:core.enemies())if(!e.dead){
                p.setColor(e.type==3?0xFFFFA02C:0xFFF06565);c.drawCircle(left+e.x*mx,top+e.y*my,2.8f,p);
            }
            p.setColor(Color.WHITE);p.setTextSize(11);p.setTypeface(PaintCompat.BOLD);p.setTextAlign(Paint.Align.LEFT);
            c.drawText("TACTICAL MAP",left+10,top+17,p);p.setTypeface(null);
        }

        private void drawHud(Canvas c){
            float w=getWidth(),h=getHeight(),s=Math.max(.75f,Math.min(1f,h/720f));
            p.setStyle(Paint.Style.FILL);p.setColor(0xE21B2520);c.drawRoundRect(new RectF(12,10,w-12,HUD-8),15,15,p);
            GameCore.Player pl=core.player();
            p.setTypeface(PaintCompat.BOLD);p.setTextAlign(Paint.Align.LEFT);p.setTextSize(19*s);p.setColor(0xFFF0D178);
            c.drawText("PERSIA WAR",28,36*s,p);
            p.setTypeface(null);p.setTextSize(13*s);p.setColor(0xFFF2F2EC);
            c.drawText(pl.skin.toUpperCase(),28,58*s,p);

            float barL=150*s,barW=250*s;
            p.setColor(0x55333333);c.drawRoundRect(new RectF(barL,24*s,barL+barW,38*s),7,7,p);
            p.setColor(0xFFE0544C);c.drawRoundRect(new RectF(barL,24*s,barL+barW*pl.hp/pl.maxHp,38*s),7,7,p);
            p.setColor(Color.WHITE);p.setTextSize(12*s);c.drawText("HP "+pl.hp+"/"+pl.maxHp,barL+8*s,35*s,p);

            p.setColor(0x55333333);c.drawRoundRect(new RectF(barL,44*s,barL+barW,56*s),6,6,p);
            p.setColor(0xFF61A4D4);c.drawRoundRect(new RectF(barL,44*s,barL+barW*Math.min(1,pl.shield/100f),56*s),6,6,p);
            c.drawText("SHIELD "+pl.shield,barL+8*s,54*s,p);

            p.setTextAlign(Paint.Align.CENTER);p.setTypeface(PaintCompat.BOLD);p.setTextSize(16*s);p.setColor(0xFFF4F1E8);
            c.drawText("KILLS "+core.kills(),w*.50f,34*s,p);
            p.setTypeface(null);p.setTextSize(12*s);c.drawText("SURVIVE THE CITY",w*.50f,56*s,p);

            p.setTextAlign(Paint.Align.RIGHT);p.setTypeface(PaintCompat.BOLD);p.setTextSize(15*s);p.setColor(0xFFEAD28C);
            c.drawText("AMMO "+pl.ammo+"/"+pl.reserveAmmo+"   BOMB "+pl.grenades,w-28,35*s,p);
            p.setTypeface(null);p.setTextSize(11*s);p.setColor(0xFFDEE4DF);
            c.drawText(core.zoneRadius()<1550?"ZONE CLOSING":"ZONE STABLE",w-28,56*s,p);
        }

        private void drawControls(Canvas c){
            float w=getWidth(),h=getHeight(),r=minDim()*.125f;
            p.setStyle(Paint.Style.FILL);p.setColor(0x33151C19);c.drawCircle(joyBaseX,joyBaseY,r+14,p);
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xB8D9C889);c.drawCircle(joyBaseX,joyBaseY,r+10,p);
            p.setStyle(Paint.Style.FILL);p.setColor(0xA5C1A45B);c.drawCircle(joyX,joyY,r*.43f,p);
            p.setColor(Color.WHITE);p.setTypeface(PaintCompat.BOLD);p.setTextSize(Math.max(9,r*.15f));p.setTextAlign(Paint.Align.CENTER);c.drawText("MOVE",joyBaseX,joyBaseY+r+24,p);

            button(c,w*.83f,h*.73f,minDim()*.092f,"FIRE",input.fire);
            button(c,w*.67f,h*.90f,minDim()*.069f,"SWORD",input.sword);
            button(c,w*.78f,h*.90f,minDim()*.069f,"BOMB",input.grenade);
            button(c,w*.89f,h*.90f,minDim()*.069f,"RELOAD",input.reload);

            if(input.aimActive){
                p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2);p.setColor(0x668DCCFF);
                c.drawCircle(aimTouchX,aimTouchY,28,p);c.drawLine(aimTouchX-42,aimTouchY,aimTouchX-18,aimTouchY,p);c.drawLine(aimTouchX+18,aimTouchY,aimTouchX+42,aimTouchY,p);
                c.drawLine(aimTouchX,aimTouchY-42,aimTouchX,aimTouchY-18,p);c.drawLine(aimTouchX,aimTouchY+18,aimTouchX,aimTouchY+42,p);
                p.setStyle(Paint.Style.FILL);
            }
        }

        private void button(Canvas c,float x,float y,float r,String text,boolean pressed){
            p.setStyle(Paint.Style.FILL);p.setColor(pressed?0xE05C6A55:0xA94A4038);c.drawCircle(x,y,r,p);
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xDDD9C889);c.drawCircle(x,y,r,p);
            p.setStyle(Paint.Style.FILL);p.setColor(Color.WHITE);p.setTypeface(PaintCompat.BOLD);p.setTextAlign(Paint.Align.CENTER);
            p.setTextSize(Math.max(10,r*.23f));c.drawText(text,x,y+5,p);p.setTypeface(null);
        }

        private void drawPause(Canvas c){
            p.setStyle(Paint.Style.FILL);p.setColor(0xB9000000);c.drawRect(0,0,getWidth(),getHeight(),p);
            p.setTextAlign(Paint.Align.CENTER);p.setTypeface(PaintCompat.BOLD);p.setTextSize(40);p.setColor(0xFFF0D17B);
            c.drawText("PAUSED",getWidth()*.5f,getHeight()*.43f,p);p.setTypeface(null);p.setTextSize(18);p.setColor(Color.WHITE);
            c.drawText("TAP TO RESUME",getWidth()*.5f,getHeight()*.52f,p);
        }

        private void drawGameOver(Canvas c){
            p.setStyle(Paint.Style.FILL);p.setColor(0xC3130B0B);c.drawRect(0,0,getWidth(),getHeight(),p);
            p.setTextAlign(Paint.Align.CENTER);p.setTypeface(PaintCompat.BOLD);p.setTextSize(42);p.setColor(0xFFFF9A72);
            c.drawText("GAME OVER",getWidth()*.5f,getHeight()*.43f,p);p.setTypeface(null);p.setTextSize(20);p.setColor(Color.WHITE);
            c.drawText("KILLS "+core.kills()+"   SCORE "+core.player().score,getWidth()*.5f,getHeight()*.51f,p);
            p.setTextSize(17);c.drawText("TAP CENTER TO RESTART",getWidth()*.5f,getHeight()*.60f,p);
        }

        private boolean inside(float x,float y,float cx,float cy,float r){return Math.hypot(x-cx,y-cy)<=r;}
        private void beginPointer(MotionEvent e,int index){
            float x=e.getX(index),y=e.getY(index),w=getWidth(),h=getHeight();
            int id=e.getPointerId(index);
            float fireR=minDim()*.115f,swordR=minDim()*.085f;
            if(inside(x,y,w*.83f,h*.73f,fireR)){firePointer=id;input.fire=true;return;}
            if(inside(x,y,w*.67f,h*.90f,swordR)){input.sword=true;return;}
            if(inside(x,y,w*.78f,h*.90f,swordR)){input.grenade=true;return;}
            if(inside(x,y,w*.89f,h*.90f,swordR)){input.reload=true;return;}
            if(x<w*.43f&&y>HUD){joyPointer=id;joyX=joyBaseX=x;joyY=joyBaseY=y;input.moveX=0;input.moveY=0;return;}
            if(x>w*.43f&&y>HUD){
                aimPointer=id;input.aimActive=true;setAim(x,y);return;
            }
        }

        private void setAim(float x,float y){
            aimTouchX=x;aimTouchY=y;
            float dx=x-cx(),dy=(y-cy())/PITCH,l=(float)Math.hypot(dx,dy);
            if(l<1)l=1;
            input.aimX=dx/l;input.aimY=dy/l;
        }

        private void setJoy(float x,float y){
            float max=minDim()*.125f,dx=x-joyBaseX,dy=y-joyBaseY,l=(float)Math.hypot(dx,dy),u=Math.min(max,l);
            if(l>0){joyX=joyBaseX+dx/l*u;joyY=joyBaseY+dy/l*u;}
            input.moveX=(joyX-joyBaseX)/max;input.moveY=(joyY-joyBaseY)/max;
        }

        @Override public boolean onTouchEvent(MotionEvent e){
            int a=e.getActionMasked();
            if(core.gameOver()){
                if(a==MotionEvent.ACTION_DOWN&&e.getX()>getWidth()*.30f&&e.getX()<getWidth()*.70f&&e.getY()>getHeight()*.45f&&e.getY()<getHeight()*.72f){
                    core.reset();paused=false;clearInput();
                }
                return true;
            }
            if(paused){
                if(a==MotionEvent.ACTION_DOWN){paused=false;clearInput();}
                return true;
            }
            if(a==MotionEvent.ACTION_DOWN||a==MotionEvent.ACTION_POINTER_DOWN){
                beginPointer(e,e.getActionIndex());return true;
            }
            if(a==MotionEvent.ACTION_MOVE){
                for(int i=0;i<e.getPointerCount();i++){
                    int id=e.getPointerId(i);
                    if(id==joyPointer)setJoy(e.getX(i),e.getY(i));
                    else if(id==aimPointer)setAim(e.getX(i),e.getY(i));
                }
                return true;
            }
            if(a==MotionEvent.ACTION_UP||a==MotionEvent.ACTION_POINTER_UP||a==MotionEvent.ACTION_CANCEL){
                int id=e.getPointerId(e.getActionIndex());
                if(id==joyPointer){joyPointer=-1;joyX=joyBaseX;joyY=joyBaseY;input.moveX=input.moveY=0;}
                if(id==aimPointer){aimPointer=-1;input.aimActive=false;input.aimX=input.aimY=0;}
                if(id==firePointer){firePointer=-1;input.fire=false;}
                input.sword=false;input.grenade=false;input.reload=false;
                return true;
            }
            return true;
        }

        private void clearInput(){
            joyPointer=firePointer=aimPointer=-1;joyX=joyBaseX;joyY=joyBaseY;
            input.moveX=input.moveY=input.aimX=input.aimY=0;input.aimActive=false;
            input.fire=input.sword=input.grenade=input.reload=false;
        }

        void togglePause(){paused=!paused;if(paused)clearInput();}
    }

    /** Avoids Android's Typeface constants leaking into the rendering helpers. */
    private static final class PaintCompat {
        static final android.graphics.Typeface BOLD=android.graphics.Typeface.create(android.graphics.Typeface.DEFAULT,android.graphics.Typeface.BOLD);
        private PaintCompat(){}
    }
}
