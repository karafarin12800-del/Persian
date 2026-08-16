package com.persiawar2d;

import android.app.Activity;
import android.content.Context;
import android.graphics.*;
import android.graphics.drawable.Drawable;
import android.os.Bundle;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowManager;
import java.util.ArrayList;
import java.util.Random;

public class MainActivity extends Activity {
    @Override public void onCreate(Bundle b) {
        super.onCreate(b);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
        setContentView(new GameView(this));
    }

    public static class GameView extends View {
        static final float HUD_H = 92f;
        static final float WORLD_W = WorldRenderer.WORLD_SIZE;
        static final float WORLD_H = WorldRenderer.WORLD_SIZE;
        final Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        final Random random = new Random(20260816L);
        final ArrayList<Enemy> enemies = new ArrayList<>();
        final ArrayList<Bullet> bullets = new ArrayList<>();
        final WorldRenderer world;
        final Drawable classic, archer, guard, scout, enemyArt;
        float px, py, aimX, aimY;
        float joyBaseX, joyBaseY, joyX, joyY, moveNX, moveNY;
        boolean joystickDown, fireDown;
        int joystickPointer=-1, firePointer=-1;
        long joystickFadeAt, lastShot, lastMelee, lastSpawn, manualAimUntil;
        int wave=1, score=0, ammo=12, reserve=100, hp=100, shield=0, weapon=0;
        boolean gameOver=false;
        String playerSkin="archer";

        public GameView(Context c) {
            super(c);
            world=new WorldRenderer(c);
            classic=c.getDrawable(R.drawable.achaemenid_player);
            archer=c.getDrawable(R.drawable.player_archer);
            guard=c.getDrawable(R.drawable.player_guard);
            scout=c.getDrawable(R.drawable.player_scout);
            enemyArt=c.getDrawable(R.drawable.persia_enemy);
            setFocusable(true);
            setLayerType(View.LAYER_TYPE_HARDWARE,null);
            resetGame();
        }

        void resetGame() {
            px=WORLD_W*.50f; py=WORLD_H*.55f; aimX=px+700; aimY=py;
            joyBaseX=Math.max(120,getWidth()*.12f); joyBaseY=Math.max(HUD_H+140,getHeight()-150);
            joyX=joyBaseX; joyY=joyBaseY; moveNX=moveNY=0;
            joystickDown=false; joystickPointer=-1; fireDown=false; firePointer=-1;
            joystickFadeAt=System.currentTimeMillis(); manualAimUntil=0;
            wave=1; score=0; ammo=12; reserve=100; hp=100; shield=0; weapon=0; gameOver=false;
            enemies.clear(); bullets.clear(); spawnWave(); invalidate();
        }

        void spawnWave() {
            int n=Math.min(7+wave*2,20);
            for(int i=0;i<n;i++){
                double a=random.nextDouble()*Math.PI*2; float d=650+random.nextFloat()*550;
                float x=clamp(px+(float)Math.cos(a)*d,120,WORLD_W-120);
                float y=clamp(py+(float)Math.sin(a)*d,HUD_H+120,WORLD_H-120);
                int type=(i%7==0)?3:(i%3==0?2:1);
                enemies.add(new Enemy(x,y,type));
            }
            lastSpawn=System.currentTimeMillis();
        }

        @Override protected void onSizeChanged(int w,int h,int ow,int oh){
            joyBaseX=Math.max(120,w*.12f); joyBaseY=h-Math.max(145,h*.17f);
            if(!joystickDown){joyX=joyBaseX;joyY=joyBaseY;}
        }
        float cameraScale(){return getWidth()/1900f;}

        @Override protected void onDraw(Canvas c){
            tick(System.currentTimeMillis()); drawWorld(c); drawHud(c); drawControls(c);
            if(gameOver)drawGameOver(c); postInvalidateOnAnimation();
        }

        void tick(long now){
            if(gameOver)return;
            if(joystickDown)movePlayer(moveNX*7f,moveNY*7f);
            if(now>=manualAimUntil)autoAim();
            for(Enemy e:enemies)if(e.hp>0){
                float dx=px-e.x,dy=py-e.y,d=Math.max(1,(float)Math.hypot(dx,dy));
                float speed=e.type==3?1.65f:e.type==2?1.4f:1.15f;
                if(d>110){e.x+=dx/d*speed;e.y+=dy/d*speed;}
                if(d<120&&now-e.lastHit>700){damagePlayer(e.type==3?12:6);e.lastHit=now;}
                if(d<900&&now-e.lastShot>(e.type==3?1100:1500)){enemyShoot(e);e.lastShot=now;}
            }
            updateBullets(); enemies.removeIf(e->e.hp<=0);
            if(enemies.isEmpty()&&now-lastSpawn>800){wave++;spawnWave();}
        }

        void updateBullets(){
            for(int i=bullets.size()-1;i>=0;i--){
                Bullet b=bullets.get(i); b.x+=b.vx; b.y+=b.vy; b.life--;
                if(b.x<0||b.y<0||b.x>WORLD_W||b.y>WORLD_H||b.life<=0){bullets.remove(i);continue;}
                if(b.player){
                    boolean hit=false;
                    for(Enemy e:enemies)if(e.hp>0&&distance(b.x,b.y,e.x,e.y)<34){e.hp-=b.damage;if(e.hp<=0)score+=e.type==3?40:10;hit=true;break;}
                    if(hit)bullets.remove(i);
                }else if(distance(b.x,b.y,px,py)<30){damagePlayer(b.damage);bullets.remove(i);}
            }
        }

        void autoAim(){Enemy best=null;float bestD=1450;for(Enemy e:enemies)if(e.hp>0){float d=distance(px,py,e.x,e.y);if(d<bestD){bestD=d;best=e;}}if(best!=null){aimX=best.x;aimY=best.y;}}
        void enemyShoot(Enemy e){float dx=px-e.x,dy=py-e.y,d=Math.max(1,(float)Math.hypot(dx,dy));bullets.add(new Bullet(e.x,e.y,dx/d*10f,dy/d*10f,8,false));}
        void shoot(){
            if(gameOver||weapon!=0)return; long now=System.currentTimeMillis(); if(now-lastShot<130)return;
            if(ammo<=0){reload();return;} if(now>=manualAimUntil)autoAim();
            float dx=aimX-px,dy=aimY-py,d=Math.max(1,(float)Math.hypot(dx,dy));
            bullets.add(new Bullet(px+dx/d*44,py+dy/d*44,dx/d*20f,dy/d*20f,28,true)); ammo--; lastShot=now;
        }
        void melee(){
            if(gameOver||weapon!=1)return; long now=System.currentTimeMillis(); if(now-lastMelee<320)return; lastMelee=now;
            float dx=aimX-px,dy=aimY-py,d=Math.max(1,(float)Math.hypot(dx,dy));
            for(Enemy e:enemies)if(e.hp>0){float ex=e.x-px,ey=e.y-py,ed=Math.max(1,(float)Math.hypot(ex,ey));float dot=(ex*dx+ey*dy)/(ed*d);if(ed<145&&dot>.05f){e.hp-=45;if(e.hp<=0)score+=e.type==3?40:10;}}
        }
        void reload(){if(gameOver||weapon!=0||ammo>=12||reserve<=0)return;int n=Math.min(12-ammo,reserve);ammo+=n;reserve-=n;}
        void toggleWeapon(){if(!gameOver)weapon=weapon==0?1:0;}
        void setAimFromScreen(float x,float y){float s=cameraScale();float ox=getWidth()/2f-px*s;float oy=(getHeight()+HUD_H)/2f-py*s;aimX=(x-ox)/s;aimY=(y-oy)/s;manualAimUntil=System.currentTimeMillis()+1200;}
        void movePlayer(float dx,float dy){px=clamp(px+dx,70,WORLD_W-70);py=clamp(py+dy,HUD_H+70,WORLD_H-70);}
        void damagePlayer(int amount){int blocked=Math.min(shield,amount);shield-=blocked;amount-=blocked;if(amount>0){hp-=amount;if(hp<=0){hp=0;gameOver=true;}}}

        void drawWorld(Canvas c){
            world.draw(c,px,py,cameraScale(),getWidth(),getHeight(),HUD_H);
            float s=cameraScale(),ox=getWidth()/2f-px*s,oy=(getHeight()+HUD_H)/2f-py*s;
            c.save();c.translate(ox,oy);for(Bullet b:bullets)drawBullet(c,b,s);for(Enemy e:enemies)drawEnemy(c,e,s);drawPlayer(c,s);c.restore();
        }
        Drawable player(){if("guard".equals(playerSkin))return guard;if("scout".equals(playerSkin))return scout;if("classic".equals(playerSkin))return classic;return archer;}
        void drawDrawable(Canvas c,Drawable d,float x,float y,float size){if(d==null)return;int h=Math.max(1,Math.round(size*.5f));d.setAlpha(255);d.setBounds(Math.round(x-h),Math.round(y-h),Math.round(x+h),Math.round(y+h));d.draw(c);}
        void drawPlayer(Canvas c,float s){
            p.setStyle(Paint.Style.FILL);p.setColor(0x55000000);c.drawOval(px*s-34*s,py*s+34*s,px*s+34*s,py*s+48*s,p);
            drawDrawable(c,player(),px*s,py*s,Math.max(82,120*s));
            // Removed the old player-to-enemy/aim line. Aim is invisible and only controls shots.
            if(shield>0){p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,3*s));p.setColor(0xAA52DFFF);c.drawCircle(px*s,py*s,62*s,p);p.setStyle(Paint.Style.FILL);}
        }
        void drawEnemy(Canvas c,Enemy e,float s){drawDrawable(c,enemyArt,e.x*s,e.y*s,(e.type==3?136:e.type==2?118:104)*s);float bw=58*s,bh=Math.max(5,6*s),left=e.x*s-bw*.5f,top=(e.y-68)*s;p.setColor(0xB0141414);c.drawRect(left,top,left+bw,top+bh,p);p.setColor(Color.rgb(196,55,45));float max=e.type==3?120:e.type==2?70:45;c.drawRect(left,top,left+bw*Math.max(0,e.hp/max),top+bh,p);}
        void drawBullet(Canvas c,Bullet b,float s){float x=b.x*s,y=b.y*s,speed=(float)Math.hypot(b.vx,b.vy),len=Math.min(30,Math.max(10,speed*.7f)),d=Math.max(1,speed);float tx=x-b.vx/d*len,ty=y-b.vy/d*len;p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(3,5*s));p.setStrokeCap(Paint.Cap.ROUND);p.setColor(b.player?Color.rgb(255,220,95):Color.rgb(255,85,70));c.drawLine(tx,ty,x,y,p);p.setStyle(Paint.Style.FILL);c.drawCircle(x,y,Math.max(3,5*s),p);}

        void drawHud(Canvas c){
            p.setStyle(Paint.Style.FILL);p.setColor(Color.argb(225,12,22,19));c.drawRect(0,0,getWidth(),HUD_H,p);
            p.setTextAlign(Paint.Align.LEFT);p.setTextSize(24);p.setColor(Color.rgb(245,205,105));c.drawText("PERSIA WAR 2.5D",22,32,p);p.setTextSize(17);p.setColor(Color.WHITE);c.drawText("WAVE "+wave+"   SCORE "+score,22,62,p);
            p.setTextAlign(Paint.Align.RIGHT);c.drawText("HP "+hp+"   AMMO "+ammo+"/"+reserve+(weapon==1?"   SWORD":"   FIREARM"),getWidth()-22,34,p);p.setTextAlign(Paint.Align.LEFT);
        }
        void drawControls(Canvas c){
            long age=System.currentTimeMillis()-joystickFadeAt;int alpha=joystickDown?220:(int)Math.max(24,150-age/4);
            p.setStyle(Paint.Style.FILL);p.setColor((alpha<<24)|0x6D786F);c.drawCircle(joyBaseX,joyBaseY,82,p);p.setColor(((Math.min(255,alpha+30))<<24)|0xE0C875);c.drawCircle(joyX,joyY,34,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(((Math.min(255,alpha+35))<<24)|0xF0D990);c.drawCircle(joyBaseX,joyBaseY,82,p);p.setStyle(Paint.Style.FILL);
            float br=Math.max(72,Math.min(105,getHeight()*.11f));float fireX=getWidth()*.84f,fireY=getHeight()*.78f,reloadX=getWidth()*.70f,reloadY=getHeight()*.89f,switchX=getWidth()*.90f,switchY=getHeight()*.89f,swordX=getWidth()*.72f,swordY=getHeight()*.61f;
            circleButton(c,fireX,fireY,br*1.08f,0xD09A4638);circleButton(c,reloadX,reloadY,br*.55f,0xB0446F5B);circleButton(c,switchX,switchY,br*.55f,0xB0446F5B);circleButton(c,swordX,swordY,br*.62f,0xB05D5140);
            textCenter(c,weapon==0?"FIRE":"SWING",fireX,fireY+8,Color.WHITE,22);textCenter(c,"R",reloadX,reloadY+7,Color.WHITE,18);textCenter(c,"SW",switchX,switchY+6,Color.WHITE,15);textCenter(c,"⚔",swordX,swordY+9,Color.WHITE,25);
        }
        void circleButton(Canvas c,float x,float y,float r,int color){p.setStyle(Paint.Style.FILL);p.setColor(color);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xCFE8CF88);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.FILL);}
        void textCenter(Canvas c,String s,float x,float y,int color,float size){p.setStyle(Paint.Style.FILL);p.setColor(color);p.setTextSize(size);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.CENTER);c.drawText(s,x,y,p);}
        void drawGameOver(Canvas c){p.setColor(0xDD000000);c.drawRect(0,0,getWidth(),getHeight(),p);textCenter(c,"GAME OVER",getWidth()/2f,getHeight()/2f-20,Color.WHITE,52);textCenter(c,"TAP TO RESTART",getWidth()/2f,getHeight()/2f+32,Color.rgb(238,210,150),22);}

        @Override public boolean onTouchEvent(MotionEvent e){
            int action=e.getActionMasked();
            if(gameOver){if(action==MotionEvent.ACTION_DOWN)resetGame();return true;}
            if(action==MotionEvent.ACTION_DOWN||action==MotionEvent.ACTION_POINTER_DOWN){
                int idx=e.getActionIndex(),id=e.getPointerId(idx);float x=e.getX(idx),y=e.getY(idx);float br=Math.max(72,Math.min(105,getHeight()*.11f));
                float fireX=getWidth()*.84f,fireY=getHeight()*.78f,reloadX=getWidth()*.70f,reloadY=getHeight()*.89f,switchX=getWidth()*.90f,switchY=getHeight()*.89f,swordX=getWidth()*.72f,swordY=getHeight()*.61f;
                if(near(x,y,fireX,fireY,br*1.25f)){firePointer=id;fireDown=true;if(weapon==0)shoot();else melee();return true;}
                if(near(x,y,reloadX,reloadY,br*.75f)){reload();return true;}
                if(near(x,y,switchX,switchY,br*.75f)){toggleWeapon();return true;}
                if(near(x,y,swordX,swordY,br*.82f)){melee();return true;}
                if(x<getWidth()*.48f&&y>HUD_H){joystickPointer=id;joystickDown=true;joystickFadeAt=System.currentTimeMillis();joyBaseX=clamp(x,82,getWidth()*.44f);joyBaseY=clamp(y,HUD_H+85,getHeight()-85);joyX=joyBaseX;joyY=joyBaseY;moveNX=moveNY=0;return true;}
                if(y>HUD_H){setAimFromScreen(x,y);return true;}
            }
            if(action==MotionEvent.ACTION_MOVE){
                for(int i=0;i<e.getPointerCount();i++){int id=e.getPointerId(i);float x=e.getX(i),y=e.getY(i);if(id==joystickPointer){float dx=x-joyBaseX,dy=y-joyBaseY,d=Math.max(1,(float)Math.hypot(dx,dy)),max=58;if(d>max){dx=dx/d*max;dy=dy/d*max;}joyX=joyBaseX+dx;joyY=joyBaseY+dy;moveNX=dx/max;moveNY=dy/max;}else if(id==firePointer&&fireDown){if(weapon==0)shoot();else melee();}}return true;
            }
            if(action==MotionEvent.ACTION_UP||action==MotionEvent.ACTION_POINTER_UP||action==MotionEvent.ACTION_CANCEL){int idx=e.getActionIndex(),id=e.getPointerId(idx);if(id==joystickPointer){joystickPointer=-1;joystickDown=false;joystickFadeAt=System.currentTimeMillis();joyX=joyBaseX;joyY=joyBaseY;moveNX=moveNY=0;}if(id==firePointer){firePointer=-1;fireDown=false;}return true;}
            return true;
        }
        boolean near(float x,float y,float cx,float cy,float r){return Math.hypot(x-cx,y-cy)<=r;}
        float clamp(float v,float a,float b){return Math.max(a,Math.min(b,v));}
        float distance(float a,float b,float c,float d){return (float)Math.hypot(a-c,b-d);}
        static class Enemy{float x,y;int hp,type;long lastShot,lastHit;Enemy(float x,float y,int type){this.x=x;this.y=y;this.type=type;hp=type==3?120:type==2?70:45;}}
        static class Bullet{float x,y,vx,vy;int damage,life=100;boolean player;Bullet(float x,float y,float vx,float vy,int damage,boolean player){this.x=x;this.y=y;this.vx=vx;this.vy=vy;this.damage=damage;this.player=player;}}
    }
}
