package com.persiawar2d;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.opengl.GLES20;
import android.opengl.GLSurfaceView;
import android.opengl.Matrix;
import android.view.MotionEvent;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.FloatBuffer;
import java.util.ArrayList;
import java.util.Random;
import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

/**
 * Stable 2.5D city battle renderer.
 * The map is procedural so it cannot fall back to a broken reference texture or
 * incomplete modular building pieces.
 */
public final class AdvancedThreeDRenderer implements GLSurfaceView.Renderer {
    private final Random random = new Random(20260818L);
    private final ArrayList<Building> buildings = new ArrayList<>();
    private final ArrayList<Tree> trees = new ArrayList<>();
    private final ArrayList<Enemy> enemies = new ArrayList<>();
    private final ArrayList<Pickup> pickups = new ArrayList<>();
    private final ArrayList<Projectile> projectiles = new ArrayList<>();
    private final float[] proj = new float[16], view = new float[16], vp = new float[16];
    private final float[] model = new float[16], mvp = new float[16];

    private FloatBuffer cube;
    private int colorProgram, aPos, uColor, uMatrix;

    private float px = 0f, pz = 0f;
    private float yaw = 0.78f;
    private float moveX, moveY;
    private float lastX, lastY;
    private boolean moving, aiming, gameOver;
    private long lastNanos;
    private float fireCooldown;
    private float playerVX, playerVZ;
    private float animationTime;
    private float muzzleFlash;
    private float weaponKick;

    private int hp = 100, ammo = 30, reserve = 120, kills = 0, grenades = 3, wave = 1;
    private long nextWaveAt;
    private int width = 1, height = 1;

    private final ArrayList<Road> roads = new ArrayList<>();

    public AdvancedThreeDRenderer(Context context) {
        buildCity();
        spawnEnemies();
        spawnPickups();
    }

    private void buildCity() {
        buildings.clear();
        trees.clear();
        roads.clear();

        // Four connected arterial roads. Every crossing is physically overlapping.
        float[] main = {-27f, -9f, 9f, 27f};
        for (float z : main) addRoadBlocks(z, true);
        for (float x : main) addRoadBlocks(x, false);

        // Buildings occupy the blocks between roads, leaving clear walkable corridors.
        float[] centers = {-18f, 0f, 18f};
        int style = 0;
        for (float z : centers) {
            for (float x : centers) {
                if (Math.abs(x) < 1 && Math.abs(z) < 1) continue;
                addBuilding(x - 4.2f, z - 4.0f, 6.8f, 6.0f, 4.0f + (style % 3), style++);
                addBuilding(x + 4.0f, z + 3.5f, 5.2f, 5.0f, 3.0f + (style % 2), style++);
            }
        }

        // Two parks / green pockets.
        addPark(-18f, 18f);
        addPark(18f, -18f);

        // Perimeter buildings, while keeping the road exits open.
        addBuilding(-32f, -18f, 5.0f, 7.0f, 3.5f, style++);
        addBuilding(32f, 18f, 5.0f, 7.0f, 4.5f, style++);
        addBuilding(-32f, 18f, 5.0f, 7.0f, 3.5f, style++);
        addBuilding(32f, -18f, 5.0f, 7.0f, 4.0f, style++);

        for (int i = 0; i < 24; i++) {
            float x = -34f + random.nextFloat() * 68f;
            float z = -34f + random.nextFloat() * 68f;
            if (isOnRoad(x, z, 1.5f) || blocked(x, z, 1.0f)) continue;
            trees.add(new Tree(x, z, 0.55f + random.nextFloat() * 0.35f));
        }
    }

    private void addRoadBlocks(float line, boolean horizontal) {
        if (horizontal) {
            addRoad(-34f, line, 34f, line, 2.15f);
        } else {
            addRoad(line, -34f, line, 34f, 2.15f);
        }
    }

    private void addRoad(float x1, float z1, float x2, float z2, float width) {
        roads.add(new Road(x1, z1, x2, z2, width));
    }

    private void addBuilding(float x, float z, float w, float d, float h, int style) {
        buildings.add(new Building(x, z, w, d, h, style));
    }

    private void addPark(float cx, float cz) {
        for (int i = 0; i < 8; i++) {
            double a = i * Math.PI * 2.0 / 8.0;
            trees.add(new Tree(cx + (float)Math.cos(a) * 4.2f,
                    cz + (float)Math.sin(a) * 4.2f, 0.8f));
        }
    }

    private boolean isOnRoad(float x, float z, float pad) {
        for (Road r : roads) {
            if (Math.abs(r.z1 - r.z2) < 0.01f) {
                if (x >= Math.min(r.x1, r.x2) - pad && x <= Math.max(r.x1, r.x2) + pad
                        && Math.abs(z - r.z1) <= r.width * .5f + pad) return true;
            } else {
                if (z >= Math.min(r.z1, r.z2) - pad && z <= Math.max(r.z1, r.z2) + pad
                        && Math.abs(x - r.x1) <= r.width * .5f + pad) return true;
            }
        }
        return false;
    }

    private boolean blocked(float x, float z, float r) {
        if (x < -34.5f || x > 34.5f || z < -34.5f || z > 34.5f) return true;
        for (Building b : buildings) {
            if (x > b.x - b.w * .5f - r && x < b.x + b.w * .5f + r
                    && z > b.z - b.d * .5f - r && z < b.z + b.d * .5f + r) return true;
        }
        return false;
    }

    private void spawnEnemies() {
        enemies.clear();
        float[][] spots = {{-25,-25},{0,-25},{25,-25},{-25,0},{25,0},{-25,25},{0,25},{25,25}};
        for (float[] s : spots) enemies.add(new Enemy(s[0], s[1]));
    }

    private void spawnPickups() {
        pickups.clear();
        pickups.add(new Pickup(-13f, -13f, Pickup.AMMO));
        pickups.add(new Pickup(13f, 13f, Pickup.AMMO));
        pickups.add(new Pickup(-13f, 13f, Pickup.GRENADE));
        pickups.add(new Pickup(13f, -13f, Pickup.MEDKIT));
    }

    @Override public void onSurfaceCreated(GL10 gl, EGLConfig config) {
        GLES20.glClearColor(0.055f, 0.075f, 0.065f, 1f);
        GLES20.glEnable(GLES20.GL_DEPTH_TEST);
        GLES20.glDisable(GLES20.GL_CULL_FACE);

        colorProgram = program(
                "attribute vec3 A; uniform mat4 M; void main(){gl_Position=M*vec4(A,1.0);}",
                "precision mediump float; uniform vec4 C; void main(){gl_FragColor=C;}"
        );
        aPos = GLES20.glGetAttribLocation(colorProgram, "A");
        uColor = GLES20.glGetUniformLocation(colorProgram, "C");
        uMatrix = GLES20.glGetUniformLocation(colorProgram, "M");
        cube = buf(makeCube());
        lastNanos = System.nanoTime();
    }

    @Override public void onSurfaceChanged(GL10 gl, int w, int h) {
        width = Math.max(1, w);
        height = Math.max(1, h);
        GLES20.glViewport(0, 0, width, height);
        Matrix.perspectiveM(proj, 0, 52f, (float)width / height, .05f, 180f);
    }

    @Override public void onDrawFrame(GL10 gl) {
        long now = System.nanoTime();
        float dt = Math.min(.033f, Math.max(.001f, (now - lastNanos) / 1_000_000_000f));
        lastNanos = now;
        if (gameOver) return;
        fireCooldown = Math.max(0, fireCooldown - dt);
        muzzleFlash = Math.max(0, muzzleFlash - dt);
        weaponKick = Math.max(0, weaponKick - dt * 5f);

        movePlayer(dt);
        animationTime += dt;
        updateEnemies(dt);
        updateProjectiles(dt);
        collectPickups();
        long nowMs = System.currentTimeMillis();
        if (livingEnemyCount() == 0) {
            if (nextWaveAt == 0) nextWaveAt = nowMs + 1400;
            else if (nowMs >= nextWaveAt) { wave++; spawnEnemies(); nextWaveAt = 0; }
        } else {
            nextWaveAt = 0;
        }

        float distance = 15.5f;
        float camX = px - (float)Math.sin(yaw) * distance;
        float camZ = pz + (float)Math.cos(yaw) * distance;
        Matrix.setLookAtM(view, 0, camX, 12.5f, camZ, px, 0, pz, 0, 1, 0);
        Matrix.multiplyMM(vp, 0, proj, 0, view, 0);

        GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT | GLES20.GL_DEPTH_BUFFER_BIT);

        // Ground.
        box(0, -.25f, 0, 70f, .5f, 70f, .20f, .31f, .23f);

        // Connected road grid and intersections.
        for (Road r : roads) drawRoad(r);

        // Depth-tested 3D buildings and vegetation.
        for (Building b : buildings) drawBuilding(b);
        for (Tree t : trees) drawTree(t);
        for (Pickup p : pickups) drawPickup(p);
        for (Projectile p : projectiles) drawProjectile(p);
        for (Enemy e : enemies) if (e.hp > 0) drawEnemy(e);
        drawPlayer();
    }

    private void drawRoad(Road r) {
        boolean horizontal = Math.abs(r.z1 - r.z2) < .01f;
        float len = horizontal ? Math.abs(r.x2 - r.x1) : Math.abs(r.z2 - r.z1);
        float cx = (r.x1 + r.x2) * .5f, cz = (r.z1 + r.z2) * .5f;
        if (horizontal) {
            box(cx, .02f, cz, len, .06f, r.width, .10f, .11f, .10f);
            box(cx, .055f, cz - r.width*.43f, len, .03f, .13f, .34f, .35f, .30f);
            box(cx, .055f, cz + r.width*.43f, len, .03f, .13f, .34f, .35f, .30f);
            for(float x=r.x1+1.7f;x<r.x2-1.0f;x+=3.8f) {
                box(x, .075f, cz, 2.0f, .025f, .09f, .74f, .68f, .38f);
            }
            for(float x=r.x1+1.0f;x<r.x2-1.0f;x+=9.0f) {
                box(x, .075f, cz-r.width*.49f, 4.0f, .018f, .08f, .55f, .56f, .50f);
                box(x, .075f, cz+r.width*.49f, 4.0f, .018f, .08f, .55f, .56f, .50f);
            }
        } else {
            box(cx, .02f, cz, r.width, .06f, len, .10f, .11f, .10f);
            box(cx - r.width*.43f, .055f, cz, .13f, .03f, len, .34f, .35f, .30f);
            box(cx + r.width*.43f, .055f, cz, .13f, .03f, len, .34f, .35f, .30f);
            for(float z=r.z1+1.7f;z<r.z2-1.0f;z+=3.8f) {
                box(cx, .075f, z, .09f, .025f, 2.0f, .74f, .68f, .38f);
            }
            for(float z=r.z1+1.0f;z<r.z2-1.0f;z+=9.0f) {
                box(cx-r.width*.49f, .075f, z, .08f, .018f, 4.0f, .55f, .56f, .50f);
                box(cx+r.width*.49f, .075f, z, .08f, .018f, 4.0f, .55f, .56f, .50f);
            }
        }
    }

    private void drawBuilding(Building b) {
        float bodyR = .30f + (b.style % 3) * .045f;
        float bodyG = .25f + (b.style % 4) * .025f;
        float bodyB = .19f + (b.style % 2) * .035f;
        box(b.x, b.h*.5f, b.z, b.w, b.h, b.d, bodyR, bodyG, bodyB);

        // Roof slab gives a clear 2.5D silhouette instead of flat brown rectangles.
        box(b.x, b.h + .12f, b.z, b.w + .18f, .22f, b.d + .18f,
                .16f, .13f, .10f);
        box(b.x, b.h + .25f, b.z, Math.min(b.w*.42f,1.9f), .16f, Math.min(b.d*.42f,1.7f),
                .22f, .18f, .13f);

        // Lit facade panels/windows.
        float frontZ = b.z - b.d*.5f - .012f;
        int rows = Math.max(1, (int)(b.h / 1.25f));
        int cols = Math.max(1, (int)(b.w / 1.25f));
        for (int row = 0; row < rows; row++) {
            for (int col = 0; col < cols; col++) {
                float wx = b.x - b.w*.5f + .65f + col * 1.25f;
                float wy = .65f + row * 1.25f;
                if (wx > b.x + b.w*.5f - .35f || wy > b.h - .35f) continue;
                box(wx, wy, frontZ, .34f, .42f, .025f, .65f, .60f, .34f);
            }
        }
    }

    private void drawTree(Tree t) {
        box(t.x, .75f, t.z, .22f, 1.5f, .22f, .24f, .15f, .08f);
        box(t.x, 1.65f, t.z, 1.35f*t.r, 1.55f*t.r, 1.35f*t.r,
                .10f, .32f, .13f);
        box(t.x-.35f*t.r, 2.05f, t.z+.10f, .75f*t.r, .85f*t.r, .75f*t.r,
                .14f, .42f, .16f);
    }

    private void drawEnemy(Enemy e) {
        float t = animationTime * 7.0f + e.x * .17f + e.z * .11f;
        float stride = (float)Math.sin(t) * .22f;
        float bob = Math.abs((float)Math.sin(t * .5f)) * .035f;
        float attack = Math.max(0f, 1f - Math.min(1f, (System.currentTimeMillis()-e.lastShot) / 220f));
        box(e.x, .88f + bob, e.z, .75f, 1.7f, .65f, .55f, .10f, .08f);
        box(e.x, 1.97f + bob, e.z, .65f, .65f, .65f, .25f, .07f, .05f);
        boxRotated(e.x - .22f, .52f, e.z, .24f, 1.0f, .24f, -stride, 0f, 0f, .16f, .12f, .09f);
        boxRotated(e.x + .22f, .52f, e.z, .24f, 1.0f, .24f, stride, 0f, 0f, .16f, .12f, .09f);
        boxRotated(e.x - .40f, 1.16f + bob, e.z, .20f, .85f, .20f, stride * .65f, 0f, -12f, .46f, .14f, .10f);
        boxRotated(e.x + .40f, 1.16f + bob, e.z, .20f, .85f, .20f, -stride * .65f, 0f, 12f, .46f, .14f, .10f);
        float recoil = attack * .10f;
        boxRotated(e.x, 1.20f, e.z - .48f - recoil, .24f, .24f, .85f, 0f, 0f, 0f, .12f, .12f, .10f);
        float pct = Math.max(0f, e.hp / 100f);
        box(e.x, 2.55f, e.z, .95f, .08f, .10f, .15f, .08f, .06f);
        if (pct > 0) box(e.x - .48f*(1-pct), 2.56f, e.z, .92f*pct, .10f, .12f,
                .72f, .12f, .08f);
    }

    /**
     * Visual-only player hook.
     *
     * Gameplay movement, aiming and firing remain untouched. The actual King
     * sprite is composited by ThreeDGameActivity/KingSpriteDrawable so the
     * character can use the existing high-resolution artwork without changing
     * gameplay state or collision logic.
     */
    private void drawPlayer() {
        // Keep only the contact shadow in the 3D pass; the character art itself
        // is drawn by the transparent Canvas overlay on top of the scene.
        box(px, .055f, pz, 1.05f, .035f, .70f, .055f, .065f, .06f);
    }

    public float getPlayerSpeed() { return (float)Math.hypot(playerVX, playerVZ); }
    public float getPlayerYaw() { return yaw; }
    public boolean isPlayerFiringVisual() { return muzzleFlash > 0f; }

    private void drawPickup(Pickup p) {
        float bob = .12f * (float)Math.sin(System.nanoTime()/180_000_000.0 + p.type);
        if (p.type == Pickup.GRENADE)
            box(p.x, .55f+bob, p.z, .55f, .55f, .55f, .16f, .35f, .18f);
        else if (p.type == Pickup.AMMO)
            box(p.x, .45f+bob, p.z, .70f, .45f, .45f, .66f, .51f, .16f);
        else
            box(p.x, .45f+bob, p.z, .72f, .45f, .45f, .70f, .16f, .12f);
    }

    private void movePlayer(float dt) {
        float len = (float)Math.hypot(moveX, moveY);
        float targetVX = 0f, targetVZ = 0f;
        if (len >= .05f) {
            float forwardX = (float)Math.sin(yaw), forwardZ = (float)Math.cos(yaw);
            float rightX = (float)Math.cos(yaw), rightZ = -(float)Math.sin(yaw);
            float nx = (forwardX*moveY + rightX*moveX) / len;
            float nz = (forwardZ*moveY + rightZ*moveX) / len;
            float speed = 5.2f + Math.min(1f, len) * 1.8f;
            targetVX = nx * speed;
            targetVZ = nz * speed;
        }
        float response = 1f - (float)Math.exp(-(len >= .05f ? 12f : 16f) * dt);
        playerVX += (targetVX - playerVX) * response;
        playerVZ += (targetVZ - playerVZ) * response;
        if (Math.abs(playerVX) < .01f) playerVX = 0f;
        if (Math.abs(playerVZ) < .01f) playerVZ = 0f;
        float nx = px + playerVX * dt, nz = pz + playerVZ * dt;
        if (!blocked(nx, pz, .55f)) px = nx; else playerVX = 0f;
        if (!blocked(px, nz, .55f)) pz = nz; else playerVZ = 0f;
    }

    private void updateEnemies(float dt) {
        long now = System.currentTimeMillis();
        for (int i=0;i<enemies.size();i++) {
            Enemy e=enemies.get(i);
            if (e.hp<=0) continue;
            float dx=px-e.x,dz=pz-e.z,d=Math.max(.001f,(float)Math.hypot(dx,dz));

            float sepX=0f,sepZ=0f;
            for(int j=0;j<enemies.size();j++){
                if(i==j)continue;
                Enemy other=enemies.get(j);
                if(other.hp<=0)continue;
                float ox=e.x-other.x,oz=e.z-other.z,od=(float)Math.hypot(ox,oz);
                if(od<1.5f&&od>.001f){float push=(1.5f-od)/1.5f;sepX+=ox/od*push;sepZ+=oz/od*push;}
            }

            float moveSpeed=(d>9f?1.55f:1.05f);
            if(d>2.4f){
                float desiredX=dx/d,desiredZ=dz/d;
                if(d<8.5f){
                    float strafe=(float)Math.sin(animationTime*1.6f+i*1.7f)*.48f;
                    float sideX=-desiredZ,sideZ=desiredX;
                    desiredX=desiredX*(1f-Math.abs(strafe)*.35f)+sideX*strafe;
                    desiredZ=desiredZ*(1f-Math.abs(strafe)*.35f)+sideZ*strafe;
                }
                desiredX+=sepX*.9f; desiredZ+=sepZ*.9f;
                float len=Math.max(.001f,(float)Math.hypot(desiredX,desiredZ));
                desiredX/=len;desiredZ/=len;
                float nx=e.x+desiredX*moveSpeed*dt,nz=e.z+desiredZ*moveSpeed*dt;
                if(!blocked(nx,e.z,.45f))e.x=nx;
                if(!blocked(e.x,nz,.45f))e.z=nz;
            } else {
                hp=Math.max(0,hp-(int)Math.ceil(7f*dt));
                if(hp<=0) gameOver=true;
            }

            if(d<16f&&now-e.lastShot>1200){
                float tx=dx/d,tz=dz/d;
                projectiles.add(new Projectile(e.x+tx*.72f,1.15f,e.z+tz*.72f,tx*8.5f,tz*8.5f,2.8f,8f,false));
                e.lastShot=now;
            }
        }
    }

    private int livingEnemyCount() {
        int n=0;
        for(Enemy e:enemies) if(e.hp>0) n++;
        return n;
    }

    public void reload() {
        if(gameOver || ammo>=30 || reserve<=0)return;
        int add=Math.min(30-ammo,reserve);
        ammo+=add;reserve-=add;
    }

    public void throwGrenade() {
        if(gameOver || grenades<=0 || hp<=0)return;
        grenades--;
        float fx=(float)Math.sin(yaw),fz=(float)Math.cos(yaw);
        projectiles.add(new Projectile(px+fx*.75f,1.0f,pz+fz*.75f,fx*9.2f,fz*9.2f,.82f,90f,true,true,3.8f));
    }

    public void resetBattle() {
        px=0f;pz=0f;hp=100;ammo=30;reserve=120;kills=0;grenades=3;wave=1;
        gameOver=false;nextWaveAt=0;fireCooldown=0;muzzleFlash=0;weaponKick=0;
        projectiles.clear();spawnEnemies();spawnPickups();
    }

    private void explodeAt(float x,float z,float damage,float radius) {
        for(Enemy e:enemies) {
            if(e.hp<=0) continue;
            float d=(float)Math.hypot(e.x-x,e.z-z);
            if(d<=radius) {
                int dealt=Math.max(20,Math.round(damage*(1f-d/radius)));
                e.hp-=dealt;
                if(e.hp<=0){e.hp=0;kills++;}
            }
        }
    }

    private void collectPickups() {
        for (int i = pickups.size()-1; i >= 0; i--) {
            Pickup p = pickups.get(i);
            if (Math.hypot(px-p.x, pz-p.z) > 1.15) continue;
            if (p.type == Pickup.AMMO) reserve = Math.min(180, reserve+30);
            else if (p.type == Pickup.GRENADE) grenades = Math.min(9, grenades+1);
            else hp = Math.min(100, hp+35);
            pickups.remove(i);
        }
    }

    public boolean onTouch(MotionEvent e) {
        float x=e.getX(), y=e.getY();
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                lastX=x; lastY=y;
                if (x < width*.50f) { moving=true; moveX=moveY=0; }
                else { aiming=true; fire(); }
                return true;
            case MotionEvent.ACTION_MOVE:
                if (moving) {
                    moveX=Math.max(-1,Math.min(1,(x-lastX)/95f));
                    moveY=Math.max(-1,Math.min(1,(y-lastY)/95f));
                } else if (aiming) {
                    yaw+=(x-lastX)*.004f;
                    if (Math.abs(x-lastX)>4) fire();
                }
                lastX=x; lastY=y;
                return true;
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL:
                moveX=moveY=0; moving=false; aiming=false;
                return true;
        }
        return true;
    }

    private void fire() {
        if (gameOver || ammo<=0 || fireCooldown>0 || hp<=0) return;
        ammo--;
        fireCooldown=.20f;
        muzzleFlash=.10f;
        weaponKick=.16f;
        float fx=(float)Math.sin(yaw), fz=(float)Math.cos(yaw);
        float sx=px+fx*.72f, sz=pz+fz*.72f;
        Enemy best=null; float bestDist=22f;
        for (Enemy e:enemies) {
            if (e.hp<=0) continue;
            float dx=e.x-px,dz=e.z-pz,d=(float)Math.hypot(dx,dz);
            if (d<.5f || d>22) continue;
            float dot=(dx/d)*fx+(dz/d)*fz;
            if(dot>.82f && d<bestDist){best=e;bestDist=d;}
        }
        float tx=fx,tz=fz;
        if(best!=null){
            float dx=best.x-px,dz=best.z-pz,d=Math.max(.001f,(float)Math.hypot(dx,dz));
            tx=dx/d;tz=dz/d;
        }
        projectiles.add(new Projectile(sx,1.22f,sz,tx*14.5f,tz*14.5f,2.0f,45,true));
    }

    private void updateProjectiles(float dt) {
        for (int i=projectiles.size()-1;i>=0;i--) {
            Projectile b=projectiles.get(i);
            float oldX=b.x, oldZ=b.z;
            b.x += b.vx*dt;
            b.z += b.vz*dt;
            b.life -= dt;
            boolean expired=b.life<=0f;
            boolean out=b.x<-36f||b.x>36f||b.z<-36f||b.z>36f||blocked(b.x,b.z,.10f);
            if (expired || out) {
                if (b.explosive && expired) explodeAt(b.x,b.z,b.damage,b.splashRadius);
                projectiles.remove(i);
                continue;
            }
            boolean remove=false;
            if (b.fromPlayer) {
                for (Enemy e:enemies) {
                    if (e.hp<=0) continue;
                    if (segmentDistance(e.x,e.z,oldX,oldZ,b.x,b.z)<.65f) {
                        e.hp -= b.damage;
                        if (e.hp<=0) { e.hp=0; kills++; }
                        if (b.explosive) explodeAt(b.x,b.z,b.damage,b.splashRadius);
                        remove=true;
                        break;
                    }
                }
            } else if (Math.hypot(px-b.x,pz-b.z)<.58f) {
                hp=Math.max(0,hp-Math.round(b.damage));
                if (hp<=0) gameOver=true;
                remove=true;
            }
            if (remove) projectiles.remove(i);
        }
    }

    private float segmentDistance(float px,float pz,float x1,float z1,float x2,float z2) {
        float dx=x2-x1,dz=z2-z1;
        if(dx==0f&&dz==0f)return(float)Math.hypot(px-x1,pz-z1);
        float t=((px-x1)*dx+(pz-z1)*dz)/(dx*dx+dz*dz);
        t=Math.max(0f,Math.min(1f,t));
        return(float)Math.hypot(px-(x1+t*dx),pz-(z1+t*dz));
    }

    private void drawProjectile(Projectile b) {
        float glow=(float)(.65+.35*Math.sin(animationTime*18f));
        if(b.fromPlayer) {
            if(b.explosive) box(b.x,.98f,b.z,.34f,.34f,.34f,.28f,.58f,.30f);
            else box(b.x,.92f,b.z,.22f,.18f,.48f,1.0f*glow,.68f,.20f);
        } else {
            box(b.x,.92f,b.z,.24f,.18f,.52f,.85f,.16f,.10f);
        }
    }

    private void box(float x,float y,float z,float w,float h,float d,float r,float g,float b){
        boxRotated(x,y,z,w,h,d,0f,0f,0f,r,g,b);
    }

    private void boxRotated(float x,float y,float z,float w,float h,float d,float rotX,float rotY,float rotZ,float r,float g,float b){
        Matrix.setIdentityM(model,0);
        Matrix.translateM(model,0,x,y,z);
        if (rotX != 0f) Matrix.rotateM(model,0,rotX * 57.29578f,1f,0f,0f);
        if (rotY != 0f) Matrix.rotateM(model,0,rotY * 57.29578f,0f,1f,0f);
        if (rotZ != 0f) Matrix.rotateM(model,0,rotZ,0f,0f,1f);
        Matrix.scaleM(model,0,w*.5f,h*.5f,d*.5f);
        Matrix.multiplyMM(mvp,0,vp,0,model,0);
        GLES20.glUseProgram(colorProgram);
        GLES20.glUniformMatrix4fv(uMatrix,1,false,mvp,0);
        GLES20.glUniform4f(uColor,r,g,b,1f);
        cube.position(0);
        GLES20.glEnableVertexAttribArray(aPos);
        GLES20.glVertexAttribPointer(aPos,3,GLES20.GL_FLOAT,false,0,cube);
        GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,36);
        GLES20.glDisableVertexAttribArray(aPos);
    }

    private int program(String v,String f){int vs=shader(GLES20.GL_VERTEX_SHADER,v),fs=shader(GLES20.GL_FRAGMENT_SHADER,f),p=GLES20.glCreateProgram();GLES20.glAttachShader(p,vs);GLES20.glAttachShader(p,fs);GLES20.glLinkProgram(p);return p;}
    private int shader(int type,String src){int s=GLES20.glCreateShader(type);GLES20.glShaderSource(s,src);GLES20.glCompileShader(s);return s;}
    private static FloatBuffer buf(float[] a){FloatBuffer b=ByteBuffer.allocateDirect(a.length*4).order(ByteOrder.nativeOrder()).asFloatBuffer();b.put(a).position(0);return b;}
    private static float[] makeCube(){
        float[][] f={{-1,-1,1,1,-1,1,1,1,1,-1,-1,1,1,1,1,-1,1,1},{1,-1,-1,-1,-1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,1,-1},{-1,1,1,1,1,1,1,1,-1,-1,1,1,1,1,-1,-1,1,-1},{-1,-1,-1,1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,-1,-1,1},{1,-1,1,1,-1,-1,1,1,-1,1,-1,1,1,1,-1,1,1,1},{-1,-1,-1,-1,-1,1,-1,1,1,-1,-1,-1,-1,1,1,-1,1,-1}};
        float[] o=new float[108];int q=0;for(float[] a:f)for(float n:a)o[q++]=n;return o;
    }

    public int getHp(){return hp;} public int getAmmo(){return ammo;} public int getReserve(){return reserve;} public int getKills(){return kills;} public int getGrenades(){return grenades;} public int getWave(){return wave;}
    public boolean isGameOver(){return gameOver;}
    public void drawMinimap(Canvas c,float left,float top,float size) {
        Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
        float h=size*.82f;
        p.setStyle(Paint.Style.FILL);p.setColor(0xD91B241F);c.drawRoundRect(left,top,left+size,top+h,18,18,p);
        p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xC7E0D29B);c.drawRoundRect(left,top,left+size,top+h,18,18,p);
        float innerL=left+9,innerT=top+27,innerW=size-18,innerH=h-36,sx=innerW/68f,sz=innerH/68f;
        p.setStyle(Paint.Style.FILL);p.setColor(0xFF29362F);c.drawRect(innerL,innerT,innerL+innerW,innerT+innerH,p);
        for(Road r:roads){p.setColor(0xAA6D7770);p.setStrokeWidth(Math.max(2f,r.width*sx*.65f));c.drawLine(innerL+(r.x1+34)*sx,innerT+(r.z1+34)*sz,innerL+(r.x2+34)*sx,innerT+(r.z2+34)*sz,p);}
        for(Building b:buildings){p.setColor(0xB56B5848);c.drawRect(innerL+(b.x-b.w*.5f+34)*sx,innerT+(b.z-b.d*.5f+34)*sz,innerL+(b.x+b.w*.5f+34)*sx,innerT+(b.z+b.d*.5f+34)*sz,p);}
        p.setColor(0xFFE1B85A);c.drawCircle(innerL+(px+34)*sx,innerT+(pz+34)*sz,4,p);
        for(Enemy e:enemies)if(e.hp>0){p.setColor(0xFFE55A50);c.drawCircle(innerL+(e.x+34)*sx,innerT+(e.z+34)*sz,3,p);}
        p.setStyle(Paint.Style.FILL);p.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);p.setTextSize(12);p.setColor(0xFFF1DBA1);p.setTextAlign(Paint.Align.LEFT);c.drawText("TACTICAL MAP",left+14,top+18,p);p.setTypeface(android.graphics.Typeface.DEFAULT);
    }


    public void pause(){} public void resume(){}

    private static final class Road{final float x1,z1,x2,z2,width;Road(float x1,float z1,float x2,float z2,float width){this.x1=x1;this.z1=z1;this.x2=x2;this.z2=z2;this.width=width;}}
    private static final class Building{final float x,z,w,d,h;final int style;Building(float x,float z,float w,float d,float h,int style){this.x=x;this.z=z;this.w=w;this.d=d;this.h=h;this.style=style;}}
    private static final class Tree{final float x,z,r;Tree(float x,float z,float r){this.x=x;this.z=z;this.r=r;}}
    private static final class Enemy{float x,z;int hp=100;long lastShot;Enemy(float x,float z){this.x=x;this.z=z;}}
    private static final class Projectile{float x,y,z,vx,vz,life,damage,splashRadius;boolean fromPlayer,explosive;Projectile(float x,float y,float z,float vx,float vz,float life,float damage,boolean fromPlayer){this(x,y,z,vx,vz,life,damage,fromPlayer,false,0f);}Projectile(float x,float y,float z,float vx,float vz,float life,float damage,boolean fromPlayer,boolean explosive,float splashRadius){this.x=x;this.y=y;this.z=z;this.vx=vx;this.vz=vz;this.life=life;this.damage=damage;this.fromPlayer=fromPlayer;this.explosive=explosive;this.splashRadius=splashRadius;}}
    private static final class Pickup{static final int AMMO=1,GRENADE=2,MEDKIT=3;final float x,z;final int type;Pickup(float x,float z,int type){this.x=x;this.z=z;this.type=type;}}
}
