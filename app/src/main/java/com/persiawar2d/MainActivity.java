package com.persiawar2d;

import android.app.Activity;
import android.content.Context;
import android.graphics.*;
import android.os.Bundle;
import android.view.*;
import java.util.*;

/** Core top-down Persia War gameplay view. */
public class MainActivity extends Activity {
    @Override public void onCreate(Bundle b){
        super.onCreate(b);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN,WindowManager.LayoutParams.FLAG_FULLSCREEN);
        setContentView(new GameView(this));
    }

    public static class GameView extends View {
        static final float HUD_H=92f;
        final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
        final Random random=new Random(7);
        final ArrayList<Enemy> enemies=new ArrayList<>();
        final ArrayList<Bullet> bullets=new ArrayList<>();
        float px,py,aimX,aimY,joyX,joyY;
        int wave=1,score=0,ammo=12,reserve=100,maxHp=100,hp=100,shield=0,weapon=0;
        boolean gameOver=false;
        long lastShot=0,lastSpawn=0;
        float worldW=2400,worldH=1400;
        public String playerSkin="classic";

        public GameView(Context c){super(c);p.setTypeface(Typeface.DEFAULT);resetGame();setFocusable(true);}
        void resetGame(){
            px=worldW*.5f; py=worldH*.55f; aimX=px+500; aimY=py;
            wave=1; score=0; ammo=12; reserve=100; hp=maxHp; shield=0; weapon=0; gameOver=false;
            enemies.clear();bullets.clear();spawnWave();invalidate();
        }
        void spawnWave(){int count=Math.min(5+wave*2,28);for(int i=0;i<count;i++){double a=random.nextDouble()*Math.PI*2;float d=500+random.nextFloat()*450;float ex=clamp(px+(float)Math.cos(a)*d,80,worldW-80);float ey=clamp(py+(float)Math.sin(a)*d,120,worldH-80);enemies.add(new Enemy(ex,ey,wave%5==0&&i==0?3:(i%4==0?2:1)));}lastSpawn=System.currentTimeMillis();}
        float clamp(float v,float a,float b){return Math.max(a,Math.min(b,v));}
        float distance(float ax,float ay,float bx,float by){return (float)Math.hypot(ax-bx,ay-by);}
        public void setGameJoystick(float x,float y){joyX=x;joyY=y;}
        public void moveByJoystick(float dx,float dy){float d=Math.max(1,(float)Math.hypot(dx,dy));float speed=5.5f;px=clamp(px+dx/d*speed,50,worldW-50);py=clamp(py+dy/d*speed,HUD_H+30,worldH-50);}
        public void reload(){if(gameOver||weapon!=0||ammo>=12||reserve<=0)return;int n=Math.min(12-ammo,reserve);ammo+=n;reserve-=n;}
        public void toggleWeapon(){if(gameOver)return;weapon=weapon==0?1:0;}
        public void melee(){if(gameOver)return;long n=System.currentTimeMillis();if(n-lastShot<360)return;lastShot=n;float dx=aimX-px,dy=aimY-py,d=Math.max(1,(float)Math.hypot(dx,dy));for(Enemy e:enemies){if(e.hp<=0)continue;float ex=e.x-px,ey=e.y-py,ed=Math.max(1,(float)Math.hypot(ex,ey));float dot=(ex*dx+ey*dy)/(ed*d);if(ed<125&&dot>.15f){e.hp-=38;if(e.hp<=0)score+=e.type==3?40:10;}}}
        public void shoot(){if(gameOver)return;if(weapon==1){melee();return;}long n=System.currentTimeMillis();if(n-lastShot<220||ammo<=0)return;lastShot=n;ammo--;float dx=aimX-px,dy=aimY-py,d=Math.max(1,(float)Math.hypot(dx,dy));bullets.add(new Bullet(px,py,dx/d*18,dy/d*18,true));}
        void enemyShoot(Enemy e){float dx=px-e.x,dy=py-e.y,d=Math.max(1,(float)Math.hypot(dx,dy));bullets.add(new Bullet(e.x,e.y,dx/d*7,dy/d*7,false));}
        void tick(){if(gameOver)return;long now=System.currentTimeMillis();if(now-lastSpawn>9000&&enemies.isEmpty()){wave++;spawnWave();}
            if(joyX!=0||joyY!=0)moveByJoystick(joyX-px,joyY-py);
            for(Enemy e:enemies){if(e.hp<=0)continue;float dx=px-e.x,dy=py-e.y,d=Math.max(1,(float)Math.hypot(dx,dy));float sp=e.type==3?1.7f:e.type==2?1.45f:1.15f;if(d>90){e.x+=dx/d*sp;e.y+=dy/d*sp;}if(d<115&&now-e.lastHit>700){hp-=e.type==3?12:6;e.lastHit=now;if(hp<=0){hp=0;gameOver=true;}}if(d<700&&now-e.lastShot>1500){enemyShoot(e);e.lastShot=now;}}
            for(Bullet b:bullets){b.x+=b.vx;b.y+=b.vy;b.life--;if(b.player){for(Enemy e:enemies){if(e.hp>0&&distance(b.x,b.y,e.x,e.y)<30){e.hp-=b.damage;b.life=0;if(e.hp<=0)score+=e.type==3?40:10;break;}}}else if(distance(b.x,b.y,px,py)<28){hp-=8;b.life=0;if(hp<=0){hp=0;gameOver=true;}}}
            bullets.removeIf(b->b.life<=0);enemies.removeIf(e->e.hp<=0);
        }
        @Override protected void onDraw(Canvas c){super.onDraw(c);tick();drawWorld(c);drawHud(c);postInvalidateOnAnimation();}
        void drawWorld(Canvas c){p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(30,55,44));c.drawRect(0,0,getWidth(),getHeight(),p);float sx=getWidth()/worldW,sy=(getHeight()-HUD_H)/worldH;float scale=Math.min(sx,sy);c.save();c.translate(getWidth()/2f-px*scale,(getHeight()+HUD_H)/2f-py*scale);p.setColor(Color.rgb(94,90,70));c.drawRect(0,0,worldW*scale,worldH*scale,p);p.setColor(Color.rgb(109,104,83));for(int x=0;x<worldW;x+=128)c.drawRect(x*scale,0,(x+2)*scale,worldH*scale,p);for(int y=0;y<worldH;y+=128)c.drawRect(0,y*scale,worldW*scale,(y+2)*scale,p);p.setColor(Color.rgb(210,180,105));for(int i=0;i<14;i++)c.drawCircle((180+i*170)*scale,(130+(i%4)*280)*scale,5, p);drawPlayer(c,scale);for(Enemy e:enemies)drawEnemy(c,e,scale);for(Bullet b:bullets)drawBullet(c,b,scale);c.restore();}
        void drawPlayer(Canvas c,float s){p.setStyle(Paint.Style.FILL);p.setColor(weapon==1?Color.rgb(210,170,75):Color.rgb(55,150,100));c.drawCircle(px*s,py*s,26*s,p);p.setColor(Color.WHITE);c.drawCircle(px*s,py*s,8*s,p);p.setColor(Color.rgb(20,35,30));c.drawLine(px*s,py*s,aimX*s,aimY*s,p);}
        void drawEnemy(Canvas c,Enemy e,float s){p.setStyle(Paint.Style.FILL);p.setColor(e.type==3?Color.rgb(220,105,35):e.type==2?Color.rgb(180,45,55):Color.rgb(125,50,95));c.drawCircle(e.x*s,e.y*s,(e.type==3?32:25)*s,p);p.setColor(Color.rgb(25,25,25));c.drawCircle(e.x*s,e.y*s,7*s,p);}
        void drawBullet(Canvas c,Bullet b,float s){p.setColor(b.player?Color.rgb(250,225,115):Color.rgb(255,90,70));c.drawCircle(b.x*s,b.y*s,5*s,p);}
        void drawHud(Canvas c){p.setStyle(Paint.Style.FILL);p.setColor(Color.argb(220,12,22,19));c.drawRect(0,0,getWidth(),HUD_H,p);p.setColor(Color.rgb(245,205,105));p.setTextSize(25);c.drawText("PERSIA WAR 2D",22,32,p);p.setTextSize(18);p.setColor(Color.WHITE);c.drawText("WAVE "+wave+"   SCORE "+score,22,62,p);c.drawText("HP "+hp+"   AMMO "+ammo+"/"+reserve+(weapon==1?"   SWORD":"   FIREARM"),getWidth()-370,34,p);if(gameOver){p.setColor(Color.argb(210,0,0,0));c.drawRect(0,0,getWidth(),getHeight(),p);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(48);p.setColor(Color.rgb(245,205,105));c.drawText("GAME OVER",getWidth()/2f,getHeight()/2f,p);p.setTextSize(20);p.setColor(Color.WHITE);c.drawText("Tap to restart",getWidth()/2f,getHeight()/2f+42,p);p.setTextAlign(Paint.Align.LEFT);}}
        @Override public boolean onTouchEvent(android.view.MotionEvent e){if(e.getAction()==MotionEvent.ACTION_UP&&gameOver){resetGame();return true;}if(e.getAction()==MotionEvent.ACTION_DOWN||e.getAction()==MotionEvent.ACTION_MOVE){float x=e.getX(),y=e.getY();aimX=px+(x-getWidth()/2f)*2;aimY=py+(y-(getHeight()+HUD_H)/2f)*2;if(y>getHeight()*.55f&&x>getWidth()*.68f)shoot();else if(y>getHeight()*.55f&&x<getWidth()*.35f){float dx=x-getWidth()*.18f,dy=y-(getHeight()*.72f);joyX=dx;joyY=dy;}return true;}if(e.getAction()==MotionEvent.ACTION_UP){joyX=joyY=0;}return true;}
        public static class Enemy{float x,y;int hp,type;long lastShot,lastHit;Enemy(float x,float y,int type){this.x=x;this.y=y;this.type=type;hp=type==3?120:type==2?70:45;}}
        static class Bullet{float x,y,vx,vy;int life=100,damage=25;boolean player;Bullet(float x,float y,float vx,float vy,boolean player){this.x=x;this.y=y;this.vx=vx;this.vy=vy;this.player=player;if(!player)damage=8;}}
    }
}
