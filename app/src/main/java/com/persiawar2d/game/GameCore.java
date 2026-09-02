package com.persiawar2d.game;

import com.persiawar2d.world.WorldMap;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import java.util.Random;

/**
 * Single authoritative gameplay simulation. No rendering/UI code lives here.
 * This is the core that can later be driven by local input or a multiplayer snapshot.
 */
public final class GameCore {
    public static final float WORLD_SIZE = WorldMap.SIZE;
    public static final float MAX_TARGET_DISTANCE = 1800f;

    public enum Weapon { GUN, SWORD }
    public enum PickupType { AMMO, MEDKIT, GRENADE, SHIELD }
    public enum EnemyState { PATROL, ALERT, CHASE, ATTACK, DEAD }

    public static final class Input {
        public float moveX, moveY;
        public boolean fire, sword, grenade, reload;
    }

    public static final class Player {
        public float x, y;
        public int hp, maxHp, shield, ammo, reserveAmmo, grenades;
        public float speed;
        public String skin;
        public Weapon weapon = Weapon.GUN;
        public boolean dead;
        public int score;
    }

    public static final class Enemy {
        public float x, y, patrolX, patrolY;
        public int hp, maxHp, type;
        public EnemyState state = EnemyState.PATROL;
        public float patrolWait;
        public float attackCooldown;
        public float deathTimer;
        public boolean dead;
    }

    public static final class Projectile {
        public float x, y, vx, vy, damage, life;
        public boolean fromPlayer;
        public Projectile(float x,float y,float vx,float vy,float damage,float life,boolean fromPlayer){
            this.x=x;this.y=y;this.vx=vx;this.vy=vy;this.damage=damage;this.life=life;this.fromPlayer=fromPlayer;
        }
    }

    public static final class Grenade {
        public float x,y,vx,vy,life;
        public Grenade(float x,float y,float vx,float vy,float life){this.x=x;this.y=y;this.vx=vx;this.vy=vy;this.life=life;}
    }

    public static final class Pickup {
        public float x,y;
        public PickupType type;
        public Pickup(float x,float y,PickupType type){this.x=x;this.y=y;this.type=type;}
    }

    public static final class Explosion {
        public float x,y,life,radius;
        public Explosion(float x,float y,float radius,float life){this.x=x;this.y=y;this.radius=radius;this.life=life;}
    }

    private final WorldMap world;
    private final Random random = new Random(20260902L);
    private final ArrayList<Enemy> enemies = new ArrayList<>();
    private final ArrayList<Projectile> projectiles = new ArrayList<>();
    private final ArrayList<Grenade> grenades = new ArrayList<>();
    private final ArrayList<Pickup> pickups = new ArrayList<>();
    private final ArrayList<Explosion> explosions = new ArrayList<>();
    private final Player player = new Player();
    private String selectedSkin = "classic";
    private float fireCooldown;
    private float swordCooldown;
    private float reinforcementTimer;
    private float elapsed;
    private float zoneRadius = 2450f;
    private float zoneTargetRadius = 950f;
    private float zoneTimer;
    private int kills;
    private boolean gameOver;

    public GameCore(WorldMap world, String skin){this.world=world;this.selectedSkin=skin==null?"classic":skin;reset();}

    public void reset(){
        enemies.clear();projectiles.clear();grenades.clear();pickups.clear();explosions.clear();
        fireCooldown=0;swordCooldown=0;reinforcementTimer=20f;elapsed=0;zoneRadius=2450f;zoneTargetRadius=950f;zoneTimer=0;kills=0;gameOver=false;
        applyCharacterProfile();
        player.x=WorldMap.SIZE*.5f; player.y=WorldMap.SIZE*.5f; player.dead=false; player.score=0; player.weapon=Weapon.GUN;
        spawnInitialEnemies(14); spawnStartingLoot();
    }

    private void applyCharacterProfile(){
        player.skin=selectedSkin;player.maxHp=100;player.hp=100;player.shield=20;player.ammo=12;player.reserveAmmo=90;player.grenades=3;player.speed=360f;
        if("blue".equals(selectedSkin)){player.maxHp=105;player.hp=105;player.shield=35;player.speed=335f;}
        else if("red".equals(selectedSkin)){player.maxHp=95;player.hp=95;player.shield=10;player.speed=390f;}
        else if("darius".equals(selectedSkin)){player.maxHp=120;player.hp=120;player.shield=45;player.speed=345f;}
    }

    private void spawnInitialEnemies(int count){
        for(int i=0;i<count;i++){
            Enemy e=new Enemy();e.type=(i%6==0)?3:((i%3==0)?2:1);e.maxHp=e.hp=e.type==3?150:e.type==2?90:55;
            float a=random.nextFloat()*(float)Math.PI*2f;float d=700+random.nextFloat()*1500f;
            e.x=clamp(player.x+(float)Math.cos(a)*d,150,WorldMap.SIZE-150);e.y=clamp(player.y+(float)Math.sin(a)*d,150,WorldMap.SIZE-150);
            if(world.isBlocked(e.x,e.y,55)){e.x=clamp(e.x+250,150,WorldMap.SIZE-150);e.y=clamp(e.y+180,150,WorldMap.SIZE-150);}
            choosePatrolTarget(e);enemies.add(e);
        }
    }

    private void spawnStartingLoot(){
        addSafePickup(player.x+420,player.y-260,PickupType.AMMO);
        addSafePickup(player.x-420,player.y+260,PickupType.GRENADE);
        addSafePickup(player.x+230,player.y+440,PickupType.MEDKIT);
        addSafePickup(player.x-260,player.y-440,PickupType.SHIELD);
    }

    private void addSafePickup(float x,float y,PickupType type){x=clamp(x,100,WorldMap.SIZE-100);y=clamp(y,100,WorldMap.SIZE-100);if(!world.isBlocked(x,y,35))pickups.add(new Pickup(x,y,type));}

    public void update(float dt, Input input){
        if(gameOver) return;
        dt=Math.min(.05f,Math.max(.001f,dt));elapsed+=dt;fireCooldown=Math.max(0,fireCooldown-dt);swordCooldown=Math.max(0,swordCooldown-dt);zoneTimer+=dt;reinforcementTimer-=dt;
        movePlayer(dt,input.moveX,input.moveY);
        if(input.reload) reload();
        if(input.sword) melee();
        if(input.grenade) throwGrenade();
        if(input.fire) fire();
        updateProjectiles(dt);updateGrenades(dt);updateExplosions(dt);updateEnemies(dt);collectPickups();updateZone(dt);
        if(reinforcementTimer<=0 && enemies.size()-countDead()<6){spawnReinforcements(3);reinforcementTimer=28f;}
        cleanupDead();
    }

    private void movePlayer(float dt,float mx,float my){
        float len=(float)Math.hypot(mx,my);if(len<.05f)return;mx/=len;my/=len;float step=player.speed*dt;
        float nx=clamp(player.x+mx*step,70,WorldMap.SIZE-70);float ny=clamp(player.y+my*step,70,WorldMap.SIZE-70);
        if(!world.isBlocked(nx,player.y,55))player.x=nx;if(!world.isBlocked(player.x,ny,55))player.y=ny;
    }

    public Enemy getAutoAimTarget(){
        Enemy best=null;float bestD=MAX_TARGET_DISTANCE;
        for(Enemy e:enemies){if(e.dead||e.hp<=0)continue;float d=distance(player.x,player.y,e.x,e.y);if(d>bestD)continue;if(world.hasLineOfSight(player.x,player.y,e.x,e.y)){best=e;bestD=d;}}
        return best;
    }

    private void fire(){
        if(player.weapon!=Weapon.GUN||fireCooldown>0||player.ammo<=0||player.dead)return;
        Enemy target=getAutoAimTarget();if(target==null){return;}
        float dx=target.x-player.x,dy=target.y-player.y,d=Math.max(1,distance(0,0,dx,dy));
        projectiles.add(new Projectile(player.x+dx/d*60,player.y+dy/d*60,dx/d*1350,dy/d*1350,30,2.4f,true));
        player.ammo--;fireCooldown=.16f;
    }

    private void reload(){if(player.ammo>=12||player.reserveAmmo<=0||player.dead)return;int n=Math.min(12-player.ammo,player.reserveAmmo);player.ammo+=n;player.reserveAmmo-=n;}

    private void melee(){
        if(player.dead||swordCooldown>0)return;player.weapon=Weapon.SWORD;Enemy target=getAutoAimTarget();if(target==null||distance(player.x,player.y,target.x,target.y)>190){swordCooldown=.25f;return;}
        float dx=target.x-player.x,dy=target.y-player.y,d=Math.max(1,distance(0,0,dx,dy));
        for(Enemy e:enemies){if(e.dead||e.hp<=0)continue;float ex=e.x-player.x,ey=e.y-player.y,ed=Math.max(1,distance(0,0,ex,ey));float dot=(ex*dx+ey*dy)/(ed*d);if(ed<205&&dot>.15f)damageEnemy(e,70);}
        swordCooldown=.55f;
    }

    private void throwGrenade(){
        if(player.dead||player.grenades<=0)return;Enemy target=getAutoAimTarget();if(target==null)return;float dx=target.x-player.x,dy=target.y-player.y,d=Math.max(1,distance(0,0,dx,dy));
        grenades.add(new Grenade(player.x,player.y,dx/d*780f,dy/d*780f,.65f));player.grenades--;
    }

    private void updateProjectiles(float dt){
        for(Iterator<Projectile> it=projectiles.iterator();it.hasNext();){Projectile p=it.next();float ox=p.x,oy=p.y;p.x+=p.vx*dt;p.y+=p.vy*dt;p.life-=dt;
            if(p.life<=0||p.x<0||p.y<0||p.x>WorldMap.SIZE||p.y>WorldMap.SIZE||world.isBlocked(p.x,p.y,4)){it.remove();continue;}
            if(p.fromPlayer){Enemy hit=null;for(Enemy e:enemies)if(!e.dead&&segmentDistance(e.x,e.y,ox,oy,p.x,p.y)<48){hit=e;break;}if(hit!=null){damageEnemy(hit,p.damage);it.remove();}}
            else if(segmentDistance(player.x,player.y,ox,oy,p.x,p.y)<42){damagePlayer(p.damage);it.remove();}
        }
    }

    private void updateGrenades(float dt){
        for(Iterator<Grenade> it=grenades.iterator();it.hasNext();){Grenade g=it.next();g.x+=g.vx*dt;g.y+=g.vy*dt;g.life-=dt;if(g.life<=0){explode(g.x,g.y);it.remove();}}
    }

    private void explode(float x,float y){
        explosions.add(new Explosion(x,y,260,.38f));
        for(Enemy e:enemies){if(e.dead||e.hp<=0)continue;float d=distance(x,y,e.x,e.y);if(d<260)damageEnemy(e,d<120?110:60);}
    }

    private void updateExplosions(float dt){for(Iterator<Explosion> it=explosions.iterator();it.hasNext();){Explosion e=it.next();e.life-=dt;if(e.life<=0)it.remove();}}

    private void damageEnemy(Enemy e,float amount){if(e.dead)return;e.hp-=(int)Math.round(amount);if(e.hp<=0){e.hp=0;e.dead=true;e.state=EnemyState.DEAD;e.deathTimer=.9f;kills++;player.score+=e.type==3?50:e.type==2?25:10;if(random.nextFloat()<.38f){PickupType t=random.nextFloat()<.45f?PickupType.AMMO:(random.nextFloat()<.5f?PickupType.MEDKIT:PickupType.GRENADE);addSafePickup(e.x,e.y,t);}}}

    private void damagePlayer(float amount){
        int dmg=(int)Math.ceil(amount);int blocked=Math.min(player.shield,dmg);player.shield-=blocked;dmg-=blocked;if(dmg>0){player.hp=Math.max(0,player.hp-dmg);}if(player.hp<=0){player.dead=true;gameOver=true;}
    }

    private void updateEnemies(float dt){
        for(Enemy e:enemies){if(e.dead){e.deathTimer-=dt;continue;}e.attackCooldown=Math.max(0,e.attackCooldown-dt);float d=distance(player.x,player.y,e.x,e.y);boolean los=d<1600&&world.hasLineOfSight(e.x,e.y,player.x,player.y);
            if(d<1600&&los){e.state=d<190?EnemyState.ATTACK:EnemyState.CHASE;}
            else e.state=EnemyState.PATROL;
            if(e.state==EnemyState.PATROL)patrolEnemy(e,dt);else if(e.state==EnemyState.CHASE)chaseEnemy(e,dt);else if(e.state==EnemyState.ATTACK)attackEnemy(e);
        }
    }

    private void patrolEnemy(Enemy e,float dt){float dx=e.patrolX-e.x,dy=e.patrolY-e.y,d=distance(0,0,dx,dy);if(d<45||e.patrolWait>0){e.patrolWait=Math.max(0,e.patrolWait-dt);if(e.patrolWait<=0)choosePatrolTarget(e);return;}moveEnemy(e,dx/d*70f*dt,dy/d*70f*dt);}
    private void choosePatrolTarget(Enemy e){for(int tries=0;tries<12;tries++){float x=clamp(e.x+(random.nextFloat()-.5f)*900,120,WorldMap.SIZE-120),y=clamp(e.y+(random.nextFloat()-.5f)*900,120,WorldMap.SIZE-120);if(!world.isBlocked(x,y,45)){e.patrolX=x;e.patrolY=y;e.patrolWait=.2f+random.nextFloat()*1.3f;return;}}e.patrolX=e.x;e.patrolY=e.y;e.patrolWait=.5f;}
    private void chaseEnemy(Enemy e,float dt){float dx=player.x-e.x,dy=player.y-e.y,d=Math.max(1,distance(0,0,dx,dy));moveEnemy(e,dx/d*(e.type==3?120:e.type==2?98:82)*dt,dy/d*(e.type==3?120:e.type==2?98:82)*dt);}
    private void attackEnemy(Enemy e){float d=distance(player.x,player.y,e.x,e.y);if(d<135){if(e.attackCooldown<=0){damagePlayer(e.type==3?15:e.type==2?9:6);e.attackCooldown=.75f;}}else if(e.attackCooldown<=0&&d<1000){float dx=player.x-e.x,dy=player.y-e.y,len=Math.max(1,distance(0,0,dx,dy));projectiles.add(new Projectile(e.x+dx/len*45,e.y+dy/len*45,dx/len*620,dy/len*620,e.type==3?14:8,2f,false));e.attackCooldown=e.type==3?1.15f:e.type==2?1.45f:1.8f;}}
    private void moveEnemy(Enemy e,float dx,float dy){float nx=clamp(e.x+dx,70,WorldMap.SIZE-70),ny=clamp(e.y+dy,70,WorldMap.SIZE-70);if(!world.isBlocked(nx,e.y,45))e.x=nx;if(!world.isBlocked(e.x,ny,45))e.y=ny;}

    private void collectPickups(){for(Iterator<Pickup> it=pickups.iterator();it.hasNext();){Pickup p=it.next();if(distance(player.x,player.y,p.x,p.y)>80)continue;switch(p.type){case AMMO:player.reserveAmmo=Math.min(180,player.reserveAmmo+36);break;case MEDKIT:player.hp=Math.min(player.maxHp,player.hp+35);break;case GRENADE:player.grenades=Math.min(9,player.grenades+1);break;case SHIELD:player.shield=Math.min(100,player.shield+30);break;}it.remove();}}

    private void updateZone(float dt){
        if(zoneTimer>20f&&zoneRadius>zoneTargetRadius){zoneRadius=Math.max(zoneTargetRadius,zoneRadius-dt*5.1f);}
        if(distance(player.x,player.y,WORLD_SIZE*.5f,WORLD_SIZE*.5f)>zoneRadius){damagePlayer(9f*dt);}
    }

    private void spawnReinforcements(int count){for(int i=0;i<count;i++){Enemy e=new Enemy();e.type=1+random.nextInt(3);e.maxHp=e.hp=e.type==3?150:e.type==2?90:55;float a=random.nextFloat()*(float)Math.PI*2f;float d=1200+random.nextFloat()*700f;e.x=clamp(player.x+(float)Math.cos(a)*d,100,WorldMap.SIZE-100);e.y=clamp(player.y+(float)Math.sin(a)*d,100,WorldMap.SIZE-100);if(!world.isBlocked(e.x,e.y,50)){choosePatrolTarget(e);enemies.add(e);}}}
    private int countDead(){int n=0;for(Enemy e:enemies)if(e.dead)n++;return n;}
    private void cleanupDead(){for(Iterator<Enemy> it=enemies.iterator();it.hasNext();){Enemy e=it.next();if(e.dead&&e.deathTimer<=0)it.remove();}}

    private static float distance(float x1,float y1,float x2,float y2){return(float)Math.hypot(x1-x2,y1-y2);}
    private static float segmentDistance(float px,float py,float x1,float y1,float x2,float y2){float dx=x2-x1,dy=y2-y1;if(dx==0&&dy==0)return distance(px,py,x1,y1);float t=((px-x1)*dx+(py-y1)*dy)/(dx*dx+dy*dy);t=Math.max(0,Math.min(1,t));return distance(px,py,x1+t*dx,y1+t*dy);}
    private static float clamp(float v,float lo,float hi){return Math.max(lo,Math.min(hi,v));}

    public Player player(){return player;}
    public List<Enemy> enemies(){return enemies;}
    public List<Projectile> projectiles(){return projectiles;}
    public List<Grenade> grenades(){return grenades;}
    public List<Pickup> pickups(){return pickups;}
    public List<Explosion> explosions(){return explosions;}
    public float zoneRadius(){return zoneRadius;}
    public int kills(){return kills;}
    public boolean gameOver(){return gameOver;}
    public Enemy target(){return getAutoAimTarget();}
}
