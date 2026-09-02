package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.opengl.GLES20;
import android.opengl.GLSurfaceView;
import android.opengl.GLUtils;
import android.opengl.Matrix;
import android.view.MotionEvent;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.FloatBuffer;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;
import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

/**
 * Main playable 3D battle renderer. The 3D world is the game, not a separate demo:
 * perspective/isometric camera, large follow-map, left joystick, right aim/fire,
 * visible projectiles, enemy shooting, health, ammo and kills.
 */
public final class Battle3DRenderer implements GLSurfaceView.Renderer {
    private static final float SCALE=1.55f, WORLD=108f;
    private final Context context;
    private final ArrayList<CityKit.Piece> world=new ArrayList<>();
    private final ArrayList<CityKit.Piece> buildings=new ArrayList<>();
    private final ArrayList<Enemy> enemies=new ArrayList<>();
    private final ArrayList<Bullet> bullets=new ArrayList<>();
    private final ArrayList<Bitmap> pending=new ArrayList<>();
    private final ArrayList<Integer> textures=new ArrayList<>();
    private final float[] proj=new float[16],view=new float[16],vp=new float[16],model=new float[16],mvp=new float[16],uiProj=new float[16];
    private FloatBuffer cube,quad,uv;
    private int colorProgram,texProgram,ca,cc,cm,ta,tu,tm,ts;
    private int width,height;
    private float px=0,pz=31,yaw=0,stickX,stickY,joyCX,joyCY,joyRadius=110;
    private int joyPointer=-1,aimPointer=-1;
    private float aimLastX,aimLastY;
    private long lastNs;
    private int hp=100,ammo=120,kills=0;
    private float fireTimer=0;
    private boolean fireHeld=false;

    public Battle3DRenderer(Context c){
        context=c;
        loadPanels();
        CityKit kit=new CityKit(20261201L);
        for(CityKit.Piece q:kit.pieces()){
            CityKit.Piece s=new CityKit.Piece(q.type,q.x*SCALE,q.z*SCALE,q.w*SCALE,q.d*SCALE,q.h,q.panel);
            world.add(s);
            if(isBuilding(s.type)) buildings.add(s);
        }
        // A real combat roster instead of three passive enemies.
        float[][] spots={{-42,-42},{0,-43},{42,-42},{-43,0},{43,0},{-42,42},{0,43},{42,42},
                {-30,-16},{30,-16},{-30,16},{30,16},{-16,-30},{16,-30},{-16,30},{16,30}};
        for(float[] s:spots) enemies.add(new Enemy(s[0],s[1]));
    }

    private boolean isBuilding(CityKit.Type t){
        return t==CityKit.Type.HOUSE_1||t==CityKit.Type.HOUSE_2||t==CityKit.Type.WAREHOUSE||t==CityKit.Type.SHOP;
    }

    private void loadPanels(){
        try(InputStream in=context.getAssets().open("original_packages/kenney_isometric-buildings.zip");
            ZipInputStream z=new ZipInputStream(in)){
            ZipEntry e;
            while((e=z.getNextEntry())!=null && pending.size()<32){
                String n=e.getName().toLowerCase();
                if(e.isDirectory()||!n.endsWith(".png")||!n.contains("buildingtile")) continue;
                byte[] d=read(z); Bitmap b=BitmapFactory.decodeByteArray(d,0,d.length);
                if(b!=null) pending.add(b);
            }
        }catch(Exception ignored){}
    }
    private byte[] read(ZipInputStream z)throws Exception{
        ByteArrayOutputStream o=new ByteArrayOutputStream(); byte[] b=new byte[8192]; int n;
        while((n=z.read(b))>0)o.write(b,0,n); return o.toByteArray();
    }

    @Override public void onSurfaceCreated(GL10 gl,EGLConfig cfg){
        GLES20.glClearColor(.18f,.24f,.29f,1);
        GLES20.glEnable(GLES20.GL_DEPTH_TEST);
        GLES20.glEnable(GLES20.GL_CULL_FACE);
        colorProgram=program("attribute vec3 A;uniform mat4 M;void main(){gl_Position=M*vec4(A,1.);}",
                "precision mediump float;uniform vec4 C;void main(){gl_FragColor=C;}");
        texProgram=program("attribute vec3 A;attribute vec2 U;uniform mat4 M;varying vec2 T;void main(){T=U;gl_Position=M*vec4(A,1.);}",
                "precision mediump float;uniform sampler2D S;varying vec2 T;void main(){gl_FragColor=texture2D(S,T);}");
        ca=GLES20.glGetAttribLocation(colorProgram,"A"); cc=GLES20.glGetUniformLocation(colorProgram,"C"); cm=GLES20.glGetUniformLocation(colorProgram,"M");
        ta=GLES20.glGetAttribLocation(texProgram,"A"); tu=GLES20.glGetAttribLocation(texProgram,"U"); tm=GLES20.glGetUniformLocation(texProgram,"M"); ts=GLES20.glGetUniformLocation(texProgram,"S");
        cube=buffer(cubeVertices());
        quad=buffer(new float[]{-1,-1,0,1,-1,0,1,1,0,-1,-1,0,1,1,0,-1,1,0});
        uv=buffer(new float[]{0,1,1,1,1,0,0,1,1,0,0,0});
        for(Bitmap b:pending){textures.add(texture(b));b.recycle();} pending.clear();
        lastNs=System.nanoTime();
    }

    private int texture(Bitmap b){
        int[] id=new int[1]; GLES20.glGenTextures(1,id,0); GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,id[0]);
        GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MIN_FILTER,GLES20.GL_LINEAR);
        GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MAG_FILTER,GLES20.GL_LINEAR);
        GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_S,GLES20.GL_CLAMP_TO_EDGE);
        GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_T,GLES20.GL_CLAMP_TO_EDGE);
        GLUtils.texImage2D(GLES20.GL_TEXTURE_2D,0,b,0); return id[0];
    }

    private float[] cubeVertices(){
        float[][] f={{-1,-1,1,1,-1,1,1,1,1,-1,-1,1,1,1,1,-1,1,1},
                {1,-1,-1,-1,-1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,1,-1},
                {-1,1,1,1,1,1,1,1,-1,-1,1,1,1,1,-1,-1,1,-1},
                {-1,-1,-1,1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,-1,-1,1},
                {1,-1,1,1,-1,-1,1,1,-1,1,-1,1,1,1,-1,1,1,1},
                {-1,-1,-1,-1,-1,1,-1,1,1,-1,-1,-1,-1,1,1,-1,1,-1}};
        float[] o=new float[108]; int q=0; for(float[] a:f)for(float x:a)o[q++]=x; return o;
    }

    @Override public void onSurfaceChanged(GL10 gl,int w,int h){
        width=w; height=h; GLES20.glViewport(0,0,w,h);
        float aspect=(float)w/Math.max(1,h);
        float near=.2f, far=220f, top=(float)Math.tan(Math.toRadians(56/2f))*near, right=top*aspect;
        Matrix.frustumM(proj,0,-right,right,-top,top,near,far);
        Matrix.orthoM(uiProj,0,0,w,h,0,-1,1);
        joyCX=145; joyCY=h-145;
    }

    @Override public void onDrawFrame(GL10 gl){
        long now=System.nanoTime(); float dt=Math.min(.05f,(now-lastNs)/1e9f); lastNs=now;
        fireTimer=Math.max(0,fireTimer-dt);
        movePlayer(dt); updateEnemies(dt); updateBullets(dt);

        float camDist=19f;
        float cx=px-(float)Math.sin(yaw)*camDist;
        float cz=pz-(float)Math.cos(yaw)*camDist;
        Matrix.setLookAtM(view,0,cx,15.5f,cz,px,0,pz,0,1,0);
        Matrix.multiplyMM(vp,0,proj,0,view,0);

        GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT|GLES20.GL_DEPTH_BUFFER_BIT);
        drawGround();
        for(CityKit.Piece q:world) drawPiece(q);
        for(CityKit.Piece b:buildings){shadow(b.x,b.z,Math.max(b.w,b.d)*.52f);drawBuilding(b);}
        for(Enemy e:enemies)if(e.hp>0){shadow(e.x,e.z,1.2f);character(e.x,e.z,.72f,.82f,.12f,.08f);}
        for(Bullet b:bullets)if(b.life>0) projectile(b);
        shadow(px,pz,1.1f); character(px,pz,.72f,.12f,.32f,.78f);
        drawControls();
    }

    private void drawGround(){box(0,-.3f,0,WORLD,.5f,WORLD,.20f,.43f,.22f);}
    private void drawPiece(CityKit.Piece q){
        switch(q.type){
            case ROAD: box(q.x,.02f,q.z,q.w,.08f,q.d,.20f,.21f,.22f); roadMark(q); break;
            case SIDEWALK: box(q.x,.09f,q.z,q.w,.12f,q.d,.58f,.56f,.49f); break;
            case WALL: box(q.x,q.h/2,q.z,q.w,q.h,q.d,.32f,.28f,.23f); break;
            case TREE: tree(q.x,q.z); break;
            case ROCK: rock(q.x,q.z,q.w,q.d,q.h); break;
            case CAR: car(q.x,q.z); break;
            case LOOT: loot(q.x,q.z); break;
            case RUBBLE: rubble(q); break;
            default: break;
        }
    }
    private float[] buildingBase(CityKit.Type t){
        if(t==CityKit.Type.WAREHOUSE)return new float[]{.28f,.32f,.35f};
        if(t==CityKit.Type.SHOP)return new float[]{.60f,.40f,.17f};
        return new float[]{.68f,.62f,.48f};
    }
    private void drawBuilding(CityKit.Piece b){
        box(b.x,b.h/2,b.z,b.w,b.h,b.d,buildingBase(b.type));
        facade(b); box(b.x,b.h+.07f,b.z,b.w+.15f,.14f,b.d+.15f,.82f,.77f,.58f);
    }

    private void movePlayer(float dt){
        float l=(float)Math.hypot(stickX,stickY); if(l<.08f)return;
        float s=8.0f*dt,fx=(float)Math.sin(yaw),fz=(float)Math.cos(yaw),rx=(float)Math.cos(yaw),rz=-(float)Math.sin(yaw);
        float nx=px+(fx*(stickY/l)+rx*(stickX/l))*s;
        float nz=pz+(fz*(stickY/l)+rz*(stickX/l))*s;
        if(!blocked(nx,pz,.7f))px=nx; if(!blocked(px,nz,.7f))pz=nz;
        px=Math.max(-50,Math.min(50,px)); pz=Math.max(-50,Math.min(50,pz));
    }
    private boolean blocked(float x,float z,float r){
        for(CityKit.Piece b:buildings)if(x>b.x-b.w/2-r&&x<b.x+b.w/2+r&&z>b.z-b.d/2-r&&z<b.z+b.d/2+r)return true;
        return false;
    }

    private void updateEnemies(float dt){
        for(Enemy e:enemies)if(e.hp>0){
            float ax=px-e.x,az=pz-e.z,d=(float)Math.hypot(ax,az);
            e.shoot=Math.max(0,e.shoot-dt);
            if(d>3.5f){float sp=1.25f;e.x+=ax/d*sp*dt;e.z+=az/d*sp*dt;}
            if(d<27f&&e.shoot<=0){
                e.shoot=1.05f+(e.seed%4)*.18f;
                float vx=ax/Math.max(.01f,d)*13f,vz=az/Math.max(.01f,d)*13f;
                bullets.add(new Bullet(e.x,e.z,vx,vz,2.5f,false));
            }
            e.x=Math.max(-50,Math.min(50,e.x)); e.z=Math.max(-50,Math.min(50,e.z));
        }
    }

    private void updateBullets(float dt){
        Iterator<Bullet> it=bullets.iterator();
        while(it.hasNext()){
            Bullet b=it.next(); b.x+=b.vx*dt;b.z+=b.vz*dt;b.life-=dt;
            boolean remove=b.life<=0||Math.abs(b.x)>55||Math.abs(b.z)>55;
            if(!remove&&!b.fromPlayer){
                if(Math.hypot(b.x-px,b.z-pz)<.85){hp=Math.max(0,hp-8);remove=true;}
            } else if(!remove){
                for(Enemy e:enemies)if(e.hp>0&&Math.hypot(b.x-e.x,b.z-e.z)<1.0){
                    e.hp-=50; if(e.hp<=0)kills++; remove=true; break;
                }
            }
            if(remove)it.remove();
        }
    }

    private void fire(){
        if(ammo<=0||fireTimer>0)return;
        ammo--;fireTimer=.18f;
        float vx=(float)Math.sin(yaw)*17f,vz=(float)Math.cos(yaw)*17f;
        bullets.add(new Bullet(px,pz,vx,vz,2.2f,true));
    }

    private void roadMark(CityKit.Piece q){
        if(q.w>q.d)for(float x=q.x-q.w/2+4*SCALE;x<q.x+q.w/2;x+=7*SCALE)box(x,.075f,q.z,.8f,.025f,.10f,.86f,.76f,.34f);
        else if(q.d>q.w)for(float z=q.z-q.d/2+4*SCALE;z<q.z+q.d/2;z+=7*SCALE)box(q.x,.075f,z,.10f,.025f,.8f,.86f,.76f,.34f);
    }
    private void tree(float x,float z){
        shadow(x,z,1.35f);box(x,1.0f,z,.45f,2,.45f,.40f,.22f,.10f);box(x,2.55f,z,2.3f,2.1f,2.3f,.13f,.42f,.08f);box(x,3.45f,z,1.55f,1.1f,1.55f,.18f,.50f,.08f);
    }
    private void rock(float x,float z,float w,float d,float h){box(x,h/2,z,w,h,d,.30f,.31f,.32f);box(x,h*.78f,z,w*.65f,h*.55f,d*.65f,.39f,.40f,.40f);}
    private void car(float x,float z){box(x,.48f,z,2.8f,.75f,1.55f,.64f,.14f,.08f);box(x,.95f,z,1.55f,.42f,1.2f,.12f,.18f,.22f);}
    private void loot(float x,float z){box(x,.32f,z,1.05f,.64f,1.05f,.82f,.58f,.08f);box(x,.68f,z,1.12f,.08f,1.12f,.95f,.80f,.15f);}
    private void rubble(CityKit.Piece q){box(q.x,.45f,q.z,q.w,.9f,q.d,.34f,.31f,.29f);box(q.x+.6f,.7f,q.z-.25f,q.w*.4f,.55f,q.d*.5f,.43f,.39f,.34f);}
    private void character(float x,float z,float s,float r,float g,float b){box(x,.65f,z,s,.95f,s*.72f,r,g,b);box(x,1.38f,z,s*.62f,.55f,s*.62f,.84f,.68f,.48f);}
    private void projectile(Bullet b){box(b.x,.38f,b.z,.28f,.28f,.28f,b.fromPlayer?1f:.92f,b.fromPlayer?.82f:.16f,b.fromPlayer?.10f:.06f);}
    private void shadow(float x,float z,float s){box(x,.012f,z,s,.025f,s*.68f,.11f,.11f,.10f);}

    private void drawControls(){
        GLES20.glDisable(GLES20.GL_DEPTH_TEST); GLES20.glDisable(GLES20.GL_CULL_FACE);
        drawCircle(joyCX,joyCY,joyRadius,.10f,.10f,.12f,.40f);
        float k=joyRadius*.58f; drawCircle(joyCX+stickX*k,joyCY+stickY*k,k,.22f,.52f,.78f,.75f);
        float fx=width-135,fy=height-145; drawCircle(fx,fy,82,.72f,.12f,.08f,.55f);
        drawCircle(fx,fy,58,.86f,.22f,.12f,.75f);
        // Direction/aim pad marker.
        float ax=fx+(float)Math.sin(yaw)*30,ay=fy-(float)Math.cos(yaw)*30; drawCircle(ax,ay,12,.98f,.92f,.70f,.95f);
        GLES20.glEnable(GLES20.GL_DEPTH_TEST); GLES20.glEnable(GLES20.GL_CULL_FACE);
    }
    private void drawCircle(float cx,float cy,float r,float cr,float cg,float cb,float caa){
        int n=32;float[] v=new float[(n+2)*3];v[0]=cx;v[1]=cy;v[2]=0;
        for(int i=0;i<=n;i++){double a=i*Math.PI*2/n;v[(i+1)*3]=cx+(float)Math.cos(a)*r;v[(i+1)*3+1]=cy+(float)Math.sin(a)*r;v[(i+1)*3+2]=0;}
        FloatBuffer f=buffer(v);Matrix.setIdentityM(model,0);Matrix.multiplyMM(mvp,0,uiProj,0,model,0);
        GLES20.glUseProgram(colorProgram);GLES20.glUniformMatrix4fv(cm,1,false,mvp,0);GLES20.glUniform4f(cc,cr,cg,cb,caa);
        GLES20.glEnableVertexAttribArray(ca);GLES20.glVertexAttribPointer(ca,3,GLES20.GL_FLOAT,false,0,f);GLES20.glDrawArrays(GLES20.GL_TRIANGLE_FAN,0,n+2);GLES20.glDisableVertexAttribArray(ca);
    }

    private void box(float x,float y,float z,float w,float h,float d,float r,float g,float b){
        Matrix.setIdentityM(model,0);Matrix.translateM(model,0,x,y,z);Matrix.scaleM(model,0,w/2,h/2,d/2);Matrix.multiplyMM(mvp,0,vp,0,model,0);
        GLES20.glUseProgram(colorProgram);GLES20.glUniformMatrix4fv(cm,1,false,mvp,0);GLES20.glUniform4f(cc,r,g,b,1);
        cube.position(0);GLES20.glEnableVertexAttribArray(ca);GLES20.glVertexAttribPointer(ca,3,GLES20.GL_FLOAT,false,0,cube);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,36);GLES20.glDisableVertexAttribArray(ca);
    }
    private void box(float x,float y,float z,float w,float h,float d,float[] c){box(x,y,z,w,h,d,c[0],c[1],c[2]);}
    private void facade(CityKit.Piece b){
        if(textures.isEmpty())return;int id=textures.get(Math.abs(b.panel)%textures.size());
        face(id,b.x,b.z-b.d/2-.025f,b.w,b.h,0);face(id,b.x,b.z+b.d/2+.025f,b.w,b.h,180);
        face(id,b.x-b.w/2-.025f,b.z,b.d,b.h,90);face(id,b.x+b.w/2+.025f,b.z,b.d,b.h,-90);
    }
    private void face(int id,float x,float z,float w,float h,float rot){
        Matrix.setIdentityM(model,0);Matrix.translateM(model,0,x,h/2,z);Matrix.rotateM(model,0,rot,0,1,0);Matrix.scaleM(model,0,w/2,h/2,1);Matrix.multiplyMM(mvp,0,vp,0,model,0);
        GLES20.glUseProgram(texProgram);GLES20.glUniformMatrix4fv(tm,1,false,mvp,0);GLES20.glUniform1i(ts,0);GLES20.glActiveTexture(GLES20.GL_TEXTURE0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,id);
        quad.position(0);uv.position(0);GLES20.glEnableVertexAttribArray(ta);GLES20.glVertexAttribPointer(ta,3,GLES20.GL_FLOAT,false,0,quad);GLES20.glEnableVertexAttribArray(tu);GLES20.glVertexAttribPointer(tu,2,GLES20.GL_FLOAT,false,0,uv);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,6);GLES20.glDisableVertexAttribArray(ta);GLES20.glDisableVertexAttribArray(tu);
    }

    @Override public boolean onTouchEvent(MotionEvent e){return onTouch(e);}
    public boolean onTouch(MotionEvent e){
        int action=e.getActionMasked(),index=e.getActionIndex(),id=e.getPointerId(index);float x=e.getX(index),y=e.getY(index);
        if(action==MotionEvent.ACTION_DOWN||action==MotionEvent.ACTION_POINTER_DOWN){
            if(x<width*.48f && joyPointer==-1){joyPointer=id;updateJoystick(x,y);return true;}
            if(aimPointer==-1){aimPointer=id;aimLastX=x;aimLastY=y;fireHeld=true;fire();return true;}
        } else if(action==MotionEvent.ACTION_MOVE){
            for(int i=0;i<e.getPointerCount();i++){int pid=e.getPointerId(i);float xx=e.getX(i),yy=e.getY(i);
                if(pid==joyPointer)updateJoystick(xx,yy); else if(pid==aimPointer){yaw+=(xx-aimLastX)*.006f;aimLastX=xx;aimLastY=yy;fireHeld=true;fire();}}
            return true;
        } else if(action==MotionEvent.ACTION_UP||action==MotionEvent.ACTION_POINTER_UP||action==MotionEvent.ACTION_CANCEL){
            if(id==joyPointer){joyPointer=-1;stickX=stickY=0;}
            if(id==aimPointer){aimPointer=-1;fireHeld=false;}
            return true;
        }
        return true;
    }
    private void updateJoystick(float x,float y){
        float dx=x-joyCX,dy=y-joyCY,l=(float)Math.hypot(dx,dy),max=joyRadius*.82f;if(l>max){dx*=max/l;dy*=max/l;}stickX=dx/max;stickY=dy/max;
    }
    public int getHp(){return hp;} public int getAmmo(){return ammo;} public int getKills(){return kills;}
    public void pause(){} public void resume(){}

    private static FloatBuffer buffer(float[] a){ByteBuffer b=ByteBuffer.allocateDirect(a.length*4).order(ByteOrder.nativeOrder());FloatBuffer f=b.asFloatBuffer();f.put(a).position(0);return f;}
    private int program(String vs,String fs){int v=GLES20.glCreateShader(GLES20.GL_VERTEX_SHADER);GLES20.glShaderSource(v,vs);GLES20.glCompileShader(v);int f=GLES20.glCreateShader(GLES20.GL_FRAGMENT_SHADER);GLES20.glShaderSource(f,fs);GLES20.glCompileShader(f);int p=GLES20.glCreateProgram();GLES20.glAttachShader(p,v);GLES20.glAttachShader(p,f);GLES20.glLinkProgram(p);return p;}

    private static final class Enemy{float x,z,hp=100,shoot;final int seed;Enemy(float x,float z){this.x=x;this.z=z;seed=(int)(Math.abs(x*13+z*7))%9;shoot=.4f+seed*.12f;}}
    private static final class Bullet{float x,z,vx,vz,life;boolean fromPlayer;Bullet(float x,float z,float vx,float vz,float life,boolean p){this.x=x;this.z=z;this.vx=vx;this.vz=vz;this.life=life;fromPlayer=p;}}
}
