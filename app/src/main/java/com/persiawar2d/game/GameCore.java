package com.persiawar2d.game;

import com.persiawar2d.world.WorldMap;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import java.util.Random;

/** Single source of truth for movement, targeting, combat, loot, patrol and zone. */
public final class GameCore {
    public static final float WORLD_SIZE=WorldMap.SIZE;
    public static final float MAX_TARGET_DISTANCE=1800f;
    public enum PickupType{AMMO,MEDKIT,GRENADE,SHIELD}
    public enum EnemyState{PATROL,CHASE,ATTACK,DEAD}
    public static final class Input{public float moveX,moveY;public boolean fire,sword,grenade,reload;}
    public static final class Player{public float x,y,speed;public int hp,maxHp,shield,ammo,reserveAmmo,grenades,score;public String skin;public boolean dead;}
    public static final class Enemy{public float x,y,patrolX,patrolY,attackCooldown,patrolWait,deathTimer;public int hp,maxHp,type;public EnemyState state=EnemyState.PATROL;public boolean dead;}
    public static final class Projectile{public float x,y,vx,vy,damage,life;public boolean fromPlayer;Projectile(float x,float y,float vx,float vy,float damage,float life,boolean fromPlayer){this.x=x;this.y=y;this.vx=vx;this.vy=vy;this.damage=damage;this.life=life;this.fromPlayer=fromPlayer;}}
    public static final class Grenade{public float x,y,vx,vy,life;Grenade(float x,float y,float vx,float vy,float life){this.x=x;this.y=y;this.vx=vx;this.vy=vy;this.life=life;}}
    public static final class Pickup{public float x,y;public PickupType type;Pickup(float x,float y,PickupType type){this.x=x;this.y=y;this.type=type;}}
    public static final class Explosion{public float x,y,life,radius;Explosion(float x,float y,float radius){this.x=x;this.y=y;this.radius=radius;this.life=.38f;}}

    private final WorldMap world; private final Random random=new Random(20260902L); private final Player player=new Player();
    private final ArrayList<Enemy> enemies=new ArrayList<>(); private final ArrayList<Projectile> projectiles=new ArrayList<>(); private final ArrayList<Grenade> grenades=new ArrayList<>(); private final ArrayList<Pickup> pickups=new ArrayList<>(); private final ArrayList<Explosion> explosions=new ArrayList<>();
    private final String skin; private float fireCd,swordCd,grenadeCd,reinforceCd,zoneTimer,zoneRadius=2450f,damageCarry; private int kills; private boolean gameOver;

    public GameCore(WorldMap world,String skin){this.world=world;this.skin=skin==null?"classic":skin;reset();}
    public void reset(){enemies.clear();projectiles.clear();grenades.clear();pickups.clear();explosions.clear();fireCd=swordCd=grenadeCd=0;reinforceCd=24;zoneTimer=0;damageCarry=0;zoneRadius=2450;kills=0;gameOver=false;setProfile();player.x=WORLD_SIZE*.5f;player.y=WORLD_SIZE*.5f;spawnEnemies(14);spawnStartingLoot();}
    private void setProfile(){player.skin=skin;player.maxHp=100;player.hp=100;player.shield=20;player.speed=360;player.ammo=12;player.reserveAmmo=90;player.grenades=3;if("blue".equals(skin)){player.maxHp=105;player.hp=105;player.shield=35;player.speed=335;}else if("red".equals(skin)){player.maxHp=95;player.hp=95;player.shield=10;player.speed=390;}else if("darius".equals(skin)){player.maxHp=120;player.hp=120;player.shield=45;player.speed=345;}}
    private void spawnEnemies(int n){for(int i=0;i<n;i++){Enemy e=new Enemy();e.type=i%6==0?3:(i%3==0?2:1);e.maxHp=e.hp=e.type==3?150:e.type==2?90:55;float a=random.nextFloat()*(float)Math.PI*2,d=850+random.nextFloat()*1500;e.x=clamp(player.x+(float)Math.cos(a)*d,130,WORLD_SIZE-130);e.y=clamp(player.y+(float)Math.sin(a)*d,130,WORLD_SIZE-130);if(world.isBlocked(e.x,e.y,50)){e.x=clamp(e.x+240,130,WORLD_SIZE-130);e.y=clamp(e.y+160,130,WORLD_SIZE-130);}choosePatrol(e);enemies.add(e);}}
    private void spawnStartingLoot(){addPickup(player.x+430,player.y-260,PickupType.AMMO);addPickup(player.x-430,player.y+260,PickupType.GRENADE);addPickup(player.x+220,player.y+440,PickupType.MEDKIT);addPickup(player.x-260,player.y-440,PickupType.SHIELD);}
    private void addPickup(float x,float y,PickupType type){x=clamp(x,100,WORLD_SIZE-100);y=clamp(y,100,WORLD_SIZE-100);if(!world.isBlocked(x,y,30))pickups.add(new Pickup(x,y,type));}

    public void update(float dt,Input in){if(gameOver)return;dt=Math.min(.05f,Math.max(.001f,dt));fireCd=Math.max(0,fireCd-dt);swordCd=Math.max(0,swordCd-dt);grenadeCd=Math.max(0,grenadeCd-dt);reinforceCd-=dt;zoneTimer+=dt;movePlayer(dt,in.moveX,in.moveY);if(in.reload)reload();if(in.sword)melee();if(in.grenade)throwGrenade();if(in.fire)fire();updateProjectiles(dt);updateGrenades(dt);updateExplosions(dt);updateEnemies(dt);collectPickups();updateZone(dt);if(reinforceCd<=0&&aliveCount()<7){spawnEnemies(3);reinforceCd=28;}cleanupDead();}
    private void movePlayer(float dt,float mx,float my){float len=(float)Math.hypot(mx,my);if(len<.05)return;mx/=len;my/=len;float s=player.speed*dt;float nx=clamp(player.x+mx*s,70,WORLD_SIZE-70),ny=clamp(player.y+my*s,70,WORLD_SIZE-70);if(!world.isBlocked(nx,player.y,55))player.x=nx;if(!world.isBlocked(player.x,ny,55))player.y=ny;}

    public Enemy getAutoAimTarget(){Enemy best=null;float bestD=MAX_TARGET_DISTANCE;for(Enemy e:enemies){if(e.dead||e.hp<=0)continue;float d=dist(player.x,player.y,e.x,e.y);if(d<bestD&&world.hasLineOfSight(player.x,player.y,e.x,e.y)){best=e;bestD=d;}}return best;}
    public void fire(){if(gameOver||player.dead||fireCd>0||player.ammo<=0)return;Enemy e=getAutoAimTarget();if(e==null)return;float dx=e.x-player.x,dy=e.y-player.y,d=Math.max(1,dist(0,0,dx,dy));projectiles.add(new Projectile(player.x+dx/d*60,player.y+dy/d*60,dx/d*1350,dy/d*1350,30,2.4f,true));player.ammo--;fireCd=.16f;}
    public void reload(){if(player.ammo>=12||player.reserveAmmo<=0||player.dead)return;int n=Math.min(12-player.ammo,player.reserveAmmo);player.ammo+=n;player.reserveAmmo-=n;}
    public void melee(){if(gameOver||player.dead||swordCd>0)return;Enemy t=getAutoAimTarget();if(t==null||dist(player.x,player.y,t.x,t.y)>190){swordCd=.25f;return;}float dx=t.x-player.x,dy=t.y-player.y,d=Math.max(1,dist(0,0,dx,dy));for(Enemy e:enemies){if(e.dead||e.hp<=0)continue;float ex=e.x-player.x,ey=e.y-player.y,ed=Math.max(1,dist(0,0,ex,ey));float dot=(ex*dx+ey*dy)/(ed*d);if(ed<210&&dot>.12)damageEnemy(e,70);}swordCd=.55f;}
    public void throwGrenade(){if(gameOver||player.dead||grenadeCd>0||player.grenades<=0)return;Enemy t=getAutoAimTarget();if(t==null)return;float dx=t.x-player.x,dy=t.y-player.y,d=Math.max(1,dist(0,0,dx,dy));grenades.add(new Grenade(player.x,player.y,dx/d*780,dy/d*780,.65f));player.grenades--;grenadeCd=.9f;}

    private void updateProjectiles(float dt){for(Iterator<Projectile>it=projectiles.iterator();it.hasNext();){Projectile p=it.next();float ox=p.x,oy=p.y;p.x+=p.vx*dt;p.y+=p.vy*dt;p.life-=dt;if(p.life<=0||p.x<0||p.y<0||p.x>WORLD_SIZE||p.y>WORLD_SIZE||world.isBlocked(p.x,p.y,4)){it.remove();continue;}if(p.fromPlayer){Enemy hit=null;for(Enemy e:enemies)if(!e.dead&&segDist(e.x,e.y,ox,oy,p.x,p.y)<48){hit=e;break;}if(hit!=null){damageEnemy(hit,p.damage);it.remove();}}else if(segDist(player.x,player.y,ox,oy,p.x,p.y)<42){damagePlayer(p.damage);it.remove();}}}
    private void updateGrenades(float dt){for(Iterator<Grenade>it=grenades.iterator();it.hasNext();){Grenade g=it.next();g.x+=g.vx*dt;g.y+=g.vy*dt;g.life-=dt;if(g.life<=0){explode(g.x,g.y);it.remove();}}}
    private void explode(float x,float y){explosions.add(new Explosion(x,y,260));for(Enemy e:enemies){if(e.dead)continue;float d=dist(x,y,e.x,e.y);if(d<260)damageEnemy(e,d<120?110:60);}}
    private void updateExplosions(float dt){for(Iterator<Explosion>it=explosions.iterator();it.hasNext();){Explosion e=it.next();e.life-=dt;if(e.life<=0)it.remove();}}
    private void damageEnemy(Enemy e,float amount){e.hp-=(int)Math.round(amount);if(e.hp<=0&&!e.dead){e.hp=0;e.dead=true;e.state=EnemyState.DEAD;e.deathTimer=.9f;kills++;player.score+=e.type==3?50:e.type==2?25:10;if(random.nextFloat()<.42){float r=random.nextFloat();addPickup(e.x,e.y,r<.48?PickupType.AMMO:r<.7?PickupType.MEDKIT:r<.9?PickupType.GRENADE:PickupType.SHIELD);}}}
    private void damagePlayer(float amount){damageCarry+=amount;int dmg=(int)damageCarry;damageCarry-=dmg;if(dmg<=0)return;int shielded=Math.min(player.shield,dmg);player.shield-=shielded;dmg-=shielded;if(dmg>0)player.hp=Math.max(0,player.hp-dmg);if(player.hp<=0){player.dead=true;gameOver=true;}}

    private void updateEnemies(float dt){for(Enemy e:enemies){if(e.dead){e.deathTimer-=dt;continue;}e.attackCooldown=Math.max(0,e.attackCooldown-dt);float d=dist(player.x,player.y,e.x,e.y);boolean los=d<1600&&world.hasLineOfSight(e.x,e.y,player.x,player.y);if(d<1600&&los)e.state=d<190?EnemyState.ATTACK:EnemyState.CHASE;else e.state=EnemyState.PATROL;switch(e.state){case PATROL:patrol(e,dt);break;case CHASE:chase(e,dt);break;case ATTACK:attack(e);break;default:break;}}}
    private void patrol(Enemy e,float dt){float dx=e.patrolX-e.x,dy=e.patrolY-e.y,d=dist(0,0,dx,dy);if(d<45){e.patrolWait=Math.max(0,e.patrolWait-dt);if(e.patrolWait<=0)choosePatrol(e);return;}moveEnemy(e,dx/d*70*dt,dy/d*70*dt);}
    private void choosePatrol(Enemy e){for(int i=0;i<10;i++){float x=clamp(e.x+(random.nextFloat()-.5f)*900,110,WORLD_SIZE-110),y=clamp(e.y+(random.nextFloat()-.5f)*900,110,WORLD_SIZE-110);if(!world.isBlocked(x,y,45)){e.patrolX=x;e.patrolY=y;e.patrolWait=.2f+random.nextFloat();return;}}e.patrolX=e.x;e.patrolY=e.y;e.patrolWait=.5f;}
    private void chase(Enemy e,float dt){float dx=player.x-e.x,dy=player.y-e.y,d=Math.max(1,dist(0,0,dx,dy)),s=e.type==3?120:e.type==2?98:82;moveEnemy(e,dx/d*s*dt,dy/d*s*dt);}
    private void attack(Enemy e){float d=dist(player.x,player.y,e.x,e.y);if(d<135){if(e.attackCooldown<=0){damagePlayer(e.type==3?15:e.type==2?9:6);e.attackCooldown=.75f;}}else if(e.attackCooldown<=0&&d<1000){float dx=player.x-e.x,dy=player.y-e.y,len=Math.max(1,dist(0,0,dx,dy));projectiles.add(new Projectile(e.x+dx/len*45,e.y+dy/len*45,dx/len*620,dy/len*620,e.type==3?14:8,2,false));e.attackCooldown=e.type==3?1.15f:e.type==2?1.45f:1.8f;}}
    private void moveEnemy(Enemy e,float dx,float dy){float nx=clamp(e.x+dx,70,WORLD_SIZE-70),ny=clamp(e.y+dy,70,WORLD_SIZE-70);if(!world.isBlocked(nx,e.y,45))e.x=nx;if(!world.isBlocked(e.x,ny,45))e.y=ny;}
    private void collectPickups(){for(Iterator<Pickup>it=pickups.iterator();it.hasNext();){Pickup p=it.next();if(dist(player.x,player.y,p.x,p.y)>85)continue;switch(p.type){case AMMO:player.reserveAmmo=Math.min(180,player.reserveAmmo+36);break;case MEDKIT:player.hp=Math.min(player.maxHp,player.hp+35);break;case GRENADE:player.grenades=Math.min(9,player.grenades+1);break;case SHIELD:player.shield=Math.min(100,player.shield+30);break;}it.remove();}}
    private void updateZone(float dt){if(zoneTimer>20&&zoneRadius>950)zoneRadius=Math.max(950,zoneRadius-dt*5.1f);if(dist(player.x,player.y,WORLD_SIZE*.5f,WORLD_SIZE*.5f)>zoneRadius)damagePlayer(9*dt);}
    private void cleanupDead(){for(Iterator<Enemy>it=enemies.iterator();it.hasNext();){Enemy e=it.next();if(e.dead&&e.deathTimer<=0)it.remove();}}
    private int aliveCount(){int n=0;for(Enemy e:enemies)if(!e.dead&&e.hp>0)n++;return n;}
    private void spawnReinforcements(int n){spawnEnemies(n);}
    private static float dist(float x1,float y1,float x2,float y2){return(float)Math.hypot(x1-x2,y1-y2);}private static float segDist(float px,float py,float x1,float y1,float x2,float y2){float dx=x2-x1,dy=y2-y1;if(dx==0&&dy==0)return dist(px,py,x1,y1);float t=((px-x1)*dx+(py-y1)*dy)/(dx*dx+dy*dy);t=Math.max(0,Math.min(1,t));return dist(px,py,x1+t*dx,y1+t*dy);}private static float clamp(float v,float lo,float hi){return Math.max(lo,Math.min(hi,v));}

    public Player player(){return player;}public List<Enemy> enemies(){return enemies;}public List<Projectile> projectiles(){return projectiles;}public List<Grenade> grenades(){return grenades;}public List<Pickup> pickups(){return pickups;}public List<Explosion> explosions(){return explosions;}public float zoneRadius(){return zoneRadius;}public int kills(){return kills;}public boolean gameOver(){return gameOver;}public WorldMap world(){return world;}
}
