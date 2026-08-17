package com.persiawar2d;

import android.app.Activity;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Matrix;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.graphics.drawable.Drawable;
import android.os.Bundle;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowManager;
import java.util.ArrayList;
import java.util.Random;

public class MainActivity extends Activity {
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
        setContentView(new GameView(this));
    }

    public static class GameView extends View {
        static final float HUD_H = 96f;
        static final float WORLD_SIZE = WorldRenderer.WORLD_SIZE;
        static final float PLAYER_RADIUS = 56f;
        // Oblique 2.5D projection: yaw rotates the ground plane and pitch compresses its depth.
        // Characters are counter-projected so they remain upright.
        static final float CAMERA_YAW = -12.0f;
        static final float CAMERA_PITCH = 0.64f;

        final Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        final Random random = new Random(20260817L);
        final WorldRenderer world;
        final KingSpriteDrawable king;
        final Drawable enemyArt;
        final ArrayList<Enemy> enemies = new ArrayList<>();
        final ArrayList<Bullet> bullets = new ArrayList<>();
        final ArrayList<Pickup> pickups = new ArrayList<>();
        final ArrayList<ThrownGrenade> thrownGrenades = new ArrayList<>();

        float px, py, aimX, aimY;
        float joyBaseX, joyBaseY, joyX, joyY, moveNX, moveNY;
        boolean joystickDown, fireDown;
        int joystickPointer = -1, firePointer = -1, aimPointer = -1;
        long lastShot, lastMelee, lastSpawn, lastFrameAt, joystickVisibleUntil;
        long playerActionUntil;
        int wave, score, ammo, reserve, grenades, hp, maxHp, shield, weapon;
        int playerDir, playerAction, playerFrame;
        boolean gameOver;
        float explosionX, explosionY;
        long explosionUntil;

        public GameView(Context context) {
            super(context);
            world = new WorldRenderer(context);
            king = new KingSpriteDrawable(context);
            enemyArt = context.getDrawable(R.drawable.persia_enemy);
            setFocusable(true);
            setLayerType(View.LAYER_TYPE_HARDWARE, null);
            resetGame();
        }

        void resetGame() {
            px = WORLD_SIZE * .5f; py = WORLD_SIZE * .55f;
            aimX = px + 900; aimY = py;
            moveNX = moveNY = 0; joystickDown = fireDown = false;
            joystickPointer = firePointer = aimPointer = -1;
            lastFrameAt = System.currentTimeMillis(); joystickVisibleUntil = lastFrameAt + 1600;
            playerActionUntil = 0;
            wave = 1; score = 0; ammo = 12; reserve = 90; grenades = 3;
            maxHp = 100; hp = maxHp; shield = 0; weapon = 0;
            playerDir = 0; playerAction = KingSpriteDrawable.ACTION_IDLE; playerFrame = 0;
            gameOver = false; explosionUntil = 0;
            enemies.clear(); bullets.clear(); pickups.clear(); thrownGrenades.clear();
            king.setState(playerDir, playerAction, playerFrame); spawnWave(); invalidate();
        }

        void spawnWave() {
            int count = Math.min(7 + wave, 15);
            for (int i = 0; i < count; i++) {
                double a = random.nextDouble() * Math.PI * 2.0;
                float d = 750 + random.nextFloat() * 950;
                float x = clamp(px + (float)Math.cos(a)*d,150,WORLD_SIZE-150);
                float y = clamp(py + (float)Math.sin(a)*d,HUD_H+150,WORLD_SIZE-150);
                int type = (i%7==0)?3:((i%3==0)?2:1);
                if(world.isBlocked(x,y,80)){x=clamp(x+220,150,WORLD_SIZE-150);y=clamp(y+180,HUD_H+150,WORLD_SIZE-150);}
                enemies.add(new Enemy(x,y,type));
            }
            spawnWaveRewards(); lastSpawn=System.currentTimeMillis();
        }
        void spawnWaveRewards(){
            pickups.clear();
            addPickup(Pickup.AMMO,clamp(px+420,180,WORLD_SIZE-180),clamp(py-180,180,WORLD_SIZE-180));
            addPickup(Pickup.GRENADE,clamp(px-420,180,WORLD_SIZE-180),clamp(py+160,180,WORLD_SIZE-180));
            addPickup(Pickup.MEDKIT,clamp(px+130,180,WORLD_SIZE-180),clamp(py+390,180,WORLD_SIZE-180));
        }
        void addPickup(int type,float x,float y){if(!world.isBlocked(x,y,45))pickups.add(new Pickup(x,y,type));}
        @Override protected void onSizeChanged(int w,int h,int oldw,int oldh){joyBaseX=w*.17f;joyBaseY=h*.80f;joyX=joyBaseX;joyY=joyBaseY;}
        float cameraScale(){return Math.min(getWidth()/1900f,Math.max(.72f,(getHeight()-HUD_H)/1000f));}

        @Override protected void onDraw(Canvas canvas){
            long now=System.currentTimeMillis();
            float dt=Math.min(.033f,Math.max(.001f,(now-lastFrameAt)/1000f)); lastFrameAt=now;
            tick(now,dt); drawWorld(canvas); drawHud(canvas); drawControls(canvas);
            if(gameOver)drawGameOver(canvas); postInvalidateOnAnimation();
        }
        void tick(long now,float dt){
            if(gameOver){
                playerAction=KingSpriteDrawable.ACTION_DIE;
                playerFrame=Math.min(KingSpriteDrawable.FRAME_COUNT-1,(int)((now/180)%KingSpriteDrawable.FRAME_COUNT));
                king.setState(playerDir,playerAction,playerFrame);
                return;
            }
            if(joystickDown&&Math.hypot(moveNX,moveNY)>.05){
                movePlayer(moveNX*370f*dt,moveNY*370f*dt);
                if(now>=playerActionUntil)animatePlayer(now);
            } else if(now<playerActionUntil){
                animateAction(now);
            } else {
                playerAction=KingSpriteDrawable.ACTION_IDLE;
                playerFrame=0;
                king.setState(playerDir,playerAction,playerFrame);
            }
            if(fireDown)shoot(); updateEnemies(now,dt); updateBullets(dt); updateGrenades(dt); collectPickups();
            if(enemies.isEmpty()&&now-lastSpawn>800){wave++;spawnWave();}
        }
        void updateEnemies(long now,float dt){
            for(Enemy e:enemies){if(e.hp<=0)continue;float dx=px-e.x,dy=py-e.y,d=Math.max(1f,(float)Math.hypot(dx,dy));float speed=e.type==3?112f:(e.type==2?92f:76f);if(d>112)moveEnemy(e,dx/d*speed*dt,dy/d*speed*dt);if(d<118&&now-e.lastHit>700){damagePlayer(e.type==3?12:6);e.lastHit=now;}long rate=e.type==3?1150:(e.type==2?1500:1800);if(d<1100&&now-e.lastShot>rate){enemyShoot(e);e.lastShot=now;}}
        }
        void moveEnemy(Enemy e,float dx,float dy){if(!world.isBlocked(e.x+dx,e.y,44))e.x+=dx;if(!world.isBlocked(e.x,e.y+dy,44))e.y+=dy;}
        void animatePlayer(long now){
            playerAction=KingSpriteDrawable.ACTION_WALK;
            int frame=(int)((now/95)%KingSpriteDrawable.FRAME_COUNT);
            if(frame!=playerFrame){playerFrame=frame;king.setState(playerDir,playerAction,playerFrame);}
        }
        void animateAction(long now){
            int frame=(int)((now/105)%KingSpriteDrawable.FRAME_COUNT);
            playerFrame=frame;
            king.setState(playerDir,playerAction,playerFrame);
        }
        void setPlayerAction(int action,long duration){
            playerAction=action;
            playerActionUntil=System.currentTimeMillis()+duration;
            playerFrame=0;
            king.setState(playerDir,playerAction,playerFrame);
        }
        void updateDirection(float nx,float ny){
            if(Math.hypot(nx,ny)<.08)return;
            if(Math.abs(nx)>Math.abs(ny))playerDir=nx<0?1:2;else playerDir=ny<0?3:0;
            king.setState(playerDir,playerAction,playerFrame);
        }
        void updateBullets(float dt){
            for(int i=bullets.size()-1;i>=0;i--){Bullet b=bullets.get(i);float oldX=b.x,oldY=b.y;b.x+=b.vx*dt;b.y+=b.vy*dt;b.life-=dt;if(b.life<=0||b.x<0||b.y<0||b.x>WORLD_SIZE||b.y>WORLD_SIZE){bullets.remove(i);continue;}if(world.isBlocked(b.x,b.y,4)){bullets.remove(i);continue;}if(b.player){boolean hit=false;for(Enemy e:enemies){if(e.hp<=0)continue;if(segmentDistance(e.x,e.y,oldX,oldY,b.x,b.y)<48){e.hp-=b.damage;hit=true;if(e.hp<=0)onEnemyKilled(e);break;}}if(hit)bullets.remove(i);}else if(segmentDistance(px,py,oldX,oldY,b.x,b.y)<38){damagePlayer(Math.round(b.damage));bullets.remove(i);}}
            for(int i=enemies.size()-1;i>=0;i--)if(enemies.get(i).hp<=0)enemies.remove(i);
        }
        void onEnemyKilled(Enemy e){score+=e.type==3?40:(e.type==2?20:10);float roll=random.nextFloat();if(roll<.15f)addPickup(Pickup.AMMO,e.x,e.y);else if(roll<.25f)addPickup(Pickup.GRENADE,e.x,e.y);else if(roll<.34f)addPickup(Pickup.MEDKIT,e.x,e.y);}
        Enemy nearestEnemy(){Enemy best=null;float bestDistance=Float.MAX_VALUE;for(Enemy e:enemies){if(e.hp<=0)continue;float d=distance(px,py,e.x,e.y);if(d<bestDistance){bestDistance=d;best=e;}}return best;}
        void autoAim(){Enemy best=nearestEnemy();if(best!=null){aimX=best.x;aimY=best.y;}}
        void enemyShoot(Enemy e){float dx=px-e.x,dy=py-e.y,d=Math.max(1f,(float)Math.hypot(dx,dy));bullets.add(new Bullet(e.x+dx/d*42,e.y+dy/d*42,dx/d*680,dy/d*680,8,false,1f));}
        void shoot(){
            if(gameOver||weapon!=0)return;long now=System.currentTimeMillis();if(now-lastShot<155)return;if(ammo<=0){reload();return;}
            Enemy target=nearestEnemy();if(target==null)return;aimX=target.x;aimY=target.y;float dx=target.x-px,dy=target.y-py,d=Math.max(1f,(float)Math.hypot(dx,dy));
            bullets.add(new Bullet(px+dx/d*60,py+dy/d*60,dx/d*1260,dy/d*1260,30,true,2.2f));ammo--;lastShot=now;setPlayerAction(KingSpriteDrawable.ACTION_ATTACK,300);
        }
        void melee(){
            if(gameOver||weapon!=1)return;long now=System.currentTimeMillis();if(now-lastMelee<320)return;lastMelee=now;Enemy target=nearestEnemy();if(target==null||distance(px,py,target.x,target.y)>170)return;
            float dx=target.x-px,dy=target.y-py,d=Math.max(1f,(float)Math.hypot(dx,dy));for(Enemy e:enemies){if(e.hp<=0)continue;float ex=e.x-px,ey=e.y-py,ed=Math.max(1f,(float)Math.hypot(ex,ey));float dot=(ex*dx+ey*dy)/(ed*d);if(ed<180&&dot>.25f){e.hp-=45;if(e.hp<=0)onEnemyKilled(e);}}setPlayerAction(KingSpriteDrawable.ACTION_ATTACK,300);
        }
        void useGrenade(){if(gameOver||grenades<=0)return;Enemy target=nearestEnemy();if(target==null)return;grenades--;float dx=target.x-px,dy=target.y-py,d=Math.max(1f,(float)Math.hypot(dx,dy));thrownGrenades.add(new ThrownGrenade(px,py,dx/d*780f,dy/d*780f,.45f));}
        void updateGrenades(float dt){for(int i=thrownGrenades.size()-1;i>=0;i--){ThrownGrenade g=thrownGrenades.get(i);g.x+=g.vx*dt;g.y+=g.vy*dt;g.life-=dt;if(g.life<=0){explode(g.x,g.y);thrownGrenades.remove(i);}}}
        void explode(float x,float y){explosionX=x;explosionY=y;explosionUntil=System.currentTimeMillis()+360;for(Enemy e:enemies){if(e.hp<=0)continue;float d=distance(x,y,e.x,e.y);if(d<260){e.hp-=d<130?90:55;if(e.hp<=0)onEnemyKilled(e);}}}
        void reload(){if(gameOver||ammo>=12||reserve<=0)return;int amount=Math.min(12-ammo,reserve);ammo+=amount;reserve-=amount;}
        void toggleWeapon(){if(!gameOver)weapon=weapon==0?1:0;}
        void movePlayer(float dx,float dy){float nx=clamp(px+dx,90,WORLD_SIZE-90),ny=clamp(py+dy,HUD_H+90,WORLD_SIZE-90);if(!world.isBlocked(nx,py,PLAYER_RADIUS))px=nx;if(!world.isBlocked(px,ny,PLAYER_RADIUS))py=ny;updateDirection(moveNX,moveNY);}
        void damagePlayer(int amount){
            int blocked=Math.min(shield,amount);shield-=blocked;amount-=blocked;
            if(amount>0){hp-=amount;if(hp<=0){hp=0;gameOver=true;fireDown=false;playerActionUntil=0;playerAction=KingSpriteDrawable.ACTION_DIE;playerFrame=0;king.setState(playerDir,playerAction,playerFrame);}else setPlayerAction(KingSpriteDrawable.ACTION_HURT,240);}
        }
        void collectPickups(){for(int i=pickups.size()-1;i>=0;i--){Pickup item=pickups.get(i);if(distance(px,py,item.x,item.y)>75)continue;if(item.type==Pickup.AMMO)reserve=Math.min(180,reserve+30);else if(item.type==Pickup.GRENADE)grenades=Math.min(9,grenades+1);else if(item.type==Pickup.MEDKIT)hp=Math.min(maxHp,hp+35);pickups.remove(i);}}

        void drawWorld(Canvas c){
            float s=cameraScale();float cx=getWidth()*.5f,cy=HUD_H+(getHeight()-HUD_H)*.5f;
            Matrix camera=new Matrix();camera.setRotate(CAMERA_YAW,cx,cy);camera.postScale(1f,CAMERA_PITCH,cx,cy);
            c.save();c.concat(camera);
            world.draw(c,px,py,s,getWidth(),getHeight(),HUD_H);
            float ox=getWidth()/2f-px*s,oy=HUD_H+(getHeight()-HUD_H)/2f-py*s;
            c.save();c.translate(ox,oy);
            for(Pickup item:pickups)drawPickup(c,item,s);for(ThrownGrenade g:thrownGrenades)drawThrownGrenade(c,g,s);for(Bullet b:bullets)drawBullet(c,b,s);for(Enemy e:enemies)if(e.hp>0)drawEnemy(c,e,s);drawPlayer(c,s);
            if(System.currentTimeMillis()<explosionUntil)drawExplosion(c,s);c.restore();world.drawForeground(c,px,py,s,getWidth(),getHeight(),HUD_H);c.restore();
        }
        void drawPlayer(Canvas c,float s){
            float x=px*s,y=py*s;p.setStyle(Paint.Style.FILL);p.setColor(0x55000000);c.drawOval(x-40*s,y+45*s,x+40*s,y+62*s,p);
            king.setState(playerDir,playerAction,playerFrame);king.setAlpha(255);int width=Math.max(92,Math.round(116*s)),height=Math.max(154,Math.round(210*s));int halfW=width/2,halfH=height/2;
            c.save();c.translate(x,y-8*s);c.scale(1f,1f/CAMERA_PITCH);king.setBounds(-halfW,-halfH,halfW,halfH);king.draw(c);c.restore();
            if(shield>0){p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,3*s));p.setColor(0xAA52DFFF);c.drawOval(x-56*s,y-78*s,x+56*s,y+58*s,p);p.setStyle(Paint.Style.FILL);}
        }
        void drawEnemy(Canvas c,Enemy e,float s){
            if(enemyArt==null)return;float x=e.x*s,y=e.y*s;int size=Math.round((e.type==3?146:(e.type==2?130:116))*s),half=size/2;
            c.save();c.translate(x,y);c.scale(1f,1f/CAMERA_PITCH);enemyArt.setAlpha(255);enemyArt.setBounds(-half,-half,half,half);enemyArt.draw(c);c.restore();
            float bw=68*s,bh=Math.max(5,7*s),left=x-bw*.5f,top=(e.y-88)*s;p.setStyle(Paint.Style.FILL);p.setColor(0xB4141414);c.drawRoundRect(left,top,left+bw,top+bh,bh,bh,p);float max=e.type==3?120:(e.type==2?70:45);p.setColor(Color.rgb(196,55,45));c.drawRoundRect(left,top,left+bw*Math.max(0,e.hp/max),top+bh,bh,bh,p);
        }
        void drawBullet(Canvas c,Bullet b,float s){float x=b.x*s,y=b.y*s;p.setStyle(Paint.Style.FILL);p.setColor(b.player?Color.rgb(255,218,87):Color.rgb(255,80,65));c.drawCircle(x,y,Math.max(4,6*s),p);}
        void drawPickup(Canvas c,Pickup item,float s){float x=item.x*s,y=item.y*s,pulse=1f+.08f*(float)Math.sin(System.currentTimeMillis()/180.0+item.type);p.setStyle(Paint.Style.FILL);p.setColor(0x3D000000);c.drawOval(x-26*s,y+20*s,x+26*s,y+31*s,p);if(item.type==Pickup.AMMO){p.setColor(Color.rgb(218,177,70));c.drawRoundRect(x-18*s,y-20*s,x+18*s,y+20*s,8*s,8*s,p);p.setColor(Color.rgb(87,69,41));c.drawRect(x-8*s,y-12*s,x-3*s,y+12*s,p);c.drawRect(x+4*s,y-12*s,x+9*s,y+12*s,p);}else if(item.type==Pickup.GRENADE){p.setColor(Color.rgb(55,92,58));c.drawCircle(x,y,18*s*pulse,p);p.setColor(Color.rgb(214,180,78));c.drawRect(x+4*s,y-18*s,x+11*s,y-8*s,p);}else{p.setColor(Color.rgb(205,63,58));c.drawRoundRect(x-20*s,y-16*s,x+20*s,y+16*s,8*s,8*s,p);p.setColor(Color.WHITE);c.drawRect(x-6*s,y-12*s,x+6*s,y+12*s,p);c.drawRect(x-12*s,y-6*s,x+12*s,y+6*s,p);}}
        void drawThrownGrenade(Canvas c,ThrownGrenade g,float s){p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(61,99,62));c.drawCircle(g.x*s,g.y*s,12*s,p);}
        void drawExplosion(Canvas c,float s){float left=Math.max(0,explosionUntil-System.currentTimeMillis()),alpha=left/360f;p.setStyle(Paint.Style.FILL);p.setColor((int)(120*alpha)<<24|0xF2B84B);c.drawCircle(explosionX*s,explosionY*s,170*s*(1f-alpha*.35f),p);p.setColor((int)(170*alpha)<<24|0xFFE5A1);c.drawCircle(explosionX*s,explosionY*s,85*s*(1f-alpha*.2f),p);}

        void drawHud(Canvas c){p.setStyle(Paint.Style.FILL);p.setColor(Color.argb(228,14,22,20));c.drawRect(0,0,getWidth(),HUD_H,p);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.LEFT);p.setTextSize(23);p.setColor(Color.rgb(244,208,113));c.drawText("PERSIA WAR 2.5D",20,30,p);p.setTypeface(Typeface.DEFAULT);p.setTextSize(16);p.setColor(Color.WHITE);c.drawText("WAVE "+wave+"   SCORE "+score,20,62,p);float barW=Math.min(290,getWidth()*.34f),barX=getWidth()-barW-22,barY=17;p.setColor(0xFF2A332D);c.drawRoundRect(barX,barY,barX+barW,barY+26,13,13,p);float hpW=barW*hp/(float)maxHp;p.setColor(hp>35?Color.rgb(80,177,92):Color.rgb(204,74,61));c.drawRoundRect(barX+3,barY+3,barX+Math.max(6,hpW-3),barY+23,10,10,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2);p.setColor(0xE8CFC990);c.drawRoundRect(barX,barY,barX+barW,barY+26,13,13,p);p.setStyle(Paint.Style.FILL);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(14);p.setColor(Color.WHITE);c.drawText("HP "+hp+"%",barX+barW/2f,barY+18,p);p.setTextAlign(Paint.Align.RIGHT);p.setTextSize(14);c.drawText("AMMO "+ammo+"/"+reserve+"   G "+grenades+(weapon==1?"   SWORD":"   FIREARM"),getWidth()-22,70,p);p.setTextAlign(Paint.Align.LEFT);}
        void drawControls(Canvas c){long age=Math.max(0,joystickVisibleUntil-System.currentTimeMillis());int alpha=joystickDown?240:(int)Math.max(35,Math.min(220,70+age/10));float jx=joyBaseX,jy=joyBaseY;p.setStyle(Paint.Style.FILL);p.setColor((alpha<<24)|0x405149);c.drawCircle(jx,jy,106,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(4);p.setColor((alpha<<24)|0xD5C584);c.drawCircle(jx,jy,106,p);p.setStyle(Paint.Style.FILL);p.setColor((alpha<<24)|0xC4AF63);c.drawCircle(joyX,joyY,43,p);float br=Math.max(92,Math.min(132,getHeight()*.14f));float fireX=getWidth()*.83f,fireY=getHeight()*.72f,grenadeX=getWidth()*.67f,grenadeY=getHeight()*.72f,reloadX=getWidth()*.78f,reloadY=getHeight()*.91f,weaponX=getWidth()*.91f,weaponY=getHeight()*.91f,swordX=getWidth()*.63f,swordY=getHeight()*.55f;actionButton(c,fireX,fireY,br*1.10f,0xD19D4A3E,"F",28);actionButton(c,grenadeX,grenadeY,br*.68f,0xC058704E,"G",24);actionButton(c,reloadX,reloadY,br*.54f,0xB0455B55,"R",18);actionButton(c,weaponX,weaponY,br*.54f,0xB0455B55,"W",18);actionButton(c,swordX,swordY,br*.58f,0xB05B503D,"S",22);}
        void actionButton(Canvas c,float x,float y,float r,int fill,String label,float textSize){p.setStyle(Paint.Style.FILL);p.setColor(0x33000000);c.drawCircle(x,y+7,r+4,p);p.setColor(fill);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xD7E7D29A);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.FILL);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(textSize);p.setColor(Color.WHITE);c.drawText(label,x,y+textSize*.34f,p);p.setTypeface(Typeface.DEFAULT);}
        void drawGameOver(Canvas c){p.setStyle(Paint.Style.FILL);p.setColor(0xDD000000);c.drawRect(0,0,getWidth(),getHeight(),p);centeredText(c,"GAME OVER",getWidth()/2f,getHeight()/2f-25,Color.WHITE,52);centeredText(c,"TAP TO RESTART",getWidth()/2f,getHeight()/2f+30,Color.rgb(238,210,150),22);}
        void centeredText(Canvas c,String text,float x,float y,int color,float size){p.setStyle(Paint.Style.FILL);p.setColor(color);p.setTextAlign(Paint.Align.CENTER);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextSize(size);c.drawText(text,x,y,p);p.setTypeface(Typeface.DEFAULT);}

        @Override public boolean onTouchEvent(MotionEvent event){
            int action=event.getActionMasked();if(gameOver){if(action==MotionEvent.ACTION_DOWN)resetGame();return true;}
            float br=Math.max(92,Math.min(132,getHeight()*.14f));float fireX=getWidth()*.83f,fireY=getHeight()*.72f,grenadeX=getWidth()*.67f,grenadeY=getHeight()*.72f,reloadX=getWidth()*.78f,reloadY=getHeight()*.91f,weaponX=getWidth()*.91f,weaponY=getHeight()*.91f,swordX=getWidth()*.63f,swordY=getHeight()*.55f;
            if(action==MotionEvent.ACTION_DOWN||action==MotionEvent.ACTION_POINTER_DOWN){int idx=event.getActionIndex(),id=event.getPointerId(idx);float x=event.getX(idx),y=event.getY(idx);if(near(x,y,fireX,fireY,br*1.34f)){firePointer=id;fireDown=true;shoot();return true;}if(near(x,y,grenadeX,grenadeY,br*.82f)){useGrenade();return true;}if(near(x,y,reloadX,reloadY,br*.68f)){reload();return true;}if(near(x,y,weaponX,weaponY,br*.68f)){toggleWeapon();return true;}if(near(x,y,swordX,swordY,br*.75f)){melee();return true;}if(x<getWidth()*.52f&&y>HUD_H){joystickPointer=id;joystickDown=true;joystickVisibleUntil=System.currentTimeMillis()+1800;joyBaseX=clamp(x,108,getWidth()*.46f);joyBaseY=clamp(y,HUD_H+108,getHeight()-108);joyX=joyBaseX;joyY=joyBaseY;moveNX=moveNY=0;return true;}if(y>HUD_H){aimPointer=id;setAimFromScreen(x,y);return true;}}
            if(action==MotionEvent.ACTION_MOVE){for(int i=0;i<event.getPointerCount();i++){int id=event.getPointerId(i);float x=event.getX(i),y=event.getY(i);if(id==joystickPointer){float dx=x-joyBaseX,dy=y-joyBaseY,mag=Math.max(1f,(float)Math.hypot(dx,dy)),max=92f,use=Math.min(max,mag);joyX=joyBaseX+dx/mag*use;joyY=joyBaseY+dy/mag*use;moveNX=(joyX-joyBaseX)/max;moveNY=(joyY-joyBaseY)/max;}if(id==aimPointer)setAimFromScreen(x,y);}return true;}
            if(action==MotionEvent.ACTION_UP||action==MotionEvent.ACTION_POINTER_UP||action==MotionEvent.ACTION_CANCEL){int id=event.getPointerId(event.getActionIndex());if(id==joystickPointer){joystickPointer=-1;joystickDown=false;moveNX=moveNY=0;joyX=joyBaseX;joyY=joyBaseY;joystickVisibleUntil=System.currentTimeMillis()+1200;}if(id==firePointer){firePointer=-1;fireDown=false;}if(id==aimPointer)aimPointer=-1;return true;}return true;
        }

        /** Inverse of the same oblique camera projection used for the world, keeping touch aiming aligned. */
        void setAimFromScreen(float sx,float sy){
            float s=cameraScale();float cx=getWidth()*.5f,cy=HUD_H+(getHeight()-HUD_H)*.5f;float dx=sx-cx,dy=(sy-cy)/CAMERA_PITCH;
            double a=Math.toRadians(CAMERA_YAW),cos=Math.cos(a),sin=Math.sin(a);float projectedX=(float)(dx*cos+dy*sin);float projectedY=(float)(-dx*sin+dy*cos);
            float ox=getWidth()/2f-px*s,oy=HUD_H+(getHeight()-HUD_H)/2f-py*s;aimX=(projectedX-(ox-cx))/s;aimY=(projectedY-(oy-cy))/s;
        }
        boolean near(float x,float y,float cx,float cy,float r){return Math.hypot(x-cx,y-cy)<=r;}float distance(float x1,float y1,float x2,float y2){return(float)Math.hypot(x1-x2,y1-y2);}float segmentDistance(float px,float py,float x1,float y1,float x2,float y2){float dx=x2-x1,dy=y2-y1;if(dx==0&&dy==0)return distance(px,py,x1,y1);float t=((px-x1)*dx+(py-y1)*dy)/(dx*dx+dy*dy);t=Math.max(0,Math.min(1,t));return distance(px,py,x1+t*dx,y1+t*dy);}float clamp(float v,float lo,float hi){return Math.max(lo,Math.min(hi,v));}
    }

    static final class Enemy { float x,y; int hp,type; long lastShot,lastHit; Enemy(float x,float y,int type){this.x=x;this.y=y;this.type=type;this.hp=type==3?120:(type==2?70:45);} }
    static final class Bullet { float x,y,vx,vy,damage,life; boolean player; Bullet(float x,float y,float vx,float vy,float damage,boolean player,float life){this.x=x;this.y=y;this.vx=vx;this.vy=vy;this.damage=damage;this.player=player;this.life=life;} }
    static final class ThrownGrenade { float x,y,vx,vy,life; ThrownGrenade(float x,float y,float vx,float vy,float life){this.x=x;this.y=y;this.vx=vx;this.vy=vy;this.life=life;} }
    static final class Pickup { static final int AMMO=1,GRENADE=2,MEDKIT=3; final float x,y; final int type; Pickup(float x,float y,int type){this.x=x;this.y=y;this.type=type;} }
}
