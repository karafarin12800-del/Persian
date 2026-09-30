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
        // Stable 2.5D projection: no yaw rotation, so roads/buildings keep their intended axes.
        // A vertical compression gives depth while characters remain upright via counter-projection.
        static final float CAMERA_YAW = 0.0f;
        static final float CAMERA_PITCH = 0.80f;

        final Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        final Random random = new Random(20260817L);
        final WorldRenderer world;
        final KingSpriteDrawable king;
        final Drawable enemyArt;
        final Drawable achaemenidEnemyArt;
        final Drawable achaemenidPlayerArt;
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
            achaemenidEnemyArt = context.getDrawable(R.drawable.achaemenid_enemy);
            achaemenidPlayerArt = context.getDrawable(R.drawable.achaemenid_player);
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
        @Override protected void onSizeChanged(int w,int h,int oldw,int oldh){joyBaseX=w*.16f;joyBaseY=h*.80f;joyX=joyBaseX;joyY=joyBaseY;}
        float cameraScale(){return Math.min(getWidth()/2200f,Math.max(.62f,(getHeight()-HUD_H)/1180f));}

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
                movePlayer(moveNX*620f*dt,moveNY*620f*dt);
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
            float x=px*s,y=py*s;
            float bob=(joystickDown?3f:1.2f)*(float)Math.sin(System.currentTimeMillis()/110.0);
            float dx=aimX-px,dy=aimY-py,angle=(float)Math.atan2(dy,dx);
            p.setStyle(Paint.Style.FILL);p.setColor(0x55000000);c.drawOval(x-48*s,y+46*s,x+48*s,y+68*s,p);
            c.save();c.translate(x,y+bob*s);c.scale(1f,1f/CAMERA_PITCH);
            p.setColor(0xFF25221E);c.drawRoundRect(-22*s,34*s,-4*s,73*s,6*s,6*s,p);c.drawRoundRect(4*s,34*s,22*s,73*s,6*s,6*s,p);
            p.setColor(0xFF111111);c.drawRoundRect(-27*s,65*s,1*s,77*s,5*s,5*s,p);c.drawRoundRect(-1*s,65*s,27*s,77*s,5*s,5*s,p);
            p.setColor(0xFFC6923B);c.drawRoundRect(-34*s,-12*s,34*s,48*s,14*s,14*s,p);
            p.setColor(0xFF174D5A);c.drawRect(-34*s,8*s,34*s,18*s,p);p.setColor(0xFFE0B95B);c.drawRect(-28*s,15*s,28*s,21*s,p);
            p.setColor(0xFF8A5A25);c.drawCircle(-31*s,-2*s,8*s,p);c.drawCircle(31*s,-2*s,8*s,p);
            p.setStrokeWidth(12*s);p.setStrokeCap(Paint.Cap.ROUND);p.setColor(0xFFC88F65);c.drawLine(-25*s,2*s,-43*s,29*s,p);c.drawLine(25*s,2*s,43*s,29*s,p);
            p.setColor(0xFFC88F65);c.drawCircle(0,-42*s,25*s,p);p.setColor(0xFF33251E);c.drawArc(-18*s,-43*s,18*s,-13*s,0,180,true,p);
            p.setColor(0xFFD4A43E);c.drawRoundRect(-27*s,-66*s,27*s,-48*s,9*s,9*s,p);p.setColor(0xFF8C672B);c.drawRect(-31*s,-51*s,31*s,-45*s,p);
            p.setColor(0xFF241D19);c.drawCircle(-9*s,-43*s,3*s,p);c.drawCircle(9*s,-43*s,3*s,p);
            c.save();c.rotate(angle*57.29578f);p.setStrokeWidth(9*s);p.setStrokeCap(Paint.Cap.SQUARE);p.setColor(0xFF292724);c.drawLine(28*s,8*s,92*s,8*s,p);
            p.setStrokeWidth(4*s);p.setColor(0xFFD3A63E);c.drawLine(50*s,8*s,83*s,8*s,p);c.restore();p.setStrokeCap(Paint.Cap.BUTT);c.restore();
            if(shield>0){p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(Math.max(2,3*s));p.setColor(0xAA52DFFF);c.drawOval(x-58*s,y-80*s,x+58*s,y+58*s,p);p.setStyle(Paint.Style.FILL);}
        }
        void drawEnemy(Canvas c,Enemy e,float s){
            float x=e.x*s,y=e.y*s,bob=1.5f*(float)Math.sin((System.currentTimeMillis()+e.type*90)/125.0);
            p.setStyle(Paint.Style.FILL);p.setColor(0x50000000);c.drawOval(x-42*s,y+42*s,x+42*s,y+62*s,p);

            // TEMP ART: use the bundled Achaemenid vector character for a cleaner visual test.
            if(achaemenidEnemyArt!=null){
                c.save();c.translate(x,y+bob*s);c.scale(1f,1f/CAMERA_PITCH);
                float half=e.type==3?48f:43f;
                achaemenidEnemyArt.setBounds((int)(-half*s),(int)(-88f*s),(int)(half*s),(int)(62f*s));
                achaemenidEnemyArt.draw(c);
                c.restore();
            }

            float max=e.type==3?120:(e.type==2?70:45),pct=Math.max(0,Math.min(1,e.hp/max));
            float bw=(e.type==3?88:70)*s,bh=7*s,left=x-bw*.5f,top=(e.y-92)*s;
            p.setColor(0xB51A1917);c.drawRoundRect(left,top,left+bw,top+bh,4*s,4*s,p);
            p.setColor(e.type==3?0xFFE0A943:0xFFE14A3E);c.drawRoundRect(left+2*s,top+2*s,left+2*s+(bw-4*s)*pct,top+bh-2*s,3*s,3*s,p);
        }

        void drawBullet(Canvas c,Bullet b,float s){
            float x=b.x*s,y=b.y*s,len=Math.max(18*s,Math.min(46*s,(float)Math.hypot(b.vx,b.vy)*.018f*s));
            float d=Math.max(1f,(float)Math.hypot(b.vx,b.vy)),ex=x-b.vx/d*len,ey=y-b.vy/d*len;
            p.setStyle(Paint.Style.STROKE);p.setStrokeCap(Paint.Cap.ROUND);p.setStrokeWidth(Math.max(7*s,10*s));p.setColor(b.player?0x4432D9FF:0x44FF3C35);c.drawLine(ex,ey,x,y,p);
            p.setStrokeWidth(Math.max(3*s,5*s));p.setColor(b.player?0xFFFFD66B:0xFFFF594D);c.drawLine(ex,ey,x,y,p);
            p.setStrokeCap(Paint.Cap.BUTT);p.setStyle(Paint.Style.FILL);c.drawCircle(x,y,Math.max(3*s,5*s),p);
        }
        void drawPickup(Canvas c,Pickup item,float s){float x=item.x*s,y=item.y*s,pulse=1f+.08f*(float)Math.sin(System.currentTimeMillis()/180.0+item.type);p.setStyle(Paint.Style.FILL);p.setColor(0x3D000000);c.drawOval(x-26*s,y+20*s,x+26*s,y+31*s,p);if(item.type==Pickup.AMMO){p.setColor(Color.rgb(218,177,70));c.drawRoundRect(x-18*s,y-20*s,x+18*s,y+20*s,8*s,8*s,p);p.setColor(Color.rgb(87,69,41));c.drawRect(x-8*s,y-12*s,x-3*s,y+12*s,p);c.drawRect(x+4*s,y-12*s,x+9*s,y+12*s,p);}else if(item.type==Pickup.GRENADE){p.setColor(Color.rgb(55,92,58));c.drawCircle(x,y,18*s*pulse,p);p.setColor(Color.rgb(214,180,78));c.drawRect(x+4*s,y-18*s,x+11*s,y-8*s,p);}else{p.setColor(Color.rgb(205,63,58));c.drawRoundRect(x-20*s,y-16*s,x+20*s,y+16*s,8*s,8*s,p);p.setColor(Color.WHITE);c.drawRect(x-6*s,y-12*s,x+6*s,y+12*s,p);c.drawRect(x-12*s,y-6*s,x+12*s,y+6*s,p);}}
        void drawThrownGrenade(Canvas c,ThrownGrenade g,float s){p.setStyle(Paint.Style.FILL);p.setColor(Color.rgb(61,99,62));c.drawCircle(g.x*s,g.y*s,12*s,p);}
        void drawExplosion(Canvas c,float s){float left=Math.max(0,explosionUntil-System.currentTimeMillis()),alpha=left/360f;p.setStyle(Paint.Style.FILL);p.setColor((int)(120*alpha)<<24|0xF2B84B);c.drawCircle(explosionX*s,explosionY*s,170*s*(1f-alpha*.35f),p);p.setColor((int)(170*alpha)<<24|0xFFE5A1);c.drawCircle(explosionX*s,explosionY*s,85*s*(1f-alpha*.2f),p);}

        void drawHud(Canvas c){
            p.setStyle(Paint.Style.FILL);p.setColor(0xE8171A18);c.drawRect(0,0,getWidth(),HUD_H,p);
            p.setColor(0xCC2A2924);c.drawRoundRect(14,9,300,HUD_H-9,18,18,p);
            if(achaemenidPlayerArt!=null){achaemenidPlayerArt.setBounds(20,16,72,68);achaemenidPlayerArt.draw(c);}
            p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.LEFT);p.setTextSize(20);p.setColor(0xFFF0C86A);c.drawText("PERSIA WAR",84,31,p);
            p.setTypeface(Typeface.DEFAULT);p.setTextSize(12);p.setColor(0xFFD8D1BF);c.drawText("ROYAL GUARD  •  2.5D",84,51,p);
            p.setTextSize(14);p.setColor(Color.WHITE);c.drawText("KILLS  "+score,84,70,p);
            float cx=getWidth()*.5f;p.setColor(0xCC2A2924);c.drawRoundRect(cx-180,9,cx+180,HUD_H-9,18,18,p);
            p.setTextAlign(Paint.Align.CENTER);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextSize(15);p.setColor(0xFFEAD9AE);c.drawText("WAVE  "+wave,cx,30,p);
            float bw=220,bx=cx-bw/2f,by=43;p.setColor(0xFF151714);c.drawRoundRect(bx,by,bx+bw,by+15,8,8,p);
            p.setColor(hp>35?0xFF5BC46A:0xFFE05247);c.drawRoundRect(bx+2,by+2,bx+2+(bw-4)*hp/(float)maxHp,by+13,6,6,p);
            p.setTextSize(11);p.setColor(Color.WHITE);c.drawText("HP  "+hp+" / "+maxHp,cx,68,p);
            float rx=getWidth()-314;p.setTextAlign(Paint.Align.LEFT);p.setColor(0xCC2A2924);c.drawRoundRect(rx,9,getWidth()-14,HUD_H-9,18,18,p);
            p.setTextSize(12);p.setColor(0xFFBEB7A6);c.drawText(weapon==0?"ROYAL BOW":"ROYAL BLADE",rx+16,29,p);
            p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextSize(21);p.setColor(0xFFFFD56A);c.drawText(ammo+" / "+reserve,rx+16,53,p);
            p.setTypeface(Typeface.DEFAULT);p.setTextSize(11);p.setColor(0xFFD8D1BF);c.drawText("AMMO     GRENADES  "+grenades,rx+112,53,p);
            p.setTextSize(11);p.setColor(0xFF8FAEA0);c.drawText("AUTO-AIM READY",rx+112,29,p);p.setTextAlign(Paint.Align.LEFT);
        }
        void drawControls(Canvas c){
            long age=Math.max(0,joystickVisibleUntil-System.currentTimeMillis());int alpha=joystickDown?235:(int)Math.max(42,Math.min(185,70+age/10));
            float jx=getWidth()*.16f,jy=getHeight()*.80f;p.setStyle(Paint.Style.FILL);p.setColor((alpha<<24)|0x26322F);c.drawCircle(jx,jy,104,p);
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor((alpha<<24)|0xD8C98E);c.drawCircle(jx,jy,104,p);
            p.setStyle(Paint.Style.FILL);p.setColor((alpha<<24)|0xC7A955);c.drawCircle(joyX,joyY,34,p);
            p.setStyle(Paint.Style.STROKE);p.setColor((alpha<<24)|0xFFF1D98A);c.drawCircle(joyX,joyY,34,p);
            p.setStyle(Paint.Style.FILL);p.setTextAlign(Paint.Align.CENTER);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextSize(11);p.setColor(0xCCFFFFFF);c.drawText("MOVE",jx,jy+132,p);
            float br=Math.max(78,Math.min(112,getHeight()*.125f));float fireX=getWidth()*.84f,fireY=getHeight()*.79f;
            actionButton(c,fireX,fireY,br,0xD17D302B,"FIRE",20);actionButton(c,getWidth()*.68f,getHeight()*.69f,br*.52f,0xB35B684B,"GRENADE",12);
            actionButton(c,getWidth()*.76f,getHeight()*.91f,br*.48f,0xB04B5651,"RELOAD",11);actionButton(c,getWidth()*.90f,getHeight()*.91f,br*.48f,0xB04B5651,"WEAPON",10);
            Enemy target=nearestEnemy();if(target!=null){float dx=(target.x-px)*cameraScale(),dy=(target.y-py)*cameraScale(),tx=getWidth()*.5f+dx,ty=HUD_H+(getHeight()-HUD_H)*.5f+dy;
                if(tx>30&&tx<getWidth()-30&&ty>HUD_H+20&&ty<getHeight()-30){p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xD7E5C05C);
                    c.drawCircle(tx,ty,25,p);c.drawLine(tx-34,ty,tx-18,ty,p);c.drawLine(tx+18,ty,tx+34,ty,p);c.drawLine(tx,ty-34,tx,ty-18,p);c.drawLine(tx,ty+18,tx,ty+34,p);}
            }
            p.setStyle(Paint.Style.FILL);p.setTypeface(Typeface.DEFAULT);p.setTextAlign(Paint.Align.LEFT);
        }
        void actionButton(Canvas c,float x,float y,float r,int fill,String label,float textSize){p.setStyle(Paint.Style.FILL);p.setColor(0x33000000);c.drawCircle(x,y+7,r+4,p);p.setColor(fill);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xD7E7D29A);c.drawCircle(x,y,r,p);p.setStyle(Paint.Style.FILL);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(textSize);p.setColor(Color.WHITE);c.drawText(label,x,y+textSize*.34f,p);p.setTypeface(Typeface.DEFAULT);}
        void drawGameOver(Canvas c){p.setStyle(Paint.Style.FILL);p.setColor(0xDD000000);c.drawRect(0,0,getWidth(),getHeight(),p);centeredText(c,"GAME OVER",getWidth()/2f,getHeight()/2f-25,Color.WHITE,52);centeredText(c,"TAP TO RESTART",getWidth()/2f,getHeight()/2f+30,Color.rgb(238,210,150),22);}
        void centeredText(Canvas c,String text,float x,float y,int color,float size){p.setStyle(Paint.Style.FILL);p.setColor(color);p.setTextAlign(Paint.Align.CENTER);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextSize(size);c.drawText(text,x,y,p);p.setTypeface(Typeface.DEFAULT);}

        @Override public boolean onTouchEvent(MotionEvent event){
            int action=event.getActionMasked();if(gameOver){if(action==MotionEvent.ACTION_DOWN)resetGame();return true;}
            float br=Math.max(92,Math.min(132,getHeight()*.14f));float fireX=getWidth()*.83f,fireY=getHeight()*.72f,grenadeX=getWidth()*.67f,grenadeY=getHeight()*.72f,reloadX=getWidth()*.78f,reloadY=getHeight()*.91f,weaponX=getWidth()*.91f,weaponY=getHeight()*.91f,swordX=getWidth()*.63f,swordY=getHeight()*.55f;
            if(action==MotionEvent.ACTION_DOWN||action==MotionEvent.ACTION_POINTER_DOWN){
                int idx=event.getActionIndex(),id=event.getPointerId(idx);float x=event.getX(idx),y=event.getY(idx);
                if(near(x,y,fireX,fireY,br*1.34f)){firePointer=id;fireDown=true;shoot();return true;}
                if(near(x,y,grenadeX,grenadeY,br*.82f)){useGrenade();return true;}
                if(near(x,y,reloadX,reloadY,br*.68f)){reload();return true;}
                if(near(x,y,weaponX,weaponY,br*.68f)){toggleWeapon();return true;}
                if(near(x,y,swordX,swordY,br*.75f)){melee();return true;}
                if(joystickPointer==-1&&x<getWidth()*.52f&&y>HUD_H){
                    joystickPointer=id;joystickDown=true;joystickVisibleUntil=System.currentTimeMillis()+1800;
                    joyX=joyBaseX;joyY=joyBaseY;
                    updateJoystick(x,y);
                    return true;
                }
                if(y>HUD_H&&aimPointer==-1){aimPointer=id;setAimFromScreen(x,y);return true;}
            }
            if(action==MotionEvent.ACTION_MOVE){
                for(int i=0;i<event.getPointerCount();i++){
                    int id=event.getPointerId(i);float x=event.getX(i),y=event.getY(i);
                    if(id==joystickPointer)updateJoystick(x,y);
                    if(id==aimPointer)setAimFromScreen(x,y);
                }
                return true;
            }
            if(action==MotionEvent.ACTION_UP||action==MotionEvent.ACTION_POINTER_UP||action==MotionEvent.ACTION_CANCEL){
                int id=event.getPointerId(event.getActionIndex());
                if(id==joystickPointer){joystickPointer=-1;joystickDown=false;moveNX=moveNY=0;joyX=joyBaseX;joyY=joyBaseY;joystickVisibleUntil=System.currentTimeMillis()+1200;}
                if(id==firePointer){firePointer=-1;fireDown=false;}
                if(id==aimPointer)aimPointer=-1;
                return true;
            }
            return true;
        }

        private void updateJoystick(float x,float y){
            float dx=x-joyBaseX,dy=y-joyBaseY,mag=Math.max(1f,(float)Math.hypot(dx,dy)),max=112f,use=Math.min(max,mag);
            joyX=joyBaseX+dx/mag*use;joyY=joyBaseY+dy/mag*use;
            moveNX=(joyX-joyBaseX)/max;moveNY=(joyY-joyBaseY)/max;
        }

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
