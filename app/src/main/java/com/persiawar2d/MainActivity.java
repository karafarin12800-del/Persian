package com.persiawar2d;

import android.app.Activity;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.DashPathEffect;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.graphics.LinearGradient;
import android.graphics.RadialGradient;
import android.graphics.Shader;
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
    float t=(System.nanoTime()%12000000000L)/1e9f;
    p.setStyle(Paint.Style.FILL);
    p.setShader(new LinearGradient(0,HUD,0,getHeight(),
            0xFF6B6654,0xFF2F3530,Shader.TileMode.CLAMP));
    c.drawRect(0,HUD,getWidth(),getHeight(),p);
    p.setShader(null);

    // Large paving panels give the battlefield a deliberate urban material language.
    float step=240f;
    float startX=(float)Math.floor((core.player().x-1600)/step)*step;
    float startY=(float)Math.floor((core.player().y-1100)/step)*step;
    p.setStyle(Paint.Style.STROKE);
    p.setStrokeWidth(Math.max(1f,0.9f*s));
    p.setColor(0x253A403A);
    for(float x=startX;x<core.player().x+1700;x+=step)c.drawLine(sx(x),HUD,sx(x),getHeight(),p);
    for(float y=startY;y<core.player().y+1150;y+=step)c.drawLine(0,sy(y),getWidth(),sy(y),p);

    // Fine concrete/grit texture, deterministically distributed in world space.
    p.setStyle(Paint.Style.FILL);
    for(int gx=-18;gx<=18;gx++){
        for(int gy=-9;gy<=9;gy++){
            float wx=((float)Math.floor(core.player().x/95f)+gx)*95f+17f*((gy&3)-1);
            float wy=((float)Math.floor(core.player().y/95f)+gy)*95f+11f*((gx&3)-1);
            if(Math.abs(wx-core.player().x)>1650||Math.abs(wy-core.player().y)>1050)continue;
            int n=(int)Math.abs(wx*31+wy*17);
            float a=8f+(n%10);
            p.setColor((n%3==0?0x16000000:0x10FFFFFF)|(((int)a)&0xFF)<<24);
            c.drawCircle(sx(wx),sy(wy),Math.max(.7f,(n%4+1)*.65f*s),p);
        }
    }

    // Dry grass, stones and dust flecks around non-road terrain.
    p.setColor(0x2CBCD28C);
    for(int i=0;i<world.grass().size();i+=2){
        WorldMap.Prop g=world.grass().get(i);
        if(Math.abs(g.x-core.player().x)>2100||Math.abs(g.y-core.player().y)>1250)continue;
        float x=sx(g.x),y=sy(g.y),r=Math.max(1f,g.size*s*.10f);
        c.drawLine(x-r,y+r*1.6f,x,y-r,p); c.drawLine(x,y-r,x+r,y+r*1.6f,p);
    }

    // Subtle moving dust light keeps the scene from feeling static.
    p.setShader(new RadialGradient((float)(cx()+Math.sin(t*.35f)*getWidth()*.32f),
            HUD+(getHeight()-HUD)*.48f,
            Math.max(getWidth(),getHeight())*.55f,
            new int[]{0x18FFE4A3,0x05000000,0x00000000},
            new float[]{0f,.55f,1f},Shader.TileMode.CLAMP));
    c.drawRect(0,HUD,getWidth(),getHeight(),p);
    p.setShader(null);

    // Bottom vignette.
    p.setShader(new LinearGradient(0,getHeight()-getHeight()*.28f,0,getHeight(),
            0x00000000,0x5A101613,Shader.TileMode.CLAMP));
    c.drawRect(0,HUD,getWidth(),getHeight(),p);
    p.setShader(null);
}

        private void drawRoads(Canvas c){
    float s=sc();
    for(WorldMap.Road r:world.roads()){
        p.setStyle(Paint.Style.FILL);
        if(r.horizontal){
            float l=sx(r.x1), rr=sx(r.x2);
            float top=sy(r.y1-r.width*.5f), bot=sy(r.y1+r.width*.5f);
            p.setColor(0x60000000); c.drawRect(l+5*s,top+7*s,rr+8*s,bot+8*s,p);
            p.setShader(new LinearGradient(0,top,0,bot,0xFF3B403D,0xFF272B2A,Shader.TileMode.CLAMP));
            c.drawRect(l,top,rr,bot,p); p.setShader(null);
            p.setColor(0xFFB79D63); c.drawRect(l,top,rr,top+4*s,p);
            p.setColor(0xFF6F6048); c.drawRect(l,bot-3*s,rr,bot,p);
            p.setStyle(Paint.Style.STROKE);
            p.setStrokeWidth(Math.max(2.2f,4*s)); p.setPathEffect(new DashPathEffect(new float[]{26*s,24*s},0));
            p.setColor(0xC6D7C38B); c.drawLine(sx(r.x1),sy(r.y1),sx(r.x2),sy(r.y2),p); p.setPathEffect(null);
            // Center glints / wear.
            p.setStrokeWidth(Math.max(1f,1.6f*s)); p.setColor(0x22FFFFFF);
            c.drawLine(l,top+9*s,rr,top+9*s,p);
        }else{
            float l=sx(r.x1-r.width*.5f), rr=sx(r.x1+r.width*.5f);
            float top=sy(r.y1), bot=sy(r.y2);
            p.setColor(0x60000000); c.drawRect(l+5*s,top+6*s,rr+8*s,bot+8*s,p);
            p.setShader(new LinearGradient(l,0,rr,0,0xFF3B403D,0xFF252928,Shader.TileMode.CLAMP));
            c.drawRect(l,top,rr,bot,p); p.setShader(null);
            p.setColor(0xFFB79D63); c.drawRect(l,top,l+4*s,bot,p);
            p.setColor(0xFF6F6048); c.drawRect(rr-3*s,top,rr,bot,p);
            p.setStyle(Paint.Style.STROKE);
            p.setStrokeWidth(Math.max(2.2f,4*s)); p.setPathEffect(new DashPathEffect(new float[]{26*s,24*s},0));
            p.setColor(0xC6D7C38B); c.drawLine(sx(r.x1),sy(r.y1),sx(r.x2),sy(r.y2),p); p.setPathEffect(null);
            p.setStrokeWidth(Math.max(1f,1.6f*s)); p.setColor(0x22FFFFFF);
            c.drawLine(l+9*s,top, l+9*s,bot,p);
        }

        // Small road furniture accents at endpoints make intersections read more naturally.
        float ex=r.horizontal?sx(r.x2):sx(r.x1), ey=r.horizontal?sy(r.y2):sy(r.y2);
        p.setStyle(Paint.Style.FILL); p.setColor(0x77504738);
        c.drawCircle(ex,ey,Math.max(2,5*s),p);
    }
}

        private void drawBuildings(Canvas c){
    float s=sc();
    for(WorldMap.Building b:world.buildings()){
        if(Math.abs(b.x+b.w*.5f-core.player().x)>2050||Math.abs(b.y+b.h*.5f-core.player().y)>1200)continue;
        float l=sx(b.x),r=sx(b.x+b.w),base=sy(b.y+b.h),back=sy(b.y);
        float lift=(82+(b.style%4)*13)*s;

        p.setStyle(Paint.Style.FILL);
        p.setColor(0x5F000000);
        c.drawRoundRect(new RectF(l+11*s,back-lift+13*s,r+15*s,base+15*s),10*s,10*s,p);

        // Main facade with directional light gradient.
        int dark=(b.style%5==0)?0xFF514337:(b.style%5==1?0xFF5C4A38:0xFF4C4036);
        int light=(b.style%5==0)?0xFF947457:(b.style%5==1?0xFF9A7B5A:0xFF876B53);
        p.setShader(new LinearGradient(l,0,r,0,light,dark,Shader.TileMode.CLAMP));
        c.drawRoundRect(new RectF(l,back-lift,r,base),7*s,7*s,p); p.setShader(null);

        // Roof slab + parapet.
        path.reset();
        path.moveTo(l-8*s,back-lift+7*s);
        path.lineTo(l+6*s,back-lift-8*s);
        path.lineTo(r-6*s,back-lift-18*s);
        path.lineTo(r+8*s,back-lift+5*s);
        path.lineTo(r-1*s,back-lift+13*s);
        path.lineTo(l+2*s,back-lift+14*s);
        path.close();
        p.setColor((b.style%3==0)?0xFF2E2824:0xFF362C27); c.drawPath(path,p);
        p.setColor(0xFFD0B16F); c.drawRect(l,back-lift+13*s,r,back-lift+18*s,p);
        p.setColor(0x55302822); c.drawRect(l,base-13*s,r,base-8*s,p);

        int cols=Math.max(2,Math.min(6,(int)(b.w/90)));
        int rows=Math.max(1,Math.min(2,(int)(b.h/120)));
        for(int row=0;row<rows;row++){
            for(int i=0;i<cols;i++){
                float frac=(i+.5f)/cols;
                float x=l+frac*(r-l);
                float fy=(row+.9f)/(rows+1.2f);
                float y=(back-lift)+(base-(back-lift))*fy;
                float ww=11*s,hh=17*s;
                p.setColor((b.style+i+row)%4==0?0xFFE0C37B:0xFFB48F5B);
                c.drawRoundRect(new RectF(x-ww,y-hh,x+ww,y+hh),7*s,7*s,p);
                p.setShader(new LinearGradient(x-ww, y-hh, x+ww, y+hh,
                        0xFF263D45,0xFF10191C,Shader.TileMode.CLAMP));
                c.drawRoundRect(new RectF(x-ww+3*s,y-hh+3*s,x+ww-3*s,y+hh-2*s),5*s,5*s,p);
                p.setShader(null);
                p.setColor(0x35FFE8A8);
                c.drawRect(x-2*s,y-hh+4*s,x+2*s,y+hh-3*s,p);
            }
        }

        // Door + canopy + tile band.
        float dx=(l+r)*.5f;
        p.setColor(0xFFD1B36C); c.drawRoundRect(new RectF(dx-21*s,base-66*s,dx+21*s,base+2*s),12*s,12*s,p);
        p.setColor(0xFF342720); c.drawRoundRect(new RectF(dx-14*s,base-57*s,dx+14*s,base+2*s),9*s,9*s,p);
        p.setColor(0xFF3A7D84); c.drawRect(l+16*s,back-lift+26*s,r-16*s,back-lift+31*s,p);
        p.setColor(0x66E9D59A); c.drawRect(l+17*s,back-lift+32*s,r-17*s,back-lift+34*s,p);

        // Rooftop mechanicals: tank, vents and small satellite/antenna shapes.
        p.setColor(0xFF4C514D);
        c.drawRoundRect(new RectF(l+18*s,back-lift-3*s,l+34*s,back-lift+10*s),3*s,3*s,p);
        p.setColor(0xFF9C8F70); c.drawRect(l+22*s,back-lift-8*s,l+30*s,back-lift-2*s,p);
        p.setStyle(Paint.Style.STROKE); p.setStrokeWidth(Math.max(1.3f,2*s)); p.setColor(0xFFAAA18B);
        c.drawLine(r-22*s,back-lift+7*s,r-22*s,back-lift-13*s,p);
        c.drawCircle(r-22*s,back-lift-15*s,4*s,p);
        p.setStyle(Paint.Style.FILL);
        p.setColor(0xFFD5B45E); c.drawCircle(dx,back-lift-20*s,3.3f*s,p);
    }
}

        private void drawVehicles(Canvas c){
    float s=sc();
    for(WorldMap.Vehicle v:world.vehicles()){
        if(Math.abs(v.x-core.player().x)>1900||Math.abs(v.y-core.player().y)>1100)continue;
        float x=sx(v.x),y=sy(v.y);
        float w=v.w*s,h=v.h*s;
        c.save(); c.rotate(v.angle,x,y);

        p.setStyle(Paint.Style.FILL); p.setColor(0x65000000);
        c.drawRoundRect(new RectF(x-w*.55f,y-h*.35f,x+w*.55f,y+h*.50f),10*s,10*s,p);
        p.setShader(new LinearGradient(x-w*.5f,y-h*.5f,x+w*.5f,y+h*.5f,
                0xFF50615F,0xFF1E282A,Shader.TileMode.CLAMP));
        c.drawRoundRect(new RectF(x-w*.5f,y-h*.5f,x+w*.5f,y+h*.5f),9*s,9*s,p); p.setShader(null);

        p.setColor(0xFF182126); c.drawRoundRect(new RectF(x-w*.22f,y-h*.31f,x+w*.22f,y+h*.05f),4*s,4*s,p);
        p.setColor(0xFF7C8E8C); c.drawRect(x-w*.05f,y-h*.48f,x+w*.05f,y-h*.34f,p);
        p.setColor(0xFFA56B3A); c.drawRect(x-w*.42f,y-h*.47f,x-w*.31f,y-h*.24f,p);
        p.setColor(0xFFD8C166); c.drawRect(x+w*.31f,y-h*.47f,x+w*.42f,y-h*.24f,p);
        p.setColor(0xFF111616);
        c.drawOval(new RectF(x-w*.42f,y+h*.22f,x-w*.27f,y+h*.55f),p);
        c.drawOval(new RectF(x+w*.27f,y+h*.22f,x+w*.42f,y+h*.55f),p);
        p.setColor(0xFFD9D4C5);
        c.drawCircle(x-w*.34f,y+h*.21f,Math.max(1.5f,2.2f*s),p);
        c.drawCircle(x+w*.34f,y+h*.21f,Math.max(1.5f,2.2f*s),p);
        p.setStyle(Paint.Style.STROKE); p.setStrokeWidth(Math.max(1.2f,1.7f*s)); p.setColor(0x557BC9C1);
        c.drawRoundRect(new RectF(x-w*.43f,y-h*.46f,x+w*.43f,y+h*.42f),7*s,7*s,p);
        p.setStyle(Paint.Style.FILL);
        c.restore();
    }
}

        private void drawFences(Canvas c){
    float s=sc();
    for(WorldMap.Fence f:world.fences()){
        if(Math.abs((f.x1+f.x2)*.5f-core.player().x)>1900||Math.abs((f.y1+f.y2)*.5f-core.player().y)>1100)continue;
        float x1=sx(f.x1),y1=sy(f.y1),x2=sx(f.x2),y2=sy(f.y2);
        p.setStyle(Paint.Style.STROKE); p.setStrokeCap(Paint.Cap.ROUND);
        p.setStrokeWidth(Math.max(5,7*s)); p.setColor(0x71000000); c.drawLine(x1+2*s,y1+4*s,x2+2*s,y2+4*s,p);
        p.setStrokeWidth(Math.max(3,5*s)); p.setColor(0xFF70593E); c.drawLine(x1,y1,x2,y2,p);
        p.setStrokeWidth(Math.max(1.2f,1.8f*s)); p.setColor(0xFFD2AF66); c.drawLine(x1,y1-2*s,x2,y2-2*s,p);
        p.setStyle(Paint.Style.FILL); p.setColor(0xFF493C30);
        c.drawCircle(x1,y1,5*s,p); c.drawCircle(x2,y2,5*s,p);
        p.setColor(0xFFD0AD63); c.drawCircle(x1,y1-2*s,2*s,p); c.drawCircle(x2,y2-2*s,2*s,p);
        p.setStrokeCap(Paint.Cap.BUTT);
    }
}

        private void drawNature(Canvas c){
    float s=sc();
    int treeStep=Math.max(1,world.trees().size()/88);
    for(int i=0;i<world.trees().size();i+=treeStep){
        WorldMap.Prop t=world.trees().get(i);
        if(Math.abs(t.x-core.player().x)>1800||Math.abs(t.y-core.player().y)>1050)continue;
        float x=sx(t.x),y=sy(t.y),r=(18+t.size*.58f)*s;
        p.setStyle(Paint.Style.FILL); p.setColor(0x5A000000);
        c.drawOval(new RectF(x-r*.95f,y+r*.20f,x+r*.95f,y+r*.72f),p);
        p.setColor(0xFF5C3D2C); c.drawRoundRect(new RectF(x-5*s,y-r*.1f,x+5*s,y+r*.53f),3*s,3*s,p);
        p.setColor(0xFF183A2B); c.drawCircle(x,y-r*.16f,r,p);
        p.setShader(new RadialGradient(x-r*.18f,y-r*.43f,r*.92f,
                new int[]{0xFF5A9B62,0xFF2D6E46,0xFF173B2B},
                new float[]{0f,.58f,1f},Shader.TileMode.CLAMP));
        c.drawCircle(x,y-r*.20f,r,p); p.setShader(null);
        p.setColor(0x6099C970); c.drawCircle(x-r*.35f,y-r*.42f,r*.38f,p);
        p.setColor(0x4AFFFFFF); c.drawCircle(x-r*.24f,y-r*.53f,r*.16f,p);
    }

    int bushStep=Math.max(1,world.bushes().size()/76);
    for(int i=0;i<world.bushes().size();i+=bushStep){
        WorldMap.Prop b=world.bushes().get(i);
        if(Math.abs(b.x-core.player().x)>1800||Math.abs(b.y-core.player().y)>1050)continue;
        float x=sx(b.x),y=sy(b.y),r=Math.max(3,b.size*.75f*s);
        p.setColor(0x4B000000); c.drawOval(new RectF(x-r,y+r*.08f,x+r,y+r*.55f),p);
        p.setShader(new RadialGradient(x-r*.2f,y-r*.2f,Math.max(4,r),
                new int[]{0xFF619A5D,0xFF2C6844,0xFF163929},
                new float[]{0f,.62f,1f},Shader.TileMode.CLAMP));
        c.drawCircle(x,y,r,p); p.setShader(null);
        p.setColor(0x70FFFFFF); c.drawCircle(x-r*.28f,y-r*.27f,r*.16f,p);
    }

    // Small rubble stones around the scene.
    for(int i=0;i<world.grass().size();i+=7){
        WorldMap.Prop g=world.grass().get(i);
        if(Math.abs(g.x-core.player().x)>1750||Math.abs(g.y-core.player().y)>1000)continue;
        int n=(int)Math.abs(g.x*13+g.y*7);
        float rr=(1.5f+n%4)*s, x=sx(g.x+12), y=sy(g.y+9);
        p.setColor(0x55362E26); c.drawOval(new RectF(x-rr,y-rr*.45f,x+rr,y+rr*.55f),p);
        p.setColor(0x806E6252); c.drawCircle(x-rr*.2f,y-rr*.15f,Math.max(1,rr*.45f),p);
    }
}

        private void drawCombat(Canvas c){
    float s=sc();
    float t=(System.nanoTime()%10000000000L)/1e9f;

    for(GameCore.Pickup item:core.pickups()){
        float x=sx(item.x),y=sy(item.y);
        if(x<-80||x>getWidth()+80||y<HUD-100||y>getHeight()+110)continue;
        float pulse=1f+.06f*(float)Math.sin(t*3f+item.x*.01f);
        p.setStyle(Paint.Style.FILL);
        p.setColor(0x25000000); c.drawCircle(x,y,28*s,p);
        p.setStyle(Paint.Style.STROKE); p.setStrokeWidth(Math.max(1.5f,2*s));
        p.setColor(item.type==GameCore.PickupType.AMMO?0x7070D7A2:
                item.type==GameCore.PickupType.MEDKIT?0x70FF7770:
                item.type==GameCore.PickupType.GRENADE?0x7066B17D:0x707CCBFF);
        c.drawCircle(x,y,20*s*pulse,p); p.setStyle(Paint.Style.FILL);
        drawPickupIcon(c,x,y,s,item.type);
    }

    for(GameCore.Grenade g:core.grenades()){
        float x=sx(g.x),y=sy(g.y),r=Math.max(dp(6f),12*s);
        p.setStyle(Paint.Style.STROKE); p.setStrokeWidth(Math.max(1f,2*s)); p.setColor(0x4DFFE09A);
        c.drawCircle(x,y,r*1.7f,p);
        p.setStyle(Paint.Style.FILL); p.setColor(0x42000000);
        c.drawOval(new RectF(x-r*1.15f,y+r*.50f,x+r*1.15f,y+r*.92f),p);
        p.setShader(new RadialGradient(x-r*.35f,y-r*.38f,r*1.2f,
                new int[]{0xFF687C69,0xFF33463B,0xFF18231E},
                new float[]{0f,.55f,1f},Shader.TileMode.CLAMP));
        c.drawCircle(x,y,r,p); p.setShader(null);
        p.setColor(0xFFE2C978); c.drawCircle(x,y-r*.82f,r*.13f,p);
        p.setStyle(Paint.Style.STROKE); p.setStrokeWidth(Math.max(1.3f,2*s)); c.drawLine(x,y-r*.72f,x+r*.65f,y-r*1.05f,p);
        p.setStyle(Paint.Style.FILL); p.setColor(0x95D7E9C6); c.drawCircle(x-r*.28f,y-r*.30f,r*.22f,p);
    }

    for(GameCore.Projectile b:core.projectiles()){
        float x=sx(b.x),y=sy(b.y);
        float ox=sx(b.x-b.vx*.055f),oy=sy(b.y-b.vy*.055f);
        p.setStyle(Paint.Style.STROKE); p.setStrokeCap(Paint.Cap.ROUND);
        p.setStrokeWidth(Math.max(dp(5f),7*s)); p.setColor(b.fromPlayer?0x308FF3FF:0x30FF5C56); c.drawLine(ox,oy,x,y,p);
        p.setStrokeWidth(Math.max(dp(2f),3.2f*s)); p.setColor(b.fromPlayer?0xFFE8D37A:0xFFFF6F63); c.drawLine(ox,oy,x,y,p);
        p.setStyle(Paint.Style.FILL);
        p.setShader(new RadialGradient(x,y,Math.max(dp(7f),7*s),
                0xB8FFFFFF,b.fromPlayer?0x00FFE17A:0x00FF5B50,Shader.TileMode.CLAMP));
        c.drawCircle(x,y,Math.max(dp(5f),7*s),p); p.setShader(null);
        p.setColor(Color.WHITE); c.drawCircle(x,y,Math.max(1f,1.5f*s),p); p.setStrokeCap(Paint.Cap.BUTT);
    }

    for(GameCore.Enemy e:core.enemies()){
        float x=sx(e.x),y=sy(e.y);
        if(x<-120||x>getWidth()+120||y<HUD-110||y>getHeight()+120)continue;
        if(e.dead){drawDeath(c,x,y,s,e);continue;}
        float bob=(float)Math.sin(t*5.2f+e.x*.008f+e.y*.006f)*1.5f*s;
        if(e.state==GameCore.EnemyState.ATTACK){
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(1.5f,2.5f*s));p.setColor(0x65FF6A5B);
            c.drawCircle(x,y+bob,31*s,p);
        }
        drawWarrior(c,x,y+bob,s,e.type,e.hp<e.maxHp?0xFFD9A56A:e.state==GameCore.EnemyState.ATTACK?0xFFB8423E:0xFF8B3340,false);
        if(e.hp<e.maxHp){
            p.setStyle(Paint.Style.FILL);
            p.setColor(0x8A141816);c.drawRoundRect(new RectF(x-28*s,y-54*s,x+28*s,y-47*s),4*s,4*s,p);
            p.setColor(0xFFE05A51);c.drawRoundRect(new RectF(x-28*s,y-54*s,x-28*s+56*s*e.hp/e.maxHp,y-47*s),4*s,4*s,p);
            p.setColor(0xA8F3E0B0);c.drawRect(x-28*s,y-54*s,x-28*s+56*s*e.hp/e.maxHp,y-51*s,p);
        }
    }

    drawPlayer(c);

    for(GameCore.Explosion ex:core.explosions()){
        float x=sx(ex.x),y=sy(ex.y),age=1-ex.life/.38f;
        age=Math.max(0,Math.min(1,age));
        float r=ex.radius*s*(.22f+age*.95f);

        p.setStyle(Paint.Style.FILL);
        p.setColor(0x5B000000); c.drawOval(new RectF(x-r*.85f,y+r*.35f,x+r*.85f,y+r*.72f),p);
        p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,10*s*(1-age)));
        p.setColor(0xFFFFC34D);c.drawCircle(x,y,r,p);
        p.setStrokeWidth(Math.max(1,5*s*(1-age)));p.setColor(0xFFFF7347);c.drawCircle(x,y,r*.66f,p);

        p.setStyle(Paint.Style.FILL);
        p.setShader(new RadialGradient(x,y,r*.85f,
                new int[]{0xF7FFE18A,0xD9FF8D45,0x66E6382B,0x001A100E},
                new float[]{0f,.30f,.62f,1f},Shader.TileMode.CLAMP));
        c.drawCircle(x,y,r*.78f,p);p.setShader(null);

        // Smoke lobes expand upward while the blast fades.
        for(int i=0;i<5;i++){
            double a=i*Math.PI*2/5.0+t*.35;
            float sr=r*(.16f+.10f*i)*(1-age*.25f);
            float xx=x+(float)Math.cos(a)*r*.42f;
            float yy=y+(float)Math.sin(a)*r*.30f-r*age*.20f;
            p.setColor(0x2D20201D);c.drawCircle(xx,yy,sr,p);
        }
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

            // Player-only Persian visual pass: lamellar collar, cloak tabs, crest and arm guards.
            // Render-only: gameplay state, hitboxes, controls and weapon logic remain unchanged.
            if(player){
                p.setStyle(Paint.Style.FILL);
                p.setColor(0xFF9B8050);
                path.reset();
                path.moveTo(x-15*u,y-20*u);path.lineTo(x-25*u,y-7*u);path.lineTo(x-19*u,y+2*u);path.lineTo(x-12*u,y-10*u);path.close();c.drawPath(path,p);
                path.reset();
                path.moveTo(x+15*u,y-20*u);path.lineTo(x+25*u,y-7*u);path.lineTo(x+19*u,y+2*u);path.lineTo(x+12*u,y-10*u);path.close();c.drawPath(path,p);
                p.setColor(0xFFD0B16A);
                for(int row=0;row<3;row++){
                    float yy=y-4*u+row*6*u;
                    c.drawRoundRect(new RectF(x-10*u,yy,x-2*u,yy+4*u),1.5f*u,1.5f*u,p);
                    c.drawRoundRect(new RectF(x+2*u,yy,x+10*u,yy+4*u),1.5f*u,1.5f*u,p);
                }
                p.setColor(0xFF355B52);
                path.reset();path.moveTo(x-27*u,y-5*u);path.lineTo(x-34*u,y+20*u);path.lineTo(x-20*u,y+15*u);path.lineTo(x-17*u,y-8*u);path.close();c.drawPath(path,p);
                path.reset();path.moveTo(x+27*u,y-5*u);path.lineTo(x+34*u,y+20*u);path.lineTo(x+20*u,y+15*u);path.lineTo(x+17*u,y-8*u);path.close();c.drawPath(path,p);
                p.setColor(0xFFB68C43);
                path.reset();path.moveTo(x-2*u,y-45*u);path.quadTo(x-10*u,y-58*u,x-1*u,y-66*u);path.quadTo(x+7*u,y-57*u,x+3*u,y-45*u);path.close();c.drawPath(path,p);
                p.setColor(0xFFE0C06B);c.drawOval(new RectF(x-2*u,y-60*u,x+3*u,y-48*u),p);
                p.setColor(0xFF9E8250);
                c.drawRoundRect(new RectF(x-29*u,y-2*u,x-23*u,y+12*u),2*u,2*u,p);
                c.drawRoundRect(new RectF(x+23*u,y-2*u,x+29*u,y+12*u),2*u,2*u,p);
                p.setColor(0xFFD6B96D);c.drawCircle(x,y+19*u,3.5f*u,p);
                p.setColor(0xFF6E5833);c.drawCircle(x,y+19*u,1.5f*u,p);
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
                float mx=x+fx*77*u,my=y+fy*77*u;
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
    float pulse=1f+.025f*(float)Math.sin((System.nanoTime()%5000000000L)/1e9f*2.2f);
    p.setStyle(Paint.Style.STROKE);
    p.setStrokeWidth(Math.max(2,7*s));
    p.setColor(0x1737B7FF);c.drawOval(new RectF(zx-rx,zy-ry,zx+rx,zy+ry),p);
    p.setStrokeWidth(Math.max(1.5f,3*s));
    p.setPathEffect(new DashPathEffect(new float[]{30*s,20*s},(float)((System.nanoTime()/1000000)%5000)/80f));
    p.setColor(0xA0A7D8F6);c.drawOval(new RectF(zx-rx,zy-ry,zx+rx,zy+ry),p);
    p.setPathEffect(null);
    if(core.zoneRadius()<1550){
        p.setStrokeWidth(Math.max(2,5*s));
        p.setColor(0xD4F06B61);c.drawOval(new RectF(zx-rx*pulse,zy-ry*pulse,zx+rx*pulse,zy+ry*pulse),p);
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
            // Compact tactical map: roughly half the previous footprint, with a translucent HUD treatment.
            float size=Math.min(getWidth()*.19f,dp(170f));
            float mapH=size*.86f;
            float left=getWidth()-size-dp(10f),top=HUD+dp(8f);
            float radius=dp(12f);

            // Soft transparent shadow and glass frame.
            p.setStyle(Paint.Style.FILL);
            p.setColor(0x35000000);
            c.drawRoundRect(new RectF(left+dp(3f),top+dp(4f),left+size+dp(3f),top+mapH+dp(4f)),radius,radius,p);
            p.setColor(0x7A18211D);
            c.drawRoundRect(new RectF(left,top,left+size,top+mapH),radius,radius,p);

            // Translucent map surface so the battlefield remains visible underneath.
            p.setColor(0x6E25322C);
            c.drawRoundRect(new RectF(left+dp(5f),top+dp(5f),left+size-dp(5f),top+mapH-dp(5f)),radius*.72f,radius*.72f,p);

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
            p.setTextSize(dp(9f));
            c.drawText("TACTICAL MAP",left+dp(9f),top+dp(13f),p);
            p.setColor(0xFFDCE5DF);
            p.setTypeface(null);
            p.setTextSize(dp(9f));
            c.drawText("CITY GRID",left+dp(9f),top+dp(22f),p);
            p.setTextAlign(Paint.Align.RIGHT);
            p.setTypeface(PaintCompat.BOLD);
            p.setTextSize(dp(8f));
            c.drawText("N",left+size-dp(9f),top+dp(13f),p);
            c.drawText("ZONE",left+size-dp(9f),top+dp(22f),p);
            p.setTypeface(null);
            p.setTextAlign(Paint.Align.LEFT);
        }


        private void drawHud(Canvas c){
    float w=getWidth(),h=getHeight(),s=Math.max(.78f,Math.min(1.08f,h/720f));
    float t=(System.nanoTime()%8000000000L)/1e9f;
    p.setStyle(Paint.Style.FILL);
    p.setShader(new LinearGradient(0,10*s,0,(HUD-6)*s,0xF01B2522,0xC20D1514,Shader.TileMode.CLAMP));
    c.drawRoundRect(new RectF(10,8,w-10,HUD-6),16*s,16*s,p); p.setShader(null);
    p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(1.2f,1.5f*s));p.setColor(0x8AD1B96D);
    c.drawRoundRect(new RectF(10,8,w-10,HUD-6),16*s,16*s,p);p.setStyle(Paint.Style.FILL);

    GameCore.Player pl=core.player();
    p.setTypeface(PaintCompat.BOLD);p.setTextAlign(Paint.Align.LEFT);
    p.setTextSize(18*s);p.setColor(0xFFF2D47D);c.drawText("PERSIA WAR",28,34*s,p);
    p.setTypeface(null);p.setTextSize(11*s);p.setColor(0xFFBFC8C2);c.drawText(pl.skin.toUpperCase()+" • CITY OF KINGS",28,53*s,p);

    float barL=158*s,barW=Math.min(270*s,w*.24f);
    p.setColor(0x65101816);c.drawRoundRect(new RectF(barL,18*s,barL+barW,33*s),8*s,8*s,p);
    p.setShader(new LinearGradient(barL,18*s,barL+barW,18*s,0xFFE86A58,0xFF9D2E3A,Shader.TileMode.CLAMP));
    c.drawRoundRect(new RectF(barL,18*s,barL+barW*Math.max(0,pl.hp/(float)Math.max(1,pl.maxHp)),33*s),8*s,8*s,p);p.setShader(null);
    p.setColor(0xFFDDE5DE);p.setTextSize(10*s);c.drawText("HP  "+pl.hp+"/"+pl.maxHp,barL+8*s,29*s,p);

    p.setColor(0x65101816);c.drawRoundRect(new RectF(barL,39*s,barL+barW,53*s),8*s,8*s,p);
    p.setShader(new LinearGradient(barL,39*s,barL+barW,39*s,0xFF76C6E8,0xFF2E659B,Shader.TileMode.CLAMP));
    c.drawRoundRect(new RectF(barL,39*s,barL+barW*Math.min(1,Math.max(0,pl.shield/100f)),53*s),8*s,8*s,p);p.setShader(null);
    c.drawText("SHIELD  "+pl.shield,barL+8*s,50*s,p);

    p.setTextAlign(Paint.Align.CENTER);p.setTypeface(PaintCompat.BOLD);p.setTextSize(16*s);p.setColor(0xFFF4EFE3);
    c.drawText("KILLS  "+core.kills(),w*.50f,31*s,p);
    p.setTypeface(null);p.setTextSize(10*s);p.setColor(0xFFAEB7B0);c.drawText("TACTICAL SECTOR 07",w*.50f,50*s,p);

    p.setTextAlign(Paint.Align.RIGHT);p.setTypeface(PaintCompat.BOLD);p.setTextSize(14*s);p.setColor(0xFFE7CC78);
    c.drawText("AMMO  "+pl.ammo+"/"+pl.reserveAmmo+"   •   BOMB  "+pl.grenades,w-26,31*s,p);
    p.setTypeface(null);p.setTextSize(10*s);p.setColor(0xFFBDC6C0);
    c.drawText(core.zoneRadius()<1550?"ZONE CLOSING":"ZONE STABLE",w-26,50*s,p);

    // Animated tactical status light.
    p.setColor(0xFF72CDA7);c.drawCircle(w-18,18*s,3.2f*s,p);
    p.setColor(0x3072CDA7);c.drawCircle(w-18,18*s,(7+2*(float)Math.sin(t*3f))*s,p);
}

        private void drawControls(Canvas c){
    float w=getWidth(),h=getHeight(),jr=joystickRadius();

    float drawBaseX=joyActive?joyBaseX:idleJoyX();
    float drawBaseY=joyActive?joyBaseY:idleJoyY();

    p.setStyle(Paint.Style.FILL);
    p.setShader(new RadialGradient(drawBaseX,drawBaseY,jr+20,
            0x651A221F,0x10101816,Shader.TileMode.CLAMP));
    c.drawCircle(drawBaseX,drawBaseY,jr+17,p);p.setShader(null);
    p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2.5f,3*sc()));
    p.setColor(joyActive?0xD7D9C981:0x707C806E);c.drawCircle(drawBaseX,drawBaseY,jr+8,p);
    p.setStyle(Paint.Style.FILL);
    p.setColor(0x482F3A33);c.drawCircle(drawBaseX,drawBaseY,jr*.78f,p);
    p.setColor(joyActive?0xB9AD9A5D:0x6C66705B);
    c.drawCircle(joyActive?joyX:drawBaseX,joyActive?joyY:drawBaseY,jr*.38f,p);
    p.setColor(0xA5EFE8D4);c.drawCircle(joyActive?joyX-4*sc():drawBaseX-4*sc(),joyActive?joyY-4*sc():drawBaseY-4*sc(),jr*.12f,p);
    p.setTypeface(PaintCompat.BOLD);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(Math.max(dp(9f),jr*.14f));p.setColor(0xE8FFFFFF);
    c.drawText("MOVE",drawBaseX,drawBaseY+jr+23*sc(),p);

    button(c,fireX(),fireY(),fireVisualRadius(),"FIRE",firePointer>=0);
    button(c,swordX(),actionY(),actionVisualRadius(),"SWORD",swordPointer>=0);
    button(c,bombX(),actionY(),actionVisualRadius(),"BOMB",grenadePointer>=0);
    button(c,reloadX(),actionY(),actionVisualRadius(),"RELOAD",reloadPointer>=0);

    if(input.aimActive){
        p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(dp(2f),2.5f*sc()));
        p.setColor(0x7399D6E8);float rr=dp(27f),arm=dp(41f);
        c.drawCircle(aimTouchX,aimTouchY,rr,p);
        c.drawLine(aimTouchX-arm,aimTouchY,aimTouchX-rr,aimTouchY,p);
        c.drawLine(aimTouchX+rr,aimTouchY,aimTouchX+arm,aimTouchY,p);
        c.drawLine(aimTouchX,aimTouchY-arm,aimTouchX,aimTouchY-rr,p);
        c.drawLine(aimTouchX,aimTouchY+rr,aimTouchX,aimTouchY+arm,p);
        p.setStyle(Paint.Style.FILL);p.setColor(0xB6D7ECF1);c.drawCircle(aimTouchX,aimTouchY,3*sc(),p);
    }
}

        private void button(Canvas c,float x,float y,float r,String text,boolean pressed){
    float s=sc();
    p.setStyle(Paint.Style.FILL);
    p.setShader(new RadialGradient(x-r*.25f,y-r*.35f,r*1.25f,
            pressed?0xD97F8C68:0xB86E6960,
            pressed?0xB6263029:0x7A181E1B,Shader.TileMode.CLAMP));
    c.drawCircle(x,y,r,p);p.setShader(null);
    p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(dp(2.5f),3*sc()));
    p.setColor(pressed?0xF0F0D789:0xC9D1C083);c.drawCircle(x,y,r,p);
    p.setStrokeWidth(Math.max(1f,1.4f*s));p.setColor(0x55FFFFFF);c.drawCircle(x,y,r-dp(6f),p);

    p.setStyle(Paint.Style.FILL);p.setColor(0xFFEFE7D0);
    p.setTypeface(PaintCompat.BOLD);p.setTextAlign(Paint.Align.CENTER);
    float iconY=y-r*.18f;
    path.reset();
    if(text.equals("FIRE")){
        c.drawCircle(x,iconY,Math.max(4,s*5),p);
        p.setColor(0xFFFFC45D);c.drawCircle(x+2*s,iconY-2*s,Math.max(2,s*2.5f),p);
    }else if(text.equals("SWORD")){
        p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(3*s,3));p.setStrokeCap(Paint.Cap.ROUND);
        c.drawLine(x-7*s,iconY+7*s,x+7*s,iconY-7*s,p);c.drawLine(x-9*s,iconY-1*s,x+1*s,iconY+9*s,p);p.setStrokeCap(Paint.Cap.BUTT);
        p.setStyle(Paint.Style.FILL);
    }else if(text.equals("BOMB")){
        c.drawCircle(x,iconY,7*s,p);p.setColor(0xFF27372D);c.drawCircle(x,iconY,4*s,p);
        p.setColor(0xFFE9D17A);c.drawLine(x+4*s,iconY-5*s,x+8*s,iconY-9*s,p);
    }else{
        p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2.2f*s,2));p.setColor(0xFFEFE7D0);
        c.drawArc(new RectF(x-8*s,iconY-8*s,x+8*s,iconY+8*s),220,260,false,p);
        c.drawLine(x+8*s,iconY-2*s,x+4*s,iconY-8*s,p);p.setStyle(Paint.Style.FILL);
    }
    p.setColor(0xFFEFE7D0);p.setTextSize(Math.max(dp(9f),r*.20f));c.drawText(text,x,y+r*.50f,p);
    p.setTypeface(null);
}

        private void drawPause(Canvas c){
    p.setStyle(Paint.Style.FILL);
    p.setShader(new LinearGradient(0,0,getWidth(),getHeight(),0xB5000000,0xE90B1010,Shader.TileMode.CLAMP));
    c.drawRect(0,0,getWidth(),getHeight(),p);p.setShader(null);
    p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2.5f);p.setColor(0xBBD0B56A);
    c.drawRoundRect(new RectF(getWidth()*.28f,getHeight()*.34f,getWidth()*.72f,getHeight()*.64f),18,18,p);
    p.setStyle(Paint.Style.FILL);p.setTypeface(PaintCompat.BOLD);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(38);p.setColor(0xFFF1D47A);
    c.drawText("PAUSED",getWidth()*.5f,getHeight()*.46f,p);p.setTypeface(null);p.setTextSize(15);p.setColor(0xFFE9EAE4);
    c.drawText("TAP ANYWHERE TO RESUME",getWidth()*.5f,getHeight()*.54f,p);
}

        private void drawGameOver(Canvas c){
    p.setStyle(Paint.Style.FILL);
    p.setShader(new LinearGradient(0,0,0,getHeight(),0xC5000000,0xF1160C0C,Shader.TileMode.CLAMP));
    c.drawRect(0,0,getWidth(),getHeight(),p);p.setShader(null);
    p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xC9E08B64);
    c.drawRoundRect(new RectF(getWidth()*.24f,getHeight()*.30f,getWidth()*.76f,getHeight()*.70f),20,20,p);
    p.setStyle(Paint.Style.FILL);p.setTypeface(PaintCompat.BOLD);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(40);p.setColor(0xFFFF9C78);
    c.drawText("GAME OVER",getWidth()*.5f,getHeight()*.43f,p);
    p.setTypeface(null);p.setTextSize(18);p.setColor(0xFFF4EEE1);
    c.drawText("KILLS  "+core.kills()+"   •   SCORE  "+core.player().score,getWidth()*.5f,getHeight()*.51f,p);
    p.setTextSize(15);p.setColor(0xFFD9D8CF);c.drawText("TAP CENTER TO RESTART",getWidth()*.5f,getHeight()*.60f,p);
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
