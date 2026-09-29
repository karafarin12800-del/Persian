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
        private int swordPointer=-1,grenadePointer=-1,reloadPointer=-1;
        private boolean joyActive;
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

        private float sc(){return Math.min(getWidth()/1900f,Math.max(.34f,(getHeight()-HUD)/1250f));}
        private float cx(){return getWidth()*.5f;}
        private float cy(){return HUD+(getHeight()-HUD)*.52f;}
        private float sx(float x){return cx()+(x-core.player().x)*sc();}
        private float sy(float y){return cy()+(y-core.player().y)*sc()*PITCH;}
        private float minDim(){return Math.min(getWidth(),getHeight());}

        private float dp(float v){return v*getResources().getDisplayMetrics().density;}
        private float joystickRadius(){return dp(72f);}
        private float fireVisualRadius(){return dp(58f);}
        private float fireHitRadius(){return dp(64f);}
        private float actionVisualRadius(){return dp(48f);}
        private float actionHitRadius(){return dp(44f);}
        private float fireX(){return getWidth()-dp(110f);}
        private float fireY(){return getHeight()-dp(205f);}
        private float actionY(){return getHeight()-dp(80f);}
        private float swordX(){return getWidth()-dp(280f);}
        private float bombX(){return getWidth()-dp(175f);}
        private float reloadX(){return getWidth()-dp(70f);}
        private float idleJoyX(){return dp(120f);}
        private float idleJoyY(){return getHeight()-dp(105f);}

        @Override protected void onSizeChanged(int w,int h,int ow,int oh){
            joyBaseX=dp(120f);joyBaseY=h-dp(105f);joyX=joyBaseX;joyY=joyBaseY;
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
                if(Math.abs(b.x+b.w*.5f-core.player().x)>2050||Math.abs(b.y+b.h*.5f-core.player().y)>1200)continue;
                float l=sx(b.x),r=sx(b.x+b.w),base=sy(b.y+b.h),back=sy(b.y);
                float lift=(78+(b.style%4)*12)*s;
                p.setStyle(Paint.Style.FILL);

                // Ground shadow and raised Persian masonry block.
                p.setColor(0x55000000);
                c.drawRoundRect(new RectF(l+8,back+10,r+12,base+12),9*s,9*s,p);
                p.setColor((b.style%5==0)?0xFF7A6047:(b.style%5==1?0xFF80664C:0xFF725844));
                c.drawRect(l,back-lift,r,base,p);

                // Roof/parapet silhouette.
                path.reset();
                path.moveTo(l-7*s,back-lift+5*s);
                path.lineTo((l+r)*.5f,back-lift-18*s);
                path.lineTo(r+7*s,back-lift+5*s);
                path.lineTo(r,back-lift+12*s);
                path.lineTo(l,back-lift+12*s);
                path.close();
                p.setColor((b.style%3==0)?0xFF3D2D24:0xFF4A3528);
                c.drawPath(path,p);

                // Persian facade band.
                p.setColor(0xFFD2B16B);
                c.drawRect(l,back-lift+14*s,r,back-lift+20*s,p);
                p.setColor(0x668B6A42);
                c.drawRect(l,base-15*s,r,base-10*s,p);

                int cols=Math.max(2,Math.min(5,(int)(b.w/100)));
                for(int i=0;i<cols;i++){
                    float x=l+26*s+i*(r-l-52*s)/Math.max(1,cols-1);
                    float y=back-lift+(base-(back-lift))*.48f;
                    // Arched window.
                    p.setColor(0xFFBFA46A);
                    c.drawRoundRect(new RectF(x-10*s,y-18*s,x+10*s,y+14*s),9*s,9*s,p);
                    p.setColor((b.style%2==0)?0xFF25404A:0xFF2C3840);
                    c.drawRoundRect(new RectF(x-7*s,y-14*s,x+7*s,y+10*s),7*s,7*s,p);
                    p.setColor(0x66E6C96E);
                    c.drawRect(x-1*s,y-13*s,x+1*s,y+9*s,p);
                }

                // Central doorway and small entrance canopy.
                float dx=(l+r)*.5f;
                p.setColor(0xFFD0AD67);
                c.drawRoundRect(new RectF(dx-19*s,base-62*s,dx+19*s,base+1*s),12*s,12*s,p);
                p.setColor(0xFF4B3022);
                c.drawRoundRect(new RectF(dx-13*s,base-54*s,dx+13*s,base+1*s),9*s,9*s,p);

                // Small Persian roof ornament / finial.
                p.setColor(0xFFD5B45E);
                c.drawCircle(dx,back-lift-20*s,3.5f*s,p);
                c.drawRect(dx-1.5f*s,back-lift-29*s,dx+1.5f*s,back-lift-20*s,p);

                // Occasional blue tile strip.
                if(b.style%4==1){
                    p.setColor(0xFF3C7180);
                    c.drawRect(l+18*s,back-lift+27*s,r-18*s,back-lift+31*s,p);
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
            int treeStep=Math.max(1,world.trees().size()/84);
            for(int i=0;i<world.trees().size();i+=treeStep){
                WorldMap.Prop t=world.trees().get(i);
                if(Math.abs(t.x-core.player().x)>1800||Math.abs(t.y-core.player().y)>1050)continue;
                float x=sx(t.x),y=sy(t.y),r=(18+t.size*.58f)*s;
                p.setColor(0x44000000);c.drawOval(new RectF(x-r*.75f,y+r*.18f,x+r*.75f,y+r*.65f),p);
                p.setColor(0xFF70472E);c.drawRoundRect(new RectF(x-4*s,y-r*.05f,x+4*s,y+r*.46f),3*s,3*s,p);
                p.setColor(0xFF1F5539);c.drawCircle(x,y-r*.18f,r,p);
                p.setColor(0xFF34784A);c.drawCircle(x-r*.34f,y-r*.35f,r*.67f,p);
                p.setColor(0xFF4B9560);c.drawCircle(x+r*.32f,y-r*.38f,r*.55f,p);
            }
            int bushStep=Math.max(1,world.bushes().size()/70);
            for(int i=0;i<world.bushes().size();i+=bushStep){
                WorldMap.Prop b=world.bushes().get(i);
                if(Math.abs(b.x-core.player().x)>1800||Math.abs(b.y-core.player().y)>1050)continue;
                float x=sx(b.x),y=sy(b.y),r=Math.max(3,b.size*.75f*s);
                p.setColor(0x44352C20);c.drawOval(new RectF(x-r,y+r*.15f,x+r,y+r*.55f),p);
                p.setColor(0xFF2C6841);c.drawCircle(x,y,r,p);
                p.setColor(0xFF438A56);c.drawCircle(x-r*.38f,y-r*.25f,r*.62f,p);
                c.drawCircle(x+r*.35f,y-r*.18f,r*.55f,p);
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
            float s=sc(),x=cx(),y=cy();
            drawWarrior(c,x,y,s,0,0xFFD7B85F,true);
        }

        private void drawWarrior(Canvas c,float x,float y,float s,int type,int armor,boolean player){
            final float k=player?1.50f:(type==3?1.46f:type==2?1.38f:1.30f);
            final float u=s*k;
            float bob=player?(float)Math.sin(core.player().walkPhase*0.31f)*(.75f+core.player().walkBlend*1.6f)*u:0f;
            y+=bob;
            p.setStyle(Paint.Style.FILL);

            // Deep, soft contact shadow gives the soldier a real 2.5D footprint.
            p.setColor(0x70000000);
            c.drawOval(new RectF(x-27*u,y+27*u,x+27*u,y+40*u),p);
            p.setColor(0x22000000);
            c.drawOval(new RectF(x-34*u,y+21*u,x+34*u,y+37*u),p);

            // Backpack and rear radio assembly.
            p.setColor(player?0xFF253734:(type==3?0xFF302727:0xFF302B34));
            c.drawRoundRect(new RectF(x-20*u,y-4*u,x+20*u,y+25*u),7*u,7*u,p);
            p.setColor(player?0xFF3E5953:0xFF4A4045);
            c.drawRoundRect(new RectF(x-16*u,y+2*u,x+16*u,y+21*u),5*u,5*u,p);
            p.setColor(0xFF1B2524);
            c.drawRect(x+10*u,y-2*u,x+15*u,y+13*u,p);
            p.setColor(0xFFD2B56E);
            c.drawCircle(x+12.5f*u,y-5*u,3*u,p);

            // Legs: layered tactical trousers, knee guards, boots. Walking phase shifts each leg
            // independently so movement reads as a real stride rather than sliding.
            float stride=player?Math.sin(core.player().walkPhase)*7.5f*core.player().walkBlend:0f;
            float stride2=player?Math.sin(core.player().walkPhase+Math.PI)*7.5f*core.player().walkBlend:0f;
            float mfx=player?core.player().facingX:0f, mfy=player?core.player().facingY:0f;
            float fpx=-mfy, fpy=mfx;
            float leg1x=mfx*stride, leg1y=mfy*stride*PITCH;
            float leg2x=mfx*stride2, leg2y=mfy*stride2*PITCH;
            float side=5.5f;
            float l1sx=fpx*side,l1sy=fpy*side*PITCH,l2sx=-fpx*side,l2sy=-fpy*side*PITCH;
            int pants=player?0xFF273D3A:(type==3?0xFF332628:type==2?0xFF403A50:0xFF3A2C35);
            p.setColor(pants);
            c.drawRoundRect(new RectF(x-14*u+leg1x+l1sx*u,y+13*u+leg1y+l1sy*u,x-2*u+leg1x+l1sx*u,y+38*u+leg1y+l1sy*u),4*u,4*u,p);
            c.drawRoundRect(new RectF(x+2*u+leg2x+l2sx*u,y+13*u+leg2y+l2sy*u,x+14*u+leg2x+l2sx*u,y+38*u+leg2y+l2sy*u),4*u,4*u,p);
            p.setColor(0xFF151A19);
            c.drawRoundRect(new RectF(x-17*u+leg1x+l1sx*u,y+34*u+leg1y+l1sy*u,x-1*u+leg1x+l1sx*u,y+44*u+leg1y+l1sy*u),5*u,5*u,p);
            c.drawRoundRect(new RectF(x+1*u+leg2x+l2sx*u,y+34*u+leg2y+l2sy*u,x+17*u+leg2x+l2sx*u,y+44*u+leg2y+l2sy*u),5*u,5*u,p);
            p.setColor(player?0xFF526963:0xFF59525A);
            c.drawRoundRect(new RectF(x-13*u+leg1x+l1sx*u,y+22*u+leg1y+l1sy*u,x-4*u+leg1x+l1sx*u,y+30*u+leg1y+l1sy*u),3*u,3*u,p);
            c.drawRoundRect(new RectF(x+4*u+leg2x+l2sx*u,y+22*u+leg2y+l2sy*u,x+13*u+leg2x+l2sx*u,y+30*u+leg2y+l2sy*u),3*u,3*u,p);
            p.setColor(0xFF9B845C);
            c.drawRect(x-11*u,y+25*u,x-5*u,y+27*u,p);
            c.drawRect(x+5*u,y+25*u,x+11*u,y+27*u,p);

            // Torso / tactical vest.
            int cloth=player?0xFF536F68:(type==3?0xFF6A3435:type==2?0xFF544A6A:0xFF6A3A45);
            p.setColor(cloth);
            c.drawRoundRect(new RectF(x-21*u,y-16*u,x+21*u,y+24*u),10*u,10*u,p);

            // Shoulder-to-waist armor contour.
            p.setColor(0xFF1E2A29);
            c.drawRoundRect(new RectF(x-18*u,y-9*u,x+18*u,y+18*u),6*u,6*u,p);
            p.setColor(player?0xFF6B857D:(type==3?0xFF80605C:0xFF6D616A));
            c.drawRoundRect(new RectF(x-14*u,y-8*u,x+14*u,y+12*u),4*u,4*u,p);

            // Central ballistic plate with Persian-inspired turquoise/gold emblem.
            p.setColor(0xFF182321);
            c.drawRoundRect(new RectF(x-10*u,y-8*u,x+10*u,y+10*u),4*u,4*u,p);
            p.setColor(armor);
            c.drawRect(x-1.5f*u,y-7*u,x+1.5f*u,y+9*u,p);
            p.setColor(0xFF2A7A7A);
            path.reset();
            path.moveTo(x,y-4*u);path.lineTo(x+4*u,y*u);path.lineTo(x,y+4*u);path.lineTo(x-4*u,y*u);path.close();
            c.drawPath(path,p);
            p.setColor(0xFFD9B75A);
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(1.5f,1.8f*u));
            c.drawLine(x-6*u,y+13*u,x+6*u,y+13*u,p);
            p.setStyle(Paint.Style.FILL);

            // Ammunition pouches and belt.
            p.setColor(0xFF242A27);
            c.drawRoundRect(new RectF(x-19*u,y+7*u,x-10*u,y+18*u),2*u,2*u,p);
            c.drawRoundRect(new RectF(x+10*u,y+7*u,x+19*u,y+18*u),2*u,2*u,p);
            c.drawRoundRect(new RectF(x-7*u,y+11*u,x+1*u,y+19*u),2*u,2*u,p);
            c.drawRoundRect(new RectF(x+1*u,y+11*u,x+7*u,y+19*u),2*u,2*u,p);
            p.setColor(0xFFC39B55);
            c.drawRect(x-20*u,y+18*u,x+20*u,y+21*u,p);

            // Shoulder guards.
            p.setColor(player?0xFFAF9360:(type==3?0xFF96705E:0xFF806451));
            c.drawOval(new RectF(x-25*u,y-12*u,x-13*u,y+2*u),p);
            c.drawOval(new RectF(x+13*u,y-12*u,x+25*u,y+2*u),p);
            p.setColor(0x55302A20);
            c.drawOval(new RectF(x-24*u,y-8*u,x-15*u,y+1*u),p);
            c.drawOval(new RectF(x+15*u,y-8*u,x+24*u,y+1*u),p);

            // Neck and head.
            p.setColor(player?0xFFC99B72:0xFFD5AD86);
            c.drawCircle(x,y-25*u,13*u,p);
            p.setColor(0xFF33251E);
            c.drawOval(new RectF(x-14*u,y-39*u,x+14*u,y-14*u),p);
            p.setColor(player?0xFFC6A34F:(type==3?0xFF9F7B45:0xFF725036));
            c.drawOval(new RectF(x-16*u,y-42*u,x+16*u,y-27*u),p);
            // Helmet rim / visor.
            p.setColor(0xFF242A28);
            c.drawRoundRect(new RectF(x-17*u,y-32*u,x+17*u,y-25*u),3*u,3*u,p);
            p.setColor(player?0xFF6C876E:(type==3?0xFF735646:0xFF574A58));
            c.drawRoundRect(new RectF(x-13*u,y-40*u,x+13*u,y-30*u),5*u,5*u,p);
            p.setColor(0xFF111716);
            c.drawRoundRect(new RectF(x-11*u,y-33*u,x+11*u,y-28*u),2*u,2*u,p);
            p.setColor(0xFFD7BD72);
            c.drawRect(x-7*u,y-30*u,x-2*u,y-29*u,p);
            c.drawRect(x+2*u,y-30*u,x+7*u,y-29*u,p);
            // Helmet rail + side headset.
            p.setColor(0xFF3E4844);
            c.drawRect(x-12*u,y-43*u,x+12*u,y-40*u,p);
            c.drawCircle(x-16*u,y-27*u,3.2f*u,p);
            c.drawCircle(x+16*u,y-27*u,3.2f*u,p);

            // Arms are aimed with the weapon; the torso itself remains stable.
            float[] weaponDir=player?core.getWeaponAimDirection():new float[]{core.player().facingX,core.player().facingY};
            float fx=player?weaponDir[0]:1f;
            float fy=player?weaponDir[1]*PITCH:0f;
            float fl=Math.max(.001f,(float)Math.hypot(fx,fy));
            fx/=fl;fy/=fl;
            float px=-fy,py=fx;

            // Forearms and gloves. A subtle counter-swing keeps the upper body alive while walking.
            float armSwing=player?Math.sin(core.player().walkPhase+Math.PI)*3.2f*core.player().walkBlend:0f;
            p.setStrokeCap(Paint.Cap.ROUND);
            p.setStrokeWidth(Math.max(7f,9f*u));
            p.setColor(player?0xFF4C625D:0xFF51454C);
            c.drawLine(x+px*(14+armSwing)*u,y-2*u+py*(14+armSwing)*u,x+fx*24*u+px*5*u,y+fy*24*u+py*5*u,p);
            c.drawLine(x-px*(14-armSwing)*u,y-2*u-py*(14-armSwing)*u,x+fx*22*u-px*5*u,y+fy*22*u-py*5*u,p);
            p.setStrokeWidth(Math.max(4f,6f*u));
            p.setColor(0xFF171B1A);
            c.drawCircle(x+fx*25*u+px*5*u,y+fy*25*u+py*5*u,4*u,p);
            c.drawCircle(x+fx*23*u-px*5*u,y+fy*23*u-py*5*u,4*u,p);
            p.setStrokeCap(Paint.Cap.BUTT);

            // Modern assault rifle, rotated only as a weapon so the soldier does not spin.
            float angle=(float)Math.toDegrees(Math.atan2(fy,fx));
            c.save();
            c.rotate(angle,x+fx*7*u,y+fy*7*u);
            float kick=player && input.fire?4.5f:0f;
            float wx=x+fx*(7-kick)*u,wy=y+fy*(7-kick)*u;
            p.setStrokeCap(Paint.Cap.ROUND);
            p.setStrokeWidth(Math.max(4f,7*u));
            p.setColor(0xFF1A1F1E);
            c.drawLine(wx-12*u,wy+2*u,wx+42*u,wy+2*u,p);
            p.setStrokeWidth(Math.max(2f,4*u));
            p.setColor(0xFF7D8580);
            c.drawLine(wx+18*u,wy-1*u,wx+59*u,wy-1*u,p);
            p.setColor(0xFF0F1413);
            c.drawLine(wx-24*u,wy+7*u,wx+1*u,wy+7*u,p);
            // Enlarged receiver, magazine, trigger housing and textured handguard.
            p.setColor(0xFF2C3431);
            c.drawRoundRect(new RectF(wx+5*u,wy-3*u,wx+25*u,wy+9*u),2.5f*u,2.5f*u,p);
            p.setColor(0xFF111615);
            path.reset();path.moveTo(wx+8*u,wy+8*u);path.lineTo(wx+19*u,wy+8*u);path.lineTo(wx+16*u,wy+20*u);path.lineTo(wx+9*u,wy+18*u);path.close();c.drawPath(path,p);
            p.setColor(0xFF202725);
            for(int gi=0;gi<4;gi++)c.drawRect(wx+26*u+gi*5*u,wy-4*u,wx+29*u+gi*5*u,wy+6*u,p);
            p.setStyle(Paint.Style.FILL);
            c.drawRoundRect(new RectF(wx+2*u,wy+2*u,wx+18*u,wy+12*u),3*u,3*u,p);
            p.setColor(0xFF8C6239);
            c.drawRoundRect(new RectF(wx-2*u,wy+8*u,wx+7*u,wy+18*u),2*u,2*u,p);
            c.drawRoundRect(new RectF(wx+24*u,wy+2*u,wx+40*u,wy+11*u),3*u,3*u,p);
            p.setColor(0xFFD6B45D);
            c.drawRect(wx+54*u,wy-3*u,wx+64*u,wy+2*u,p);
            p.setColor(0xFFB7C0B9);
            c.drawRect(wx+62*u,wy-5*u,wx+66*u,wy+5*u,p);
            p.setColor(0xFF4D5651);
            c.drawRect(wx-7*u,wy+18*u,wx+4*u,wy+20*u,p);
            p.setStrokeCap(Paint.Cap.BUTT);
            // Sight + charging handle.
            p.setColor(0xFF303936);
            c.drawRect(wx+29*u,wy-7*u,wx+35*u,wy-2*u,p);
            c.drawRect(wx+38*u,wy-8*u,wx+43*u,wy-3*u,p);
            c.restore();

            // Sling line adds a final equipment detail without obscuring the silhouette.
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(1.2f,1.8f*u));p.setColor(0xAA8C7654);
            c.drawLine(x-px*17*u,y-py*17*u,x+fx*43*u,y+fy*43*u,p);p.setStyle(Paint.Style.FILL);

            // Small muzzle flash when the trigger is held.
            if(player && input.fire){
                float mx=x+fx*67*u,my=y+fy*67*u;
                p.setColor(0xFFFFD76A);
                path.reset();
                path.moveTo(mx,my);
                path.lineTo(mx+fx*15*u+px*6*u,my+fy*15*u+py*6*u);
                path.lineTo(mx+fx*15*u-px*6*u,my+fy*15*u-py*6*u);
                path.close();
                c.drawPath(path,p);
            }
        }

        private void drawZone(Canvas c){
            float s=sc(),zx=sx(WorldMap.SIZE*.5f),zy=sy(WorldMap.SIZE*.5f),rx=core.zoneRadius()*s,ry=rx*PITCH;
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,3*s));p.setColor(0x4D9AD8FF);p.setPathEffect(new DashPathEffect(new float[]{22*s,18*s},0));c.drawOval(new RectF(zx-rx,zy-ry,zx+rx,zy+ry),p);
            p.setPathEffect(null);
            if(core.zoneRadius()<1550){
                p.setStrokeWidth(Math.max(2,4*s));p.setColor(0xA8F05A52);c.drawOval(new RectF(zx-rx,zy-ry,zx+rx,zy+ry),p);
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
                c.drawLine(tx,ty-r-9,tx,ty-r+3,p);c.drawLine(tx,ty+r-3,tx,ty+r+9,p);p.setStyle(Paint.Style.FILL);
            }
        }

        private void drawMiniMap(Canvas c){
            float size=Math.min(getWidth()*.30f,250),left=getWidth()-size-16,top=HUD+12;
            p.setStyle(Paint.Style.FILL);p.setColor(0xD91C241F);c.drawRoundRect(new RectF(left,top,left+size,top+size*.82f),16,16,p);
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2.5f);p.setColor(0xD7D9C889);c.drawRoundRect(new RectF(left,top,left+size,top+size*.82f),16,16,p);
            float mx=size/WorldMap.SIZE,my=size*.82f/WorldMap.SIZE;
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
            float w=getWidth(),h=getHeight(),jr=joystickRadius();

            // Always show a subtle joystick landing zone; touching the left side makes
            // it float to the exact finger-down position.
            float drawBaseX=joyActive?joyBaseX:idleJoyX();
            float drawBaseY=joyActive?joyBaseY:idleJoyY();
            p.setStyle(Paint.Style.FILL);
            p.setColor(0x2D151C19);
            c.drawCircle(drawBaseX,drawBaseY,jr+14,p);
            p.setStyle(Paint.Style.STROKE);
            p.setStrokeWidth(Math.max(dp(3f),3f*dp(1f)));
            p.setColor(joyActive?0xB8D9C889:0x707B7667);
            c.drawCircle(drawBaseX,drawBaseY,jr+10,p);
            p.setStyle(Paint.Style.FILL);
            p.setColor(joyActive?0xA5C1A45B:0x557A775D);
            c.drawCircle(joyActive?joyX:drawBaseX,joyActive?joyY:drawBaseY,jr*.43f,p);
            p.setColor(0xE8FFFFFF);
            p.setTypeface(PaintCompat.BOLD);
            p.setTextSize(Math.max(dp(10f),jr*.15f));
            p.setTextAlign(Paint.Align.CENTER);
            c.drawText("MOVE",drawBaseX,drawBaseY+jr+dp(24f),p);

            button(c,fireX(),fireY(),fireVisualRadius(),"FIRE",firePointer>=0);
            button(c,swordX(),actionY(),actionVisualRadius(),"SWORD",swordPointer>=0);
            button(c,bombX(),actionY(),actionVisualRadius(),"BOMB",grenadePointer>=0);
            button(c,reloadX(),actionY(),actionVisualRadius(),"RELOAD",reloadPointer>=0);

            if(input.aimActive){
                p.setStyle(Paint.Style.STROKE);
                p.setStrokeWidth(Math.max(dp(2f),2.5f*dp(1f)));
                p.setColor(0x668DCCFF);
                float rr=dp(28f);
                float arm=dp(42f);
                c.drawCircle(aimTouchX,aimTouchY,rr,p);
                c.drawLine(aimTouchX-arm,aimTouchY,aimTouchX-rr,aimTouchY,p);
                c.drawLine(aimTouchX+rr,aimTouchY,aimTouchX+arm,aimTouchY,p);
                c.drawLine(aimTouchX,aimTouchY-arm,aimTouchX,aimTouchY-rr,p);
                c.drawLine(aimTouchX,aimTouchY+rr,aimTouchX,aimTouchY+arm,p);
                p.setStyle(Paint.Style.FILL);
            }
        }

        private void button(Canvas c,float x,float y,float r,String text,boolean pressed){
            p.setStyle(Paint.Style.FILL);
            p.setColor(pressed?0xE05C6A55:0xA94A4038);
            c.drawCircle(x,y,r,p);
            p.setStyle(Paint.Style.STROKE);
            p.setStrokeWidth(Math.max(dp(3f),3f*dp(1f)));
            p.setColor(0xDDD9C889);
            c.drawCircle(x,y,r,p);
            p.setStyle(Paint.Style.FILL);
            p.setColor(Color.WHITE);
            p.setTypeface(PaintCompat.BOLD);
            p.setTextAlign(Paint.Align.CENTER);
            p.setTextSize(Math.max(dp(10f),r*.22f));
            c.drawText(text,x,y+dp(4f),p);
            p.setTypeface(null);
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
            float x=e.getX(index),y=e.getY(index);
            int id=e.getPointerId(index);
            if(inside(x,y,fireX(),fireY(),fireHitRadius())&&firePointer<0){
                firePointer=id;
                input.fire=true;
                return;
            }
            if(inside(x,y,swordX(),actionY(),actionHitRadius())&&swordPointer<0){
                swordPointer=id;
                input.sword=true;
                return;
            }
            if(inside(x,y,bombX(),actionY(),actionHitRadius())&&grenadePointer<0){
                grenadePointer=id;
                input.grenade=true;
                return;
            }
            if(inside(x,y,reloadX(),actionY(),actionHitRadius())&&reloadPointer<0){
                reloadPointer=id;
                input.reload=true;
                return;
            }
            if(x<getWidth()*.48f&&y>HUD&&joyPointer<0){
                joyPointer=id;
                joyActive=true;
                joyX=joyBaseX=x;
                joyY=joyBaseY=y;
                input.moveX=0;
                input.moveY=0;
                return;
            }
            if(x>getWidth()*.48f&&y>HUD&&aimPointer<0){
                aimPointer=id;
                input.aimActive=true;
                setAim(x,y);
            }
        }

        private void setAim(float x,float y){
            aimTouchX=x;aimTouchY=y;
            float dx=x-cx(),dy=(y-cy())/PITCH,l=(float)Math.hypot(dx,dy);
            if(l<1)l=1;
            input.aimX=dx/l;input.aimY=dy/l;
        }

        private void setJoy(float x,float y){
            float max=joystickRadius(),dx=x-joyBaseX,dy=y-joyBaseY,l=(float)Math.hypot(dx,dy),u=Math.min(max,l);
            if(l>0){
                joyX=joyBaseX+dx/l*u;
                joyY=joyBaseY+dy/l*u;
            }
            input.moveX=(joyX-joyBaseX)/max;
            input.moveY=(joyY-joyBaseY)/max;
        }

        @Override public boolean onTouchEvent(MotionEvent e){
            int a=e.getActionMasked();
            if(core.gameOver()){
                if(a==MotionEvent.ACTION_DOWN&&e.getX()>getWidth()*.30f&&e.getX()<getWidth()*.70f
                        &&e.getY()>getHeight()*.45f&&e.getY()<getHeight()*.72f){
                    core.reset();
                    paused=false;
                    clearInput();
                }
                return true;
            }
            if(paused){
                if(a==MotionEvent.ACTION_DOWN){
                    paused=false;
                    clearInput();
                }
                return true;
            }

            if(a==MotionEvent.ACTION_DOWN||a==MotionEvent.ACTION_POINTER_DOWN){
                beginPointer(e,e.getActionIndex());
                return true;
            }

            if(a==MotionEvent.ACTION_MOVE){
                for(int i=0;i<e.getPointerCount();i++){
                    int id=e.getPointerId(i);
                    if(id==joyPointer)setJoy(e.getX(i),e.getY(i));
                    else if(id==aimPointer)setAim(e.getX(i),e.getY(i));
                }
                return true;
            }

            if(a==MotionEvent.ACTION_UP||a==MotionEvent.ACTION_POINTER_UP){
                int id=e.getPointerId(e.getActionIndex());
                if(id==joyPointer){
                    joyPointer=-1;
                    joyActive=false;
                    joyX=joyBaseX;
                    joyY=joyBaseY;
                    input.moveX=input.moveY=0;
                }
                if(id==aimPointer){
                    aimPointer=-1;
                    input.aimActive=false;
                    input.aimX=input.aimY=0;
                }
                if(id==firePointer){
                    firePointer=-1;
                    input.fire=false;
                }
                if(id==swordPointer){
                    swordPointer=-1;
                    input.sword=false;
                }
                if(id==grenadePointer){
                    grenadePointer=-1;
                    input.grenade=false;
                }
                if(id==reloadPointer){
                    reloadPointer=-1;
                    input.reload=false;
                }
                return true;
            }

            if(a==MotionEvent.ACTION_CANCEL){
                clearInput();
                return true;
            }
            return true;
        }

        private void clearInput(){
            joyPointer=firePointer=aimPointer=-1;
            swordPointer=grenadePointer=reloadPointer=-1;
            joyActive=false;
            joyX=joyBaseX;
            joyY=joyBaseY;
            input.moveX=input.moveY=input.aimX=input.aimY=0;
            input.aimActive=false;
            input.fire=input.sword=input.grenade=input.reload=false;
        }

        void togglePause(){paused=!paused;if(paused)clearInput();}
    }

    // Player art pass v2: detailed layered 2.5D soldier rendering.\n    /** Avoids Android's Typeface constants leaking into the rendering helpers. */
    private static final class PaintCompat {
        static final android.graphics.Typeface BOLD=android.graphics.Typeface.create(android.graphics.Typeface.DEFAULT,android.graphics.Typeface.BOLD);
        private PaintCompat(){}
    }
}
