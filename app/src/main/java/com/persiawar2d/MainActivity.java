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
    @Override public void onCreate(Bundle b){
        super.onCreate(b);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN,WindowManager.LayoutParams.FLAG_FULLSCREEN);
        setContentView(new GameView(this));
    }

    public static class GameView extends View {
        static final float HUD_H=92f,WORLD_SIZE=WorldRenderer.WORLD_SIZE;
        final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
        final Random random=new Random(20260816L);
        final ArrayList<Enemy> enemies=new ArrayList<>();
        final ArrayList<Bullet> bullets=new ArrayList<>();
        final WorldRenderer world;
        final KingSpriteDrawable king;
        final Drawable enemyArt;
        float px,py,aimX,aimY,joyBaseX,joyBaseY,joyX,joyY,moveNX,moveNY;
        boolean joystickDown,fireDown;
        int joystickPointer=-1,firePointer=-1;
        long joystickFadeAt,lastShot,lastMelee,lastFrameAt;
        int wave=1,score=0,ammo=12,reserve=100,hp=100,shield=0,weapon=0,playerDir=0,playerFrame=0;
        boolean gameOver=false;

        public GameView(Context c){
            super(c); world=new WorldRenderer(c); king=new KingSpriteDrawable(c);
            enemyArt=c.getDrawable(R.drawable.persia_enemy);
            setFocusable(true);setLayerType(View.LAYER_TYPE_HARDWARE,null);resetGame();
        }

        void resetGame(){
            px=4800;py=5350;aimX=px+900;aimY=py;moveNX=moveNY=0;
            joystickDown=false;joystickPointer=-1;fireDown=false;firePointer=-1;
            joystickFadeAt=System.currentTimeMillis();lastFrameAt=System.currentTimeMillis();
            wave=1;score=0;ammo=12;reserve=100;hp=100;shield=0;weapon=0;gameOver=false;
            playerDir=0;playerFrame=0;king.setState(0,0);enemies.clear();bullets.clear();spawnWave();invalidate();
        }

        void spawnWave(){
            int n=Math.min(7+wave,14);
            for(int i=0;i<n;i++){
                double a=random.nextDouble()*Math.PI*2;float d=850+random.nextFloat()*850;
                float x=clamp(px+(float)Math.cos(a)*d,180,WORLD_SIZE-180);
                float y=clamp(py+(float)Math.sin(a)*d,180,WORLD_SIZE-180);
                int type=(i%7==0)?3:(i%3==0?2:1);enemies.add(new Enemy(x,y,type));
            }
        }

        @Override protected void onSizeChanged(int w,int h,int ow,int oh){
            if(!joystickDown){joyBaseX=w*.15f;joyBaseY=h*.79f;joyX=joyBaseX;joyY=joyBaseY;}
        }
        float cameraScale(){return Math.min(getWidth()/1900f,Math.max(.72f,(getHeight()-HUD_H)/1000f));}

        @Override protected void onDraw(Canvas c){
            long now=System.currentTimeMillis();float dt=Math.min(.033f,Math.max(.001f,(now-lastFrameAt)/1000f));lastFrameAt=now;
            tick(now,dt);drawWorld(c);drawHud(c);drawControls(c);if(gameOver)drawGameOver(c);postInvalidateOnAnimation();
        }

        void tick(long now,float dt){
            if(gameOver)return;
            if(joystickDown&&Math.hypot(moveNX,moveNY)>.05f){movePlayer(moveNX*360f*dt,moveNY*360f*dt);animatePlayer(now);}else if(!joystickDown){playerFrame=0;king.setState(playerDir,0);}
            if(fireDown)shoot();
            if(now-lastShot>850)autoAim();
            for(Enemy e:enemies)if(e.hp>0){
                float dx=px-e.x,dy=py-e.y,d=Math.max(1,(float)Math.hypot(dx,dy));
                float speed=e.type==3?105f:e.type==2?88f:72f;
                if(d>110){e.x+=dx/d*speed*dt;e.y+=dy/d*speed*dt;}
                if(d<120&&now-e.lastHit>700){damagePlayer(e.type==3?12:6);e.lastHit=now;}
                if(d<1050&&now-e.lastShot>(e.type==3?1200:1700)){enemyShoot(e);e.lastShot=now;}
            }
            updateBullets(dt);enemies.removeIf(e->e.hp<=0);
            if(enemies.isEmpty()&&now-lastShot>700){wave++;spawnWave();}
        }

        void animatePlayer(long now){playerFrame=(int)((now/95)%6);king.setState(playerDir,playerFrame);}
        void updateDirection(float nx,float ny){
            if(Math.hypot(nx,ny)<.08)return;
            if(Math.abs(nx)>Math.abs(ny))playerDir=nx<0?1:2;else playerDir=ny<0?3:0;
            king.setState(playerDir,playerFrame);
        }

        void updateBullets(float dt){
            for(int i=bullets.size()-1;i>=0;i--){
                Bullet b=bullets.get(i);b.x+=b.vx*dt;b.y+=b.vy*dt;b.life-=dt;
                if(b.x<0||b.y<0||b.x>WORLD_SIZE||b.y>WORLD_SIZE||b.life<=0){bullets.remove(i);continue;}
                if(b.player){boolean hit=false;for(Enemy e:enemies)if(e.hp>0&&distance(b.x,b.y,e.x,e.y)<42){e.hp-=b.damage;if(e.hp<=0)score+=e.type==3?40:10;hit=true;break;}if(hit)bullets.remove(i);}
                else if(distance(b.x,b.y,px,py)<36){damagePlayer(Math.round(b.damage));bullets.remove(i);}
            }
        }

        void autoAim(){Enemy best=null;float bestD=1500;for(Enemy e:enemies)if(e.hp>0){float d=distance(px,py,e.x,e.y);if(d<bestD){bestD=d;best=e;}}if(best!=null){aimX=best.x;aimY=best.y;}}
        void enemyShoot(Enemy e){float dx=px-e.x,dy=py-e.y,d=Math.max(1,(float)Math.hypot(dx,dy));bullets.add(new Bullet(e.x+dx/d*42,e.y+dy/d*42,dx/d*680,dy/d*680,8,false,.85f));}

        void shoot(){
            if(gameOver)return;long now=System.currentTimeMillis();if(now-lastShot<150)return;
            if(ammo<=0){reload();return;}if(now-lastShot>850)autoAim();
            float dx=aimX-px,dy=aimY-py,d=Math.max(1,(float)Math.hypot(dx,dy));
            bullets.add(new Bullet(px+dx/d*60,py+dy/d*60,dx/d*1250,dy/d*1250,30,true,.75f));ammo--;lastShot=now;
        }

        void punch(){meleeAttack(25,125,180);}
        void sword(){meleeAttack(45,165,320);}
        void meleeAttack(float damage,float range,long cooldown){
            if(gameOver)return;long now=System.currentTimeMillis();if(now-lastMelee<cooldown)return;lastMelee=now;
            float dx=aimX-px,dy=aimY-py,d=Math.max(1,(float)Math.hypot(dx,dy));
            for(Enemy e:enemies)if(e.hp>0){float ex=e.x-px,ey=e.y-py,ed=Math.max(1,(float)Math.hypot(ex,ey));float dot=(ex*dx+ey*dy)/(ed*d);if(ed<range&&dot>.05f){e.hp-=damage;if(e.hp<=0)score+=e.type==3?40:10;}}
        }

        void reload(){if(gameOver||ammo>=12||reserve<=0)return;int n=Math.min(12-ammo,reserve);ammo+=n;reserve-=n;}
        void toggleWeapon(){if(!gameOver)weapon=weapon==0?1:0;}
        void setAimFromScreen(float x,float y){float s=cameraScale(),ox=getWidth()/2f-px*s,oy=HUD_H+(getHeight()-HUD_H)/2f-py*s;aimX=(x-ox)/s;aimY=(y-oy)/s;}
        void movePlayer(float dx,float dy){px=clamp(px+dx,70,WORLD_SIZE-70);py=clamp(py+dy,HUD_H+70,WORLD_SIZE-70);updateDirection(moveNX,moveNY);}
        void damagePlayer(int amount){int blocked=Math.min(shield,amount);shield-=blocked;amount-=blocked;if(amount>0){hp-=amount;if(hp<=0){hp=0;gameOver=true;}}}

        void drawWorld(Canvas c){
            float s=cameraScale();world.draw(c,px,py,s,getWidth(),getHeight(),HUD_H);
            float ox=getWidth()/2f-px*s,oy=HUD_H+(getHeight()-HUD_H)/2f-py*s;c.save();c.translate(ox,oy);
            for(Bullet b:bullets)drawBullet(c,b,s);for(Enemy e:enemies)drawEnemy(c,e,s);drawPlayer(c,s);c.restore();
        }

        void drawPlayer(Canvas c,float s){
            float x=px*s,y=py*s;p.setStyle(Paint.Style.FILL);p.setColor(0x55000000);c.drawOval(x-42*s,y+42*s,x+42*s,y+58*s,p);
            king.setState(playerDir,playerFrame);king.setAlpha(255);int size=Math.max(138,Math.round(190*s)),h=size/2;
            king.setBounds(Math.round(x-h),Math.round(y-h),Math.round(x+h),Math.round(y+h));king.draw(c);
            if(shield>0){p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,3*s));p.setColor(0xAA52DFFF);c.drawCircle(x,y,66*s,p);}
        }

        void drawEnemy(Canvas c,Enemy e,float s){
            if(enemyArt==null)return;float x=e.x*s,y=e.y*s;int size=Math.round((e.type==3?142:e.type==2?126:112)*s),h=size/2;
            enemyArt.setAlpha(255);enemyArt.setBounds(Math.round(x-h),Math.round(y-h),Math.round(x+h),Math.round(y+h));enemyArt.draw(c);
            float bw=62*s,bh=Math.max(5,6*s),left=x-bw*.5f,top=(e.y-78)*s;p.setStyle(Paint.Style.FILL);p.setColor(0xB0141414);c.drawRect(left,top,left+bw,top+bh,p);p.setColor(Color.rgb(196,55,45));float max=e.type==3?120:e.type==2?70:45;c.drawRect(left,top,left+bw*Math.max(0,e.hp/max),top+bh,p);
        }
        void drawBullet(Canvas c,Bullet b,float s){float x=b.x*s,y=b.y*s;p.setStyle(Paint.Style.FILL);p.setColor(b.player?Color.rgb(255,220,95):Color.rgb(255,85,70));c.drawCircle(x,y,Math.max(4,6*s),p);}

        void drawHud(Canvas c){
            p.setStyle(Paint.Style.FILL);p.setColor(Color.argb(225,12,22,19));c.drawRect(0,0,getWidth(),HUD_H,p);
            p.setTextAlign(Paint.Align.LEFT);p.setTextSize(24);p.setColor(Color.rgb(245,205,105));c.drawText("PERSIA WAR 2.5D",22,32,p);
            p.setTextSize(17);p.setColor(Color.WHITE);c.drawText("WAVE "+wave+"   SCORE "+score,22,62,p);
            p.setTextAlign(Paint.Align.RIGHT);c.drawText("HP "+hp+"   AMMO "+ammo+"/"+reserve+(weapon==1?"   SWORD":"   FIREARM"),getWidth()-22,34,p);p.setTextAlign(Paint.Align.LEFT);
        }

        /** Controller layout: attack buttons form a compact right-side cluster and never sit on the joystick. */
        void drawControls(Canvas c){
            long age=System.currentTimeMillis()-joystickFadeAt;int alpha=joystickDown?235:(int)Math.max(18,235-age/3);
            p.setStyle(Paint.Style.FILL);p.setColor((alpha<<24)|0x59615B);c.drawCircle(joyBaseX,joyBaseY,94,p);
            p.setColor(((Math.min(255,alpha+20))<<24)|0xE0C875);c.drawCircle(joyX,joyY,38,p);
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(((Math.min(255,alpha+25))<<24)|0xF0D990);c.drawCircle(joyBaseX,joyBaseY,94,p);
            p.setStyle(Paint.Style.FILL);

            float attackR=Math.max(72,Math.min(98,getHeight()*.105f));
            float fireX=getWidth()*.84f,fireY=getHeight()*.75f;
            float swordX=getWidth()*.67f,swordY=getHeight()*.62f;
            float punchX=getWidth()*.67f,punchY=getHeight()*.79f;
            float reloadX=getWidth()*.80f,reloadY=getHeight()*.91f;
            float switchX=getWidth()*.93f,switchY=getHeight()*.91f;

            circleButton(c,fireX,fireY,attackR*1.12f,0xD09A4638);
            circleButton(c,swordX,swordY,attackR*.88f,0xB05D5140);
            circleButton(c,punchX,punchY,attackR*.88f,0xB05D5140);
            circleButton(c,reloadX,reloadY,attackR*.52f,0xB0446F5B);
            circleButton(c,switchX,switchY,attackR*.52f,0xB0446F5B);
            textCenter(c,"FIRE",fireX,fireY+8,Color.WHITE,21);
            textCenter(c,"⚔",swordX,swordY+9,Color.WHITE,27);
            textCenter(c,"PUNCH",punchX,punchY+6,Color.WHITE,14);
            textCenter(c,"R",reloadX,reloadY+6,Color.WHITE,18);
            textCenter(c,"SW",switchX,switchY+6,Color.WHITE,14);
        }

        void circleButton(Canvas c,float x,float y,float r,int color){p.setStyle(Paint.Style.FILL);p.setColor(color);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xCFE8CF88);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.FILL);}
        void textCenter(Canvas c,String s,float x,float y,int color,float size){p.setStyle(Paint.Style.FILL);p.setColor(color);p.setTextSize(size);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.CENTER);c.drawText(s,x,y,p);}
        void drawGameOver(Canvas c){p.setColor(0xDD000000);c.drawRect(0,0,getWidth(),getHeight(),p);textCenter(c,"GAME OVER",getWidth()/2f,getHeight()/2f-20,Color.WHITE,52);textCenter(c,"TAP TO RESTART",getWidth()/2f,getHeight()/2f+32,Color.rgb(238,210,150),22);}

        @Override public boolean onTouchEvent(MotionEvent e){
            int action=e.getActionMasked();
            if(gameOver){if(action==MotionEvent.ACTION_DOWN)resetGame();return true;}
            if(action==MotionEvent.ACTION_DOWN||action==MotionEvent.ACTION_POINTER_DOWN){
                int idx=e.getActionIndex(),id=e.getPointerId(idx);float x=e.getX(idx),y=e.getY(idx);
                float r=Math.max(72,Math.min(98,getHeight()*.105f));
                float fireX=getWidth()*.84f,fireY=getHeight()*.75f;
                float swordX=getWidth()*.67f,swordY=getHeight()*.62f;
                float punchX=getWidth()*.67f,punchY=getHeight()*.79f;
                float reloadX=getWidth()*.80f,reloadY=getHeight()*.91f;
                float switchX=getWidth()*.93f,switchY=getHeight()*.91f;
                if(near(x,y,fireX,fireY,r*1.35f)){firePointer=id;fireDown=true;setAimFromScreen(x,y);shoot();return true;}
                if(near(x,y,swordX,swordY,r)){sword();return true;}
                if(near(x,y,punchX,punchY,r)){punch();return true;}
                if(near(x,y,reloadX,reloadY,r*.70f)){reload();return true;}
                if(near(x,y,switchX,switchY,r*.70f)){toggleWeapon();return true;}
                if(x<getWidth()*.52f&&y>HUD_H){
                    joystickPointer=id;joystickDown=true;joystickFadeAt=System.currentTimeMillis();
                    joyBaseX=clamp(x,94,getWidth()*.46f);joyBaseY=clamp(y,HUD_H+94,getHeight()-94);joyX=joyBaseX;joyY=joyBaseY;moveNX=moveNY=0;return true;
                }
                if(y>HUD_H){setAimFromScreen(x,y);return true;}
            }
            if(action==MotionEvent.ACTION_MOVE){
                for(int i=0;i<e.getPointerCount();i++){
                    int id=e.getPointerId(i);float x=e.getX(i),y=e.getY(i);
                    if(id==joystickPointer){
                        float dx=x-joyBaseX,dy=y-joyBaseY,d=(float)Math.hypot(dx,dy),max=94;
                        if(d>max){dx=dx/d*max;dy=dy/d*max;}joyX=joyBaseX+dx;joyY=joyBaseY+dy;moveNX=dx/max;moveNY=dy/max;updateDirection(moveNX,moveNY);
                    } else if(id==firePointer){setAimFromScreen(x,y);}
                }
                return true;
            }
            if(action==MotionEvent.ACTION_UP||action==MotionEvent.ACTION_POINTER_UP||action==MotionEvent.ACTION_CANCEL){
                int idx=e.getActionIndex(),id=e.getPointerId(idx);
                if(id==joystickPointer){joystickPointer=-1;joystickDown=false;moveNX=moveNY=0;joyX=joyBaseX;joystickFadeAt=System.currentTimeMillis();}
                if(id==firePointer){firePointer=-1;fireDown=false;}
                return true;
            }
            return true;
        }

        static boolean near(float x,float y,float tx,float ty,float r){return Math.hypot(x-tx,y-ty)<=r;}
        static float distance(float ax,float ay,float bx,float by){return (float)Math.hypot(ax-bx,ay-by);}
        static float clamp(float v,float a,float b){return Math.max(a,Math.min(b,v));}
        static class Enemy{float x,y,hp;final int type;long lastHit,lastShot;Enemy(float x,float y,int type){this.x=x;this.y=y;this.type=type;hp=type==3?120:type==2?70:45;}}
        static class Bullet{float x,y,vx,vy,damage,life;boolean player;Bullet(float x,float y,float vx,float vy,float damage,boolean player,float life){this.x=x;this.y=y;this.vx=vx;this.vy=vy;this.damage=damage;this.player=player;this.life=life;}}
    }
}
