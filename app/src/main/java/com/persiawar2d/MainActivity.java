package com.persiawar2d;

import android.app.Activity;
import android.content.Context;
import android.graphics.*;
import android.graphics.drawable.Drawable;
import android.os.Bundle;
import android.view.*;
import java.util.*;

/** Persia War 2.5D gameplay. The large square world scrolls under a player-centered camera. */
public class MainActivity extends Activity {
 @Override public void onCreate(Bundle b){super.onCreate(b);getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN,WindowManager.LayoutParams.FLAG_FULLSCREEN);setContentView(new GameView(this));}
 public static class GameView extends View {
  static final float HUD_H=92f,VIEW_WORLD_WIDTH=1800f;
  final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG); final Random random=new Random(7);
  final ArrayList<Enemy> enemies=new ArrayList<>(); final ArrayList<Bullet> bullets=new ArrayList<>(); final WorldRenderer world;
  final Drawable classic,archer,guard,scout,enemyArt;
  float px,py,aimX,aimY,joyX,joyY; int wave=1,score=0,ammo=12,reserve=100,maxHp=100,hp=100,shield=0,weapon=0;
  boolean gameOver=false,joyActive=false; long lastShot=0,lastSpawn=0; final float worldW=WorldRenderer.WORLD_SIZE,worldH=WorldRenderer.WORLD_SIZE;
  public String playerSkin="archer";
  public GameView(Context c){super(c);world=new WorldRenderer(c);classic=c.getDrawable(R.drawable.achaemenid_player);archer=c.getDrawable(R.drawable.player_archer);guard=c.getDrawable(R.drawable.player_guard);scout=c.getDrawable(R.drawable.player_scout);enemyArt=c.getDrawable(R.drawable.persia_enemy);resetGame();setFocusable(true);setLayerType(View.LAYER_TYPE_HARDWARE,null);}
  void resetGame(){px=worldW*.5f;py=worldH*.55f;aimX=px+500;aimY=py;wave=1;score=0;ammo=12;reserve=100;hp=maxHp;shield=0;weapon=0;gameOver=false;enemies.clear();bullets.clear();spawnWave();invalidate();}
  void spawnWave(){int n=Math.min(5+wave*2,28);for(int i=0;i<n;i++){double a=random.nextDouble()*Math.PI*2;float d=500+random.nextFloat()*450;enemies.add(new Enemy(clamp(px+(float)Math.cos(a)*d,80,worldW-80),clamp(py+(float)Math.sin(a)*d,120,worldH-80),wave%5==0&&i==0?3:(i%4==0?2:1)));}lastSpawn=System.currentTimeMillis();}
  float clamp(float v,float a,float b){return Math.max(a,Math.min(b,v));} float distance(float a,float b,float c,float d){return (float)Math.hypot(a-c,b-d);}
  public void setGameJoystick(float x,float y){joyX=x;joyY=y;}
  public void moveByJoystick(float dx,float dy){float d=Math.max(1,(float)Math.hypot(dx,dy));float speed=7f;px=clamp(px+dx/d*speed,70,worldW-70);py=clamp(py+dy/d*speed,HUD_H+50,worldH-70);}
  public void reload(){if(gameOver||weapon!=0||ammo>=12||reserve<=0)return;int n=Math.min(12-ammo,reserve);ammo+=n;reserve-=n;}
  public void toggleWeapon(){if(!gameOver)weapon=weapon==0?1:0;}
  public void melee(){if(gameOver)return;long n=System.currentTimeMillis();if(n-lastShot<360)return;lastShot=n;float dx=aimX-px,dy=aimY-py,d=Math.max(1,(float)Math.hypot(dx,dy));for(Enemy e:enemies){if(e.hp<=0)continue;float ex=e.x-px,ey=e.y-py,ed=Math.max(1,(float)Math.hypot(ex,ey));float dot=(ex*dx+ey*dy)/(ed*d);if(ed<125&&dot>.15f){e.hp-=38;if(e.hp<=0)score+=e.type==3?40:10;}}}
  public void shoot(){if(gameOver)return;if(weapon==1){melee();return;}long n=System.currentTimeMillis();if(n-lastShot<220||ammo<=0)return;lastShot=n;ammo--;float dx=aimX-px,dy=aimY-py,d=Math.max(1,(float)Math.hypot(dx,dy));bullets.add(new Bullet(px,py,dx/d*18,dy/d*18,true));}
  void enemyShoot(Enemy e){float dx=px-e.x,dy=py-e.y,d=Math.max(1,(float)Math.hypot(dx,dy));bullets.add(new Bullet(e.x,e.y,dx/d*7,dy/d*7,false);}
  void tick(){if(gameOver)return;long now=System.currentTimeMillis();if(now-lastSpawn>9000&&enemies.isEmpty()){wave++;spawnWave();}if(joyX!=0||joyY!=0)moveByJoystick(joyX,joyY);for(Enemy e:enemies){if(e.hp<=0)continue;float dx=px-e.x,dy=py-e.y,d=Math.max(1,(float)Math.hypot(dx,dy));float sp=e.type==3?1.7f:e.type==2?1.45f:1.15f;if(d>90){e.x+=dx/d*sp;e.y+=dy/d*sp;}if(d<115&&now-e.lastHit>700){hp-=e.type==3?12:6;e.lastHit=now;if(hp<=0){hp=0;gameOver=true;}}if(d<700&&now-e.lastShot>1500){enemyShoot(e);e.lastShot=now;}}for(Bullet b:bullets){b.x+=b.vx;b.y+=b.vy;b.life--;if(b.player){for(Enemy e:enemies){if(e.hp>0&&distance(b.x,b.y,e.x,e.y)<30){e.hp-=b.damage;b.life=0;if(e.hp<=0)score+=e.type==3?40:10;break;}}}else if(distance(b.x,b.y,px,py)<28){hp-=8;b.life=0;if(hp<=0){hp=0;gameOver=true;}}}bullets.removeIf(b->b.life<=0);enemies.removeIf(e->e.hp<=0);}
  @Override protected void onDraw(Canvas c){super.onDraw(c);tick();drawWorld(c);drawEntities(c);drawHud(c);postInvalidateOnAnimation();}
  float cameraScale(){return getWidth()/VIEW_WORLD_WIDTH;}
  void drawWorld(Canvas c){world.draw(c,px,py,cameraScale(),getWidth(),getHeight(),HUD_H);}
  void drawEntities(Canvas c){float s=cameraScale(),ox=getWidth()/2f-px*s,oy=(getHeight()+HUD_H)/2f-py*s;c.save();c.translate(ox,oy);for(Bullet b:bullets)drawBullet(c,b,s);for(Enemy e:enemies)drawEnemy(c,e,s);drawPlayer(c,s);c.restore();}
  void drawDrawable(Canvas c,Drawable d,float x,float y,float size){if(d==null)return;int h=Math.max(1,Math.round(size*.5f));d.setAlpha(255);d.setBounds(Math.round(x-h),Math.round(y-h),Math.round(x+h),Math.round(y+h));d.draw(c);}
  Drawable player(){if("guard".equals(playerSkin))return guard;if("scout".equals(playerSkin))return scout;if("classic".equals(playerSkin))return classic;return archer;}
  void drawPlayer(Canvas c,float s){drawDrawable(c,player(),px*s,py*s,Math.max(76,112*s));p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,4*s));p.setColor(weapon==1?Color.rgb(225,181,70):Color.rgb(45,45,38));c.drawLine(px*s,py*s,aimX*s,aimY*s,p);p.setStyle(Paint.Style.FILL);}
  void drawEnemy(Canvas c,Enemy e,float s){drawDrawable(c,enemyArt,e.x*s,e.y*s,(e.type==3?132:e.type==2?116:100)*s);float bw=48*s,bh=Math.max(4,6*s),l=e.x*s-bw*.5f,t=(e.y-58)*s;p.setColor(Color.argb(190,20,20,20));c.drawRect(l,t,l+bw,t+bh,p);p.setColor(Color.rgb(190,55,45));float max=e.type==3?120:e.type==2?70:45;c.drawRect(l,t,l+bw*Math.max(0,e.hp/max),t+bh,p);}
  void drawBullet(Canvas c,Bullet b,float s){p.setColor(b.player?Color.rgb(250,225,115):Color.rgb(255,90,70));c.drawCircle(b.x*s,b.y*s,5*s,p);}
  void drawHud(Canvas c){p.setStyle(Paint.Style.FILL);p.setColor(Color.argb(225,12,22,19));c.drawRect(0,0,getWidth(),HUD_H,p);p.setColor(Color.rgb(245,205,105));p.setTextSize(25);c.drawText("PERSIA WAR 2.5D",22,32,p);p.setTextSize(18);p.setColor(Color.WHITE);c.drawText("WAVE "+wave+"   SCORE "+score,22,62,p);c.drawText("HP "+hp+"   AMMO "+ammo+"/"+reserve+(weapon==1?"   SWORD":"   FIREARM"),getWidth()-390,34,p);drawControls(c);if(gameOver){p.setColor(Color.argb(210,0,0,0));c.drawRect(0,0,getWidth(),getHeight(),p);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(48);p.setColor(Color.rgb(245,205,105));c.drawText("GAME OVER",getWidth()/2f,getHeight()/2f,p);p.setTextSize(20);p.setColor(Color.WHITE);c.drawText("Tap to restart",getWidth()/2f,getHeight()/2f+42,p);p.setTextAlign(Paint.Align.LEFT);}}
  void drawControls(Canvas c){float cx=getWidth()*.16f,cy=getHeight()*.79f,r=Math.min(getWidth(),getHeight())*.105f;p.setColor(Color.argb(80,255,255,255));c.drawCircle(cx,cy,r,p);p.setColor(Color.argb(125,245,205,105));float k=Math.min(r*.55f,(float)Math.hypot(joyX,joyY)*r*.12f);c.drawCircle(cx+clamp(joyX,-1,1)*k,cy+clamp(joyY,-1,1)*k,r*.45f,p);float bx=getWidth()*.84f,by=getHeight()*.79f,br=r*.72f;p.setColor(Color.argb(155,190,55,45));c.drawCircle(bx,by,br,p);p.setColor(Color.WHITE);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(Math.max(16,br*.55f));c.drawText(weapon==1?"⚔":"FIRE",bx,by+7,p);p.setTextAlign(Paint.Align.LEFT);p.setColor(Color.argb(125,40,80,60));c.drawCircle(getWidth()*.72f,getHeight()*.89f,r*.42f,p);c.drawCircle(getWidth()*.88f,getHeight()*.89f,r*.42f,p);p.setColor(Color.WHITE);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(15);c.drawText("R",getWidth()*.72f,getHeight()*.895f,p);c.drawText("SW",getWidth()*.88f,getHeight()*.895f,p);p.setTextAlign(Paint.Align.LEFT);}
  @Override public boolean onTouchEvent(MotionEvent e){float x=e.getX(),y=e.getY();if(e.getAction()==MotionEvent.ACTION_UP&&gameOver){resetGame();return true;}if(e.getAction()==MotionEvent.ACTION_DOWN||e.getAction()==MotionEvent.ACTION_MOVE){if(y>getHeight()*.65f&&x<getWidth()*.36f){joyActive=true;joyX=(x-getWidth()*.16f)/Math.max(1,getWidth()*.16f);joyY=(y-getHeight()*.79f)/Math.max(1,getHeight()*.79f);joyX=clamp(joyX,-1,1);joyY=clamp(joyY,-1,1);return true;}float s=cameraScale();aimX=px+(x-getWidth()/2f)/s;aimY=py+(y-(getHeight()+HUD_H)/2f)/s;if(y>getHeight()*.66f&&x>getWidth()*.69f){shoot();return true;}if(y>getHeight()*.83f&&x>getWidth()*.66f&&x<getWidth()*.79f){reload();return true;}if(y>getHeight()*.83f&&x>getWidth()*.80f){toggleWeapon();return true;}return true;}if(e.getAction()==MotionEvent.ACTION_UP||e.getAction()==MotionEvent.ACTION_CANCEL){joyActive=false;joyX=joyY=0;return true;}return true;}
  public static class Enemy{float x,y;int hp,type;long lastShot,lastHit;Enemy(float x,float y,int type){this.x=x;this.y=y;this.type=type;hp=type==3?120:type==2?70:45;}}
  static class Bullet{float x,y,vx,vy;int life=100,damage=25;boolean player;Bullet(float x,float y,float vx,float vy,boolean player){this.x=x;this.y=y;this.vx=vx;this.vy=vy;this.player=player;if(!player)damage=8;}}
 }
}
