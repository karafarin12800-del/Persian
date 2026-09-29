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

            // Ground pickups: custom illustrated objects instead of flat lettered squares.
            for(GameCore.Pickup item:core.pickups()){
                float x=sx(item.x),y=sy(item.y);
                if(x<-70||x>getWidth()+70||y<HUD-90||y>getHeight()+100)continue;
                drawPickupIcon(c,x,y,s,item.type);
            }

            // Flying grenades: compact 3D orb with cap, pin and highlight.
            for(GameCore.Grenade g:core.grenades()){
                float x=sx(g.x),y=sy(g.y);
                float r=Math.max(dp(6f),12*s);
                p.setStyle(Paint.Style.FILL);
                p.setColor(0x4A000000);
                c.drawOval(new RectF(x-r*.9f,y+r*.45f,x+r*.9f,y+r*.9f),p);
                p.setColor(0xFF394C40);
                c.drawCircle(x,y,r,p);
                p.setColor(0xFF1E2A24);
                c.drawRoundRect(new RectF(x-r*.18f,y-r*.95f,x+r*.18f,y-r*.58f),r*.12f,r*.12f,p);
                p.setColor(0xFFD1AE5F);
                c.drawCircle(x-r*.05f,y-r*.87f,r*.13f,p);
                p.setColor(0x83B5D69E);
                c.drawCircle(x-r*.32f,y-r*.35f,r*.28f,p);
            }

            // Projectiles: layered tracer, bright tip and fading tail.
            for(GameCore.Projectile b:core.projectiles()){
                float x=sx(b.x),y=sy(b.y);
                float ox=sx(b.x-b.vx*.04f),oy=sy(b.y-b.vy*.04f);
                p.setStyle(Paint.Style.STROKE);
                p.setStrokeCap(Paint.Cap.ROUND);
                p.setStrokeWidth(Math.max(dp(3f),5.5f*s));
                p.setColor(b.fromPlayer?0x55FFE08A:0x55FF756B);
                c.drawLine(ox,oy,x,y,p);
                p.setStrokeWidth(Math.max(dp(1.4f),2.8f*s));
                p.setColor(b.fromPlayer?0xFFFFD86A:0xFFFF7168);
                c.drawLine(ox,oy,x,y,p);
                p.setStyle(Paint.Style.FILL);
                c.drawCircle(x,y,Math.max(dp(2f),3.6f*s),p);
                p.setColor(0xFFFFFFFF);
                c.drawCircle(x,y,Math.max(1.2f,1.5f*s),p);
                p.setStrokeCap(Paint.Cap.BUTT);
            }

            for(GameCore.Enemy e:core.enemies()){
                float x=sx(e.x),y=sy(e.y);if(x<-110||x>getWidth()+110||y<HUD-100||y>getHeight()+110)continue;
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
                p.setStyle(Paint.Style.STROKE);
                p.setStrokeWidth(Math.max(3,8*s*(1-progress)));
                p.setColor(0xFFFFA43B);
                c.drawCircle(x,y,r,p);
                p.setColor(0x55FFE36C);
                p.setStyle(Paint.Style.FILL);
                c.drawCircle(x,y,r*.58f,p);
            }

            drawAim(c);
        }

        private void drawPickupIcon(Canvas c,float x,float y,float s,GameCore.PickupType type){
            float r=Math.max(dp(11f),17*s);

            // Soft footprint + collectible halo.
            p.setStyle(Paint.Style.FILL);
            p.setColor(0x52000000);
            c.drawOval(new RectF(x-r*1.05f,y+r*.70f,x+r*1.05f,y+r*1.15f),p);
            p.setColor(type==GameCore.PickupType.AMMO?0x305FD8A0:
                    type==GameCore.PickupType.MEDKIT?0x30FF827A:
                    type==GameCore.PickupType.GRENADE?0x305FAF7A:0x306DBCE8);
            c.drawCircle(x,y,r*1.22f,p);

            if(type==GameCore.PickupType.AMMO){
                // Compact brass ammo case with latch and cartridges.
                p.setColor(0xFFB98C48);
                c.drawRoundRect(new RectF(x-r,y-r*.72f,x+r,y+r*.68f),r*.16f,r*.16f,p);
                p.setColor(0xFF2A332E);
                c.drawRect(x-r*.86f,y-r*.56f,x+r*.86f,y-r*.44f,p);
                p.setColor(0xFFD7B867);
                c.drawRoundRect(new RectF(x-r*.18f,y-r*.92f,x+r*.18f,y-r*.64f),r*.07f,r*.07f,p);
                for(int i=-2;i<=2;i++){
                    p.setColor(0xFFE4C978);
                    c.drawCircle(x+i*r*.25f,y-r*.08f,r*.09f,p);
                    p.setColor(0xFF8A6037);
                    c.drawRect(x+i*r*.25f-r*.035f,y-r*.02f,x+i*r*.25f+r*.035f,y+r*.48f,p);
                }
                p.setColor(0xFF6D512E);
                c.drawRect(x-r*.65f,y+r*.52f,x+r*.65f,y+r*.63f,p);
            }else if(type==GameCore.PickupType.MEDKIT){
                // Field medkit with raised cross and side clips.
                p.setColor(0xFF5A3030);
                c.drawRoundRect(new RectF(x-r,y-r*.72f,x+r,y+r*.72f),r*.18f,r*.18f,p);
                p.setColor(0xFFD9E1D4);
                c.drawRoundRect(new RectF(x-r*.12f,y-r*.53f,x+r*.12f,y+r*.53f),r*.04f,r*.04f,p);
                c.drawRoundRect(new RectF(x-r*.53f,y-r*.12f,x+r*.53f,y+r*.12f),r*.04f,r*.04f,p);
                p.setColor(0xFFE6C76B);
                c.drawRect(x-r*.72f,y-r*.84f,x+r*.72f,y-r*.74f,p);
                c.drawCircle(x-r*.88f,y, r*.09f,p);
                c.drawCircle(x+r*.88f,y, r*.09f,p);
            }else if(type==GameCore.PickupType.GRENADE){
                // Unique grenade pickup: body, safety lever and pin.
                p.setColor(0xFF32483A);
                c.drawCircle(x,y+r*.04f,r*.68f,p);
                p.setColor(0xFF1D2923);
                c.drawRoundRect(new RectF(x-r*.18f,y-r*.67f,x+r*.18f,y-r*.42f),r*.07f,r*.07f,p);
                p.setColor(0xFFD4AF60);
                p.setStyle(Paint.Style.STROKE);
                p.setStrokeWidth(Math.max(1.2f,2*s));
                c.drawArc(new RectF(x-r*.52f,y-r*.46f,x+r*.52f,y+r*.55f),210,120,false,p);
                p.setStyle(Paint.Style.FILL);
                c.drawCircle(x+r*.07f,y-r*.66f,r*.10f,p);
                p.setColor(0x83B8D7A7);
                c.drawCircle(x-r*.23f,y-r*.27f,r*.22f,p);
            }else{
                // Shield pickup: faceted energy badge, not a letter.
                path.reset();
                path.moveTo(x,y-r);
                path.lineTo(x+r*.72f,y-r*.52f);
                path.lineTo(x+r*.78f,y+r*.30f);
                path.lineTo(x,y+r);
                path.lineTo(x-r*.78f,y+r*.30f);
                path.lineTo(x-r*.72f,y-r*.52f);
                path.close();
                p.setColor(0xFF3E79A5);
                c.drawPath(path,p);
                p.setStyle(Paint.Style.STROKE);
                p.setStrokeWidth(Math.max(1.7f,2.2f*s));
                p.setColor(0xFFBFE7FF);
                c.drawPath(path,p);
                p.setStyle(Paint.Style.FILL);
                path.reset();
                path.moveTo(x,y-r*.67f);
                path.lineTo(x+r*.47f,y-r*.35f);
                path.lineTo(x+r*.49f,y+r*.18f);
                path.lineTo(x,y+r*.67f);
                path.lineTo(x-r*.49f,y+r*.18f);
                path.lineTo(x-r*.47f,y-r*.35f);
                path.close();
                p.setColor(0x5570C3E5);
                c.drawPath(path,p);
                p.setColor(0xFFD9F2FF);
                c.drawCircle(x-r*.20f,y-r*.28f,r*.10f,p);
            }
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
            // Stronger silhouette scale so the characters read clearly on phone screens.
            final float k=player?2.00f:(type==3?1.95f:type==2?1.86f:1.80f);
            final float u=s*k;
            p.setStyle(Paint.Style.FILL);
            p.setStrokeCap(Paint.Cap.ROUND);
            p.setStrokeJoin(Paint.Join.ROUND);

            // Grounded 2.5D shadow.
            p.setColor(0x76000000);
            c.drawOval(new RectF(x-34*u,y+34*u,x+34*u,y+49*u),p);
            p.setColor(0x30000000);
            c.drawOval(new RectF(x-45*u,y+25*u,x+45*u,y+43*u),p);

            // Rear silhouette / tactical backpack.
            int pack=player?0xFF223632:(type==3?0xFF3A2027:type==2?0xFF26333B:0xFF2C302E);
            p.setColor(pack);
            c.drawRoundRect(new RectF(x-25*u,y-3*u,x+25*u,y+31*u),9*u,9*u,p);
            p.setColor(player?0xFF3B5A54:(type==3?0xFF56323A:type==2?0xFF3C515E:0xFF46504C));
            c.drawRoundRect(new RectF(x-19*u,y+4*u,x+19*u,y+25*u),6*u,6*u,p);

            // Utility pack seams, radio and antenna.
            p.setColor(0xFF151B1A);
            c.drawRoundRect(new RectF(x+12*u,y-5*u,x+18*u,y+16*u),2*u,2*u,p);
            p.setStyle(Paint.Style.STROKE);
            p.setStrokeWidth(Math.max(1.4f,2f*u));
            p.setColor(0xFF77837C);
            c.drawLine(x+15*u,y-6*u,x+21*u,y-16*u,p);
            c.drawLine(x+9*u,y+8*u,x+17*u,y+8*u,p);
            p.setStyle(Paint.Style.FILL);
            p.setColor(0xFFD8B65C);
            c.drawCircle(x+15*u,y-6*u,2.8f*u,p);

            // Legs: segmented combat trousers.
            int pants=player?0xFF283C38:(type==3?0xFF2E2025:type==2?0xFF31404A:0xFF353A38);
            p.setColor(pants);
            c.drawRoundRect(new RectF(x-16*u,y+14*u,x-3*u,y+42*u),4*u,4*u,p);
            c.drawRoundRect(new RectF(x+3*u,y+14*u,x+16*u,y+42*u),4*u,4*u,p);

            // Fabric highlights / seams.
            p.setStyle(Paint.Style.STROKE);
            p.setStrokeWidth(Math.max(1.5f,2.1f*u));
            p.setColor(player?0xFF5B736C:(type==2?0xFF667984:0xFF5E625F));
            c.drawLine(x-11*u,y+17*u,x-11*u,y+38*u,p);
            c.drawLine(x+11*u,y+17*u,x+11*u,y+38*u,p);
            p.setStyle(Paint.Style.FILL);

            // Knee protection.
            int knee=player?0xFF738780:(type==3?0xFF7A4F54:type==2?0xFF647984:0xFF68716C);
            p.setColor(knee);
            c.drawRoundRect(new RectF(x-15*u,y+26*u,x-3*u,y+34*u),3*u,3*u,p);
            c.drawRoundRect(new RectF(x+3*u,y+26*u,x+15*u,y+34*u),3*u,3*u,p);
            p.setColor(0xFF17201E);
            c.drawRoundRect(new RectF(x-18*u,y+38*u,x-1*u,y+49*u),5*u,5*u,p);
            c.drawRoundRect(new RectF(x+1*u,y+38*u,x+18*u,y+49*u),5*u,5*u,p);
            p.setColor(0xFFB99D5B);
            c.drawRect(x-10*u,y+29*u,x-5*u,y+31*u,p);
            c.drawRect(x+5*u,y+29*u,x+10*u,y+31*u,p);

            // Waist harness.
            p.setColor(0xFF171E1C);
            c.drawRoundRect(new RectF(x-24*u,y+5*u,x+24*u,y+22*u),6*u,6*u,p);
            p.setColor(0xFFC3A05A);
            c.drawRect(x-22*u,y+18*u,x+22*u,y+21*u,p);

            // Torso base.
            int cloth=player?0xFF536F68:(type==3?0xFF653940:type==2?0xFF536873:0xFF555C57);
            p.setColor(cloth);
            c.drawRoundRect(new RectF(x-24*u,y-19*u,x+24*u,y+25*u),11*u,11*u,p);

            // Dark carrier outline makes the sprite feel illustrated rather than flat.
            p.setColor(0xFF1A2422);
            c.drawRoundRect(new RectF(x-19*u,y-13*u,x+19*u,y+19*u),7*u,7*u,p);
            p.setColor(player?0xFF6B857D:(type==3?0xFF7A4B51:type==2?0xFF6F828D:0xFF68716D));
            c.drawRoundRect(new RectF(x-15*u,y-11*u,x+15*u,y+15*u),5*u,5*u,p);

            // Ballistic chest plate.
            p.setColor(0xFF111817);
            c.drawRoundRect(new RectF(x-12*u,y-10*u,x+12*u,y+11*u),4*u,4*u,p);
            p.setColor(armor);
            c.drawRoundRect(new RectF(x-3*u,y-8*u,x+3*u,y+9*u),2*u,2*u,p);

            // Persian-inspired geometric insignia.
            p.setColor(player?0xFF2F8A83:(type==3?0xFF9C4B4F:type==2?0xFF4A819C:0xFF557B70));
            path.reset();
            path.moveTo(x,y-6*u);
            path.lineTo(x+6*u,y*u);
            path.lineTo(x,y+6*u);
            path.lineTo(x-6*u,y*u);
            path.close();
            c.drawPath(path,p);
            p.setStyle(Paint.Style.STROKE);
            p.setStrokeWidth(Math.max(1.2f,1.8f*u));
            p.setColor(0xFFE5C66A);
            c.drawLine(x-8*u,y+14*u,x+8*u,y+14*u,p);
            p.setStyle(Paint.Style.FILL);

            // Shoulder armor.
            int shoulder=player?0xFFB79D61:(type==3?0xFF9B656A:type==2?0xFF7D919D:0xFF7C837E);
            p.setColor(shoulder);
            c.drawOval(new RectF(x-29*u,y-14*u,x-12*u,y+4*u),p);
            c.drawOval(new RectF(x+12*u,y-14*u,x+29*u,y+4*u),p);
            p.setColor(0x55352B24);
            c.drawOval(new RectF(x-27*u,y-9*u,x-16*u,y+1*u),p);
            c.drawOval(new RectF(x+16*u,y-9*u,x+27*u,y+1*u),p);

            // Chest pouches / utility cells.
            p.setColor(0xFF242E2B);
            c.drawRoundRect(new RectF(x-23*u,y+7*u,x-12*u,y+20*u),2*u,2*u,p);
            c.drawRoundRect(new RectF(x+12*u,y+7*u,x+23*u,y+20*u),2*u,2*u,p);
            c.drawRoundRect(new RectF(x-9*u,y+12*u,x-1*u,y+22*u),2*u,2*u,p);
            c.drawRoundRect(new RectF(x+1*u,y+12*u,x+9*u,y+22*u),2*u,2*u,p);

            // Neck guard.
            p.setColor(0xFF252E2C);
            c.drawRoundRect(new RectF(x-10*u,y-23*u,x+10*u,y-14*u),3*u,3*u,p);

            // Head / jaw.
            p.setColor(player?0xFFC99A70:(type==3?0xFFB9826F:0xFFD0A17A));
            c.drawOval(new RectF(x-13*u,y-35*u,x+13*u,y-14*u),p);
            p.setColor(0xFF30231D);
            c.drawOval(new RectF(x-15*u,y-41*u,x+15*u,y-25*u),p);

            // Helmet shell and rim.
            int helmet=player?0xFF70896E:(type==3?0xFF58333A:type==2?0xFF4E6674:0xFF46524D);
            p.setColor(helmet);
            c.drawOval(new RectF(x-18*u,y-45*u,x+18*u,y-27*u),p);
            p.setColor(0xFF242C29);
            c.drawRoundRect(new RectF(x-20*u,y-34*u,x+20*u,y-26*u),3*u,3*u,p);

            // Helmet highlight strip.
            p.setColor(player?0xFFA7B38B:(type==2?0xFF8EA0A8:0xFF756D6C));
            c.drawRoundRect(new RectF(x-12*u,y-43*u,x+11*u,y-39*u),2*u,2*u,p);

            // Visor / goggles and eye slit.
            p.setColor(0xFF111615);
            c.drawRoundRect(new RectF(x-13*u,y-34*u,x+13*u,y-27*u),3*u,3*u,p);
            p.setColor(player?0xFF4A6C6A:(type==2?0xFF587C8F:0xFF593E42));
            c.drawRoundRect(new RectF(x-10*u,y-32*u,x-1*u,y-29*u),1.5f*u,1.5f*u,p);
            c.drawRoundRect(new RectF(x+1*u,y-32*u,x+10*u,y-29*u),1.5f*u,1.5f*u,p);
            p.setColor(0xFFD9BC6A);
            c.drawRect(x-7*u,y-29*u,x-2*u,y-28*u,p);
            c.drawRect(x+2*u,y-29*u,x+7*u,y-28*u,p);

            // Headset / helmet rails.
            p.setColor(0xFF414B47);
            c.drawRect(x-13*u,y-47*u,x+13*u,y-44*u,p);
            c.drawCircle(x-19*u,y-29*u,3.6f*u,p);
            c.drawCircle(x+19*u,y-29*u,3.6f*u,p);
            if(type==2&&!player){
                p.setColor(0xFF8EB1BF);
                c.drawCircle(x,y-45*u,3*u,p);
                p.setColor(0xFFCBD9DD);
                c.drawRect(x-1*u,y-52*u,x+1*u,y-45*u,p);
            }
            if(type==3&&!player){
                p.setColor(0xFFB47A47);
                c.drawRoundRect(new RectF(x-21*u,y-39*u,x-16*u,y-25*u),2*u,2*u,p);
                c.drawRoundRect(new RectF(x+16*u,y-39*u,x+21*u,y-25*u),2*u,2*u,p);
            }

            // Arm geometry follows existing facing only; no gameplay state is changed.
            float fx=player?core.player().facingX:1f;
            float fy=player?core.player().facingY*PITCH:0f;
            float fl=Math.max(.001f,(float)Math.hypot(fx,fy));
            fx/=fl; fy/=fl;
            float px=-fy,py=fx;

            // Upper arms, forearms and gloves as separate masses.
            p.setStrokeCap(Paint.Cap.ROUND);
            p.setStrokeWidth(Math.max(10f,12*u));
            p.setColor(player?0xFF4D625D:(type==3?0xFF62434A:type==2?0xFF526773:0xFF545B57));
            c.drawLine(x+px*16*u,y-1*u+py*16*u,x+fx*27*u+px*5*u,y+fy*27*u+py*5*u,p);
            c.drawLine(x-px*16*u,y-1*u-py*16*u,x+fx*25*u-px*5*u,y+fy*25*u-py*5*u,p);
            p.setStrokeWidth(Math.max(5f,7*u));
            p.setColor(0xFF141918);
            c.drawCircle(x+fx*28*u+px*5*u,y+fy*28*u+py*5*u,4.8f*u,p);
            c.drawCircle(x+fx*26*u-px*5*u,y+fy*26*u-py*5*u,4.8f*u,p);
            p.setStrokeCap(Paint.Cap.BUTT);

            // Weapon silhouette with receiver, grip, magazine, handguard, optic and barrel.
            float angle=(float)Math.toDegrees(Math.atan2(fy,fx));
            c.save();
            c.rotate(angle,x+fx*9*u,y+fy*9*u);
            float wx=x+fx*9*u,wy=y+fy*9*u;
            p.setStyle(Paint.Style.FILL);

            if(type==3&&!player){
                // Heavy gunner: bulkier receiver and box magazine.
                p.setColor(0xFF121716);
                c.drawRoundRect(new RectF(wx-23*u,wy-2*u,wx+43*u,wy+11*u),4*u,4*u,p);
                p.setColor(0xFF49534E);
                c.drawRoundRect(new RectF(wx+27*u,wy-5*u,wx+74*u,wy+2*u),2*u,2*u,p);
                p.setColor(0xFF0E1211);
                c.drawRoundRect(new RectF(wx-3*u,wy+9*u,wx+12*u,wy+28*u),3*u,3*u,p);
                p.setColor(0xFF303A36);
                c.drawRoundRect(new RectF(wx+7*u,wy+10*u,wx+24*u,wy+27*u),3*u,3*u,p);
                p.setColor(0xFFD0B265);
                c.drawRect(wx+63*u,wy-4*u,wx+77*u,wy+1*u,p);
                // Bipod hint.
                p.setStyle(Paint.Style.STROKE);
                p.setStrokeWidth(Math.max(2f,2.8f*u));
                p.setColor(0xFF58615C);
                c.drawLine(wx+41*u,wy+7*u,wx+54*u,wy+18*u,p);
                c.drawLine(wx+54*u,wy+18*u,wx+58*u,wy+8*u,p);
                p.setStyle(Paint.Style.FILL);
            }else if(type==2&&!player){
                // Recon: slimmer carbine and optic.
                p.setColor(0xFF151C1A);
                c.drawRoundRect(new RectF(wx-18*u,wy,wx+38*u,wy+9*u),3*u,3*u,p);
                p.setColor(0xFF65716D);
                c.drawRoundRect(new RectF(wx+25*u,wy-2*u,wx+65*u,wy+2*u),2*u,2*u,p);
                p.setColor(0xFF8F6139);
                c.drawRoundRect(new RectF(wx-3*u,wy+7*u,wx+7*u,wy+18*u),2*u,2*u,p);
                p.setColor(0xFF202A26);
                c.drawRoundRect(new RectF(wx+9*u,wy-7*u,wx+29*u,wy-2*u),2*u,2*u,p);
                p.setColor(0xFF9BBBC4);
                c.drawRect(wx+14*u,wy-9*u,wx+23*u,wy-6*u,p);
                c.drawRect(wx+32*u,wy-7*u,wx+38*u,wy-3*u,p);
            }else{
                // Assault rifle: player and regular enemy.
                p.setColor(0xFF141A18);
                c.drawRoundRect(new RectF(wx-19*u,wy,wx+42*u,wy+10*u),3*u,3*u,p);
                p.setColor(0xFF727A74);
                c.drawRoundRect(new RectF(wx+22*u,wy-2*u,wx+66*u,wy+2*u),2*u,2*u,p);
                p.setColor(0xFF0E1312);
                c.drawRoundRect(new RectF(wx-3*u,wy+8*u,wx+9*u,wy+23*u),2*u,2*u,p);
                p.setColor(0xFF8E6039);
                c.drawRoundRect(new RectF(wx+1*u,wy+7*u,wx+10*u,wy+17*u),2*u,2*u,p);
                // Rail and red-dot optic.
                p.setColor(0xFF3A4641);
                c.drawRect(wx+26*u,wy-7*u,wx+35*u,wy-2*u,p);
                p.setColor(0xFF6E7771);
                c.drawRect(wx+39*u,wy-8*u,wx+45*u,wy-3*u,p);
                p.setColor(type==1&&!player?0xFFB24A4F:0xFFD8B864);
                c.drawCircle(wx+42*u,wy-10*u,1.8f*u,p);
                // Handguard slots.
                p.setStyle(Paint.Style.STROKE);
                p.setStrokeWidth(Math.max(1.2f,1.8f*u));
                p.setColor(0xFF59635D);
                for(int i=0;i<4;i++) c.drawLine(wx+28*u+i*7*u,wy+2*u,wx+32*u+i*7*u,wy+2*u,p);
                p.setStyle(Paint.Style.FILL);
                p.setColor(0xFFD6B45F);
                c.drawRect(wx+60*u,wy-3*u,wx+68*u,wy+2*u,p);
            }

            // Rifle sling and subtle metal highlight.
            p.setStyle(Paint.Style.STROKE);
            p.setStrokeWidth(Math.max(1.8f,2.2f*u));
            p.setColor(0x885E665F);
            c.drawLine(wx-11*u,wy+11*u,wx+33*u,wy+24*u,p);
            p.setStrokeWidth(Math.max(1f,1.4f*u));
            p.setColor(0xAA9FA8A1);
            c.drawLine(wx+17*u,wy-3*u,wx+48*u,wy-3*u,p);
            p.setStyle(Paint.Style.FILL);
            c.restore();

            // Existing fire input only controls this visual flash.
            if(player && input.fire){
                float mx=x+fx*70*u,my=y+fy*70*u;
                p.setColor(0xFFFFDA70);
                path.reset();
                path.moveTo(mx,my);
                path.lineTo(mx+fx*19*u+px*8*u,my+fy*19*u+py*8*u);
                path.lineTo(mx+fx*19*u-px*8*u,my+fy*19*u-py*8*u);
                path.close();
                c.drawPath(path,p);
                p.setColor(0x66FFF0AA);
                c.drawCircle(mx,my,6*u,p);
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
            // Larger tactical map: readable at phone scale, but still leaves gameplay visible.
            float size=Math.min(getWidth()*.37f,dp(330f));
            float mapH=size*.86f;
            float left=getWidth()-size-dp(14f),top=HUD+dp(10f);
            float radius=dp(18f);

            // Outer shadow / frame.
            p.setStyle(Paint.Style.FILL);
            p.setColor(0x65000000);
            c.drawRoundRect(new RectF(left+dp(4f),top+dp(5f),left+size+dp(4f),top+mapH+dp(5f)),radius,radius,p);
            p.setColor(0xE318211D);
            c.drawRoundRect(new RectF(left,top,left+size,top+mapH),radius,radius,p);

            // Inner map surface.
            p.setColor(0xFF25322C);
            c.drawRoundRect(new RectF(left+dp(7f),top+dp(7f),left+size-dp(7f),top+mapH-dp(7f)),radius*.75f,radius*.75f,p);

            float innerL=left+dp(10f),innerT=top+dp(27f),innerW=size-dp(20f),innerH=mapH-dp(36f);
            float mx=innerW/WorldMap.SIZE,my=innerH/WorldMap.SIZE;

            c.save();
            c.clipRect(innerL,innerT,innerL+innerW,innerT+innerH);

            // Fine tactical grid.
            p.setStyle(Paint.Style.STROKE);
            p.setStrokeWidth(1f);
            p.setColor(0x182C9C75);
            float grid=WorldMap.SIZE/8f;
            for(int i=0;i<=8;i++){
                float gx=innerL+i*grid*mx,gy=innerT+i*grid*my;
                c.drawLine(gx,innerT,gx,innerT+innerH,p);
                c.drawLine(innerL,gy,innerL+innerW,gy,p);
            }

            // Roads.
            for(WorldMap.Road r:world.roads()){
                p.setColor(0xAA5E6B63);
                p.setStrokeWidth(Math.max(dp(2f),r.width*mx*.38f));
                c.drawLine(innerL+r.x1*mx,innerT+r.y1*my,innerL+r.x2*mx,innerT+r.y2*my,p);
                p.setColor(0x667F8D83);
                p.setStrokeWidth(Math.max(1f,r.width*mx*.10f));
                c.drawLine(innerL+r.x1*mx,innerT+r.y1*my,innerL+r.x2*mx,innerT+r.y2*my,p);
            }

            // Buildings as varied blocks.
            p.setStyle(Paint.Style.FILL);
            for(WorldMap.Building b:world.buildings()){
                int bc=(b.style%4==0)?0xB36A5949:(b.style%4==1?0xB36E624E:0xB35D5349);
                p.setColor(bc);
                c.drawRoundRect(new RectF(innerL+b.x*mx,innerT+b.y*my,
                        innerL+(b.x+b.w)*mx,innerT+(b.y+b.h)*my),dp(1.5f),dp(1.5f),p);
            }

            // World-zone ring.
            float zx=innerL+WorldMap.SIZE*.5f*mx,zy=innerT+WorldMap.SIZE*.5f*my;
            float zr=core.zoneRadius()*mx;
            p.setStyle(Paint.Style.STROKE);
            p.setStrokeWidth(Math.max(1.5f,dp(2f)));
            p.setColor(0x8C83C8E8);
            c.drawOval(new RectF(zx-zr,zy-zr*PITCH,zx+zr,zy+zr*PITCH),p);

            // Pickups are deliberately visible on the minimap too.
            for(GameCore.Pickup item:core.pickups()){
                float ix=innerL+item.x*mx,iy=innerT+item.y*my;
                p.setStyle(Paint.Style.FILL);
                p.setColor(item.type==GameCore.PickupType.AMMO?0xFFF0C85D:
                        item.type==GameCore.PickupType.MEDKIT?0xFFE06B62:
                        item.type==GameCore.PickupType.GRENADE?0xFF71B47E:0xFF72B6E2);
                c.drawCircle(ix,iy,dp(2.7f),p);
            }

            // Enemy signals.
            for(GameCore.Enemy e:core.enemies())if(!e.dead){
                float ex=innerL+e.x*mx,ey=innerT+e.y*my;
                p.setStyle(Paint.Style.FILL);
                p.setColor(e.type==3?0xFFFFAF45:e.type==2?0xFF7FC7E4:0xFFE66A63);
                c.drawCircle(ex,ey,e.type==3?dp(3.2f):dp(2.5f),p);
            }

            // Player marker as a directional chevron.
            float px=innerL+core.player().x*mx,py=innerT+core.player().y*my;
            float fx=core.player().facingX,fy=core.player().facingY,fl=Math.max(.001f,(float)Math.hypot(fx,fy));
            fx/=fl;fy/=fl;
            float sideX=-fy,sideY=fx;
            p.setColor(0xFF7CFF9A);
            path.reset();
            path.moveTo(px+fx*dp(8f),py+fy*dp(8f));
            path.lineTo(px-sideX*dp(5f)-fx*dp(4f),py-sideY*dp(5f)-fy*dp(4f));
            path.lineTo(px+sideX*dp(5f)-fx*dp(4f),py+sideY*dp(5f)-fy*dp(4f));
            path.close();
            c.drawPath(path,p);

            c.restore();

            // Header and compass.
            p.setStyle(Paint.Style.FILL);
            p.setColor(0xFFF0D178);
            p.setTypeface(PaintCompat.BOLD);
            p.setTextAlign(Paint.Align.LEFT);
            p.setTextSize(dp(13f));
            c.drawText("TACTICAL MAP",left+dp(14f),top+dp(18f),p);
            p.setColor(0xFFDCE5DF);
            p.setTypeface(null);
            p.setTextSize(dp(9f));
            c.drawText("CITY GRID",left+dp(14f),top+dp(29f),p);
            p.setTextAlign(Paint.Align.RIGHT);
            p.setTypeface(PaintCompat.BOLD);
            p.setTextSize(dp(10f));
            c.drawText("N",left+size-dp(15f),top+dp(18f),p);
            c.drawText("ZONE",left+size-dp(15f),top+dp(30f),p);
            p.setTypeface(null);
            p.setTextAlign(Paint.Align.LEFT);
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
