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
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;
import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

/** Level 12 mobile isometric renderer: roads, districts, props and textured building panels. */
public final class AdvancedThreeDRenderer implements GLSurfaceView.Renderer {
    private final Context context;
    private final ArrayList<CityKit.Piece> world=new ArrayList<>();
    private final ArrayList<CityKit.Piece> buildings=new ArrayList<>();
    private final ArrayList<Enemy> enemies=new ArrayList<>();
    private final ArrayList<Bitmap> pending=new ArrayList<>();
    private final ArrayList<Integer> textures=new ArrayList<>();
    private final float[] proj=new float[16],view=new float[16],vp=new float[16],model=new float[16],mvp=new float[16];
    private FloatBuffer cube,quad,uv;
    private int colorProgram,texProgram,ca,cc,cm,ta,tu,tm,ts;
    private float px=0,pz=20,yaw=0,stickX,stickY,lastX,lastY;
    private boolean moving; private long lastNs; private int hp=100,ammo=30,kills=0; private float fireTimer;

    public AdvancedThreeDRenderer(Context c){
        context=c;
        loadPanels();
        CityKit kit=new CityKit(20261201L);
        for(CityKit.Piece q:kit.pieces()){world.add(q); if(isBuilding(q.type)) buildings.add(q);}
        for(int i=0;i<8;i++){double a=i*Math.PI/4; enemies.add(new Enemy((float)Math.cos(a)*25,(float)Math.sin(a)*25));}
    }
    private boolean isBuilding(CityKit.Type t){return t==CityKit.Type.HOUSE_1||t==CityKit.Type.HOUSE_2||t==CityKit.Type.WAREHOUSE||t==CityKit.Type.SHOP;}
    private void loadPanels(){
        try(InputStream in=context.getAssets().open("original_packages/kenney_isometric-buildings.zip");ZipInputStream z=new ZipInputStream(in)){
            ZipEntry e; while((e=z.getNextEntry())!=null&&pending.size()<28){String n=e.getName().toLowerCase();
                if(e.isDirectory()||!n.endsWith(".png")||!n.contains("buildingtile"))continue;
                byte[] d=read(z); Bitmap b=BitmapFactory.decodeByteArray(d,0,d.length); if(b!=null)pending.add(b);
            }
        }catch(Exception ignored){}
    }
    private byte[] read(ZipInputStream z)throws Exception{ByteArrayOutputStream o=new ByteArrayOutputStream();byte[] b=new byte[8192];int n;while((n=z.read(b))>0)o.write(b,0,n);return o.toByteArray();}
    @Override public void onSurfaceCreated(GL10 gl,EGLConfig cfg){
        GLES20.glClearColor(.035f,.055f,.07f,1); GLES20.glEnable(GLES20.GL_DEPTH_TEST); GLES20.glEnable(GLES20.GL_CULL_FACE);
        colorProgram=program("attribute vec3 A;uniform mat4 M;void main(){gl_Position=M*vec4(A,1.);}","precision mediump float;uniform vec4 C;void main(){gl_FragColor=C;}");
        texProgram=program("attribute vec3 A;attribute vec2 U;uniform mat4 M;varying vec2 T;void main(){T=U;gl_Position=M*vec4(A,1.);}","precision mediump float;uniform sampler2D S;varying vec2 T;void main(){gl_FragColor=texture2D(S,T);}");
        ca=GLES20.glGetAttribLocation(colorProgram,"A");cc=GLES20.glGetUniformLocation(colorProgram,"C");cm=GLES20.glGetUniformLocation(colorProgram,"M");
        ta=GLES20.glGetAttribLocation(texProgram,"A");tu=GLES20.glGetAttribLocation(texProgram,"U");tm=GLES20.glGetUniformLocation(texProgram,"M");ts=GLES20.glGetUniformLocation(texProgram,"S");
        cube=buffer(cubeVertices()); quad=buffer(new float[]{-1,-1,0,1,-1,0,1,1,0,-1,-1,0,1,1,0,-1,1,0}); uv=buffer(new float[]{0,1,1,1,1,0,0,1,1,0,0,0});
        for(Bitmap b:pending){textures.add(texture(b));b.recycle();} pending.clear(); lastNs=System.nanoTime();
    }
    private int texture(Bitmap b){int[] id=new int[1];GLES20.glGenTextures(1,id,0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,id[0]);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MIN_FILTER,GLES20.GL_LINEAR);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MAG_FILTER,GLES20.GL_LINEAR);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_S,GLES20.GL_CLAMP_TO_EDGE);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_T,GLES20.GL_CLAMP_TO_EDGE);GLUtils.texImage2D(GLES20.GL_TEXTURE_2D,0,b,0);return id[0];}
    private float[] cubeVertices(){float[][] f={{-1,-1,1,1,-1,1,1,1,1,-1,-1,1,1,1,1,-1,1,1},{1,-1,-1,-1,-1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,1,-1},{-1,1,1,1,1,1,1,1,-1,-1,1,1,1,1,-1,-1,1,-1},{-1,-1,-1,1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,-1,-1,1},{1,-1,1,1,-1,-1,1,1,-1,1,-1,1,1,1,-1,1,1,1},{-1,-1,-1,-1,-1,1,-1,1,1,-1,-1,-1,-1,1,1,-1,1,-1}};float[] o=new float[108];int q=0;for(float[] a:f)for(float x:a)o[q++]=x;return o;}
    @Override public void onSurfaceChanged(GL10 gl,int w,int h){GLES20.glViewport(0,0,w,h);float a=(float)w/Math.max(1,h);Matrix.orthoM(proj,0,-32*a,32*a,-32,32,.1f,160);}
    @Override public void onDrawFrame(GL10 gl){
        long now=System.nanoTime();float dt=Math.min(.05f,(now-lastNs)/1e9f);lastNs=now;fireTimer=Math.max(0,fireTimer-dt);movePlayer(dt);moveEnemies(dt);
        float cx=px-(float)Math.sin(yaw)*16,cz=pz+(float)Math.cos(yaw)*16;Matrix.setLookAtM(view,0,cx,15,cz,px,0,pz,0,1,0);Matrix.multiplyMM(vp,0,proj,0,view,0);
        GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT|GLES20.GL_DEPTH_BUFFER_BIT);
        box(0,-.30f,0,70,.5f,70,.28f,.55f,.22f);
        for(CityKit.Piece q:world){
            switch(q.type){
                case ROAD: box(q.x,.02f,q.z,q.w,.08f,q.d,.22f,.24f,.25f); roadMark(q); break;
                case SIDEWALK: box(q.x,.09f,q.z,q.w,.12f,q.d,.55f,.54f,.48f); break;
                case WALL: box(q.x,q.h/2,q.z,q.w,q.h,q.d,.34f,.29f,.24f); break;
                case TREE: tree(q.x,q.z); break;
                case ROCK: rock(q.x,q.z,q.w,q.d,q.h); break;
                case CAR: car(q.x,q.z,q.panel); break;
                case LOOT: loot(q.x,q.z); break;
                case RUBBLE: rubble(q); break;
                default: break;
            }
        }
        for(CityKit.Piece b:buildings){shadow(b.x,b.z,Math.max(b.w,b.d)*.5f);box(b.x,b.h/2,b.z,b.w,b.h,b.d,buildingBase(b.type));facade(b);roofTrim(b);}
        for(Enemy e:enemies)if(e.hp>0){shadow(e.x,e.z,1);character(e.x,e.z,.62f,.16f,.22f);}
        shadow(px,pz,1);character(px,pz,.62f,.24f,.08f);
    }
    private float[] buildingBase(CityKit.Type t){if(t==CityKit.Type.WAREHOUSE)return new float[]{.30f,.33f,.36f};if(t==CityKit.Type.SHOP)return new float[]{.62f,.43f,.20f};return new float[]{.72f,.67f,.54f};}
    private void movePlayer(float dt){float l=(float)Math.hypot(stickX,stickY);if(l<.08)return;float s=6.2f*dt,fx=(float)Math.sin(yaw),fz=(float)Math.cos(yaw),rx=(float)Math.cos(yaw),rz=-(float)Math.sin(yaw);float nx=px+(fx*(stickY/l)+rx*(stickX/l))*s,nz=pz+(fz*(stickY/l)+rz*(stickX/l))*s;if(!blocked(nx,pz,.55f))px=nx;if(!blocked(px,nz,.55f))pz=nz;}
    private void moveEnemies(float dt){for(Enemy e:enemies)if(e.hp>0){float ax=px-e.x,az=pz-e.z,d=(float)Math.hypot(ax,az);if(d>2.1f){e.x+=ax/d*1.05f*dt;e.z+=az/d*1.05f*dt;}else hp=Math.max(0,hp-(int)Math.ceil(5*dt));}}
    private boolean blocked(float x,float z,float r){for(CityKit.Piece b:buildings)if(x>b.x-b.w/2-r&&x<b.x+b.w/2+r&&z>b.z-b.d/2-r&&z<b.z+b.d/2+r)return true;return x<-33||x>33||z<-33||z>33;}
    private void roadMark(CityKit.Piece q){if(q.w>q.d){for(float x=-30;x<=30;x+=6)box(x,.075f,q.z,.65f,.025f,.08f,.82f,.72f,.32f);}else if(q.d>q.w){for(float z=-30;z<=30;z+=6)box(q.x,.075f,z,.08f,.025f,.65f,.82f,.72f,.32f);}}
    private void tree(float x,float z){shadow(x,z,1.25f);box(x,1.0f,z,.42f,2.0f,.42f,.43f,.25f,.12f);box(x,2.55f,z,2.2f,2.0f,2.2f,.16f,.43f,.08f);box(x,3.45f,z,1.55f,1.15f,1.55f,.20f,.52f,.10f);}
    private void rock(float x,float z,float w,float d,float h){box(x,h/2,z,w,h,d,.32f,.34f,.35f);box(x,h*.78f,z,w*.65f,h*.55f,d*.65f,.40f,.41f,.42f);}
    private void car(float x,float z,int rot){Matrix.setIdentityM(model,0);box(x,.55f,z,2.7f,.8f,1.5f,.78f,.31f,.08f);box(x,1.02f,z,1.55f,.45f,1.2f,.16f,.21f,.23f);box(x-.55f,1.05f,z,0.45f,.3f,1.15f,.08f,.12f,.14f);}
    private void loot(float x,float z){box(x,.35f,z,1.0f,.7f,1.0f,.95f,.68f,.12f);box(x,.74f,z,1.05f,.08f,1.05f,.98f,.82f,.18f);}
    private void rubble(CityKit.Piece q){box(q.x,.5f,q.z,q.w,1.0f,q.d,.36f,.33f,.30f);box(q.x+.8f,.72f,q.z-.3f,q.w*.45f,.65f,q.d*.55f,.44f,.40f,.36f);}
    private void roofTrim(CityKit.Piece b){box(b.x,b.h+.07f,b.z,b.w+.12f,.14f,b.d+.12f,.90f,.84f,.65f);}
    private void character(float x,float z,float s,float r,float g){box(x,.62f,z,s,.95f,s*.72f,.15f,.28f,.55f);box(x,1.35f,z,s*.62f,.55f,s*.62f,.86f,.64f,.45f);}
    private void shadow(float x,float z,float s){box(x,.012f,z,s,.025f,s*.68f,.12f,.12f,.10f);}
    private void box(float x,float y,float z,float w,float h,float d,float r,float g,float b){Matrix.setIdentityM(model,0);Matrix.translateM(model,0,x,y,z);Matrix.scaleM(model,0,w/2,h/2,d/2);Matrix.multiplyMM(mvp,0,vp,0,model,0);GLES20.glUseProgram(colorProgram);GLES20.glUniformMatrix4fv(cm,1,false,mvp,0);GLES20.glUniform4f(cc,r,g,b,1);cube.position(0);GLES20.glEnableVertexAttribArray(ca);GLES20.glVertexAttribPointer(ca,3,GLES20.GL_FLOAT,false,0,cube);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,36);GLES20.glDisableVertexAttribArray(ca);}
    private void box(float x,float y,float z,float w,float h,float d,float[] c){box(x,y,z,w,h,d,c[0],c[1],c[2]);}
    private void facade(CityKit.Piece b){if(textures.isEmpty())return;int id=textures.get(b.panel%textures.size());face(id,b.x,b.z-b.d/2-.02f,b.w,b.h,0);face(id,b.x,b.z+b.d/2+.02f,b.w,b.h,180);face(id,b.x-b.w/2-.02f,b.z,b.d,b.h,90);face(id,b.x+b.w/2+.02f,b.z,b.d,b.h,-90);}
    private void face(int id,float x,float z,float w,float h,float rot){Matrix.setIdentityM(model,0);Matrix.translateM(model,0,x,h/2,z);Matrix.rotateM(model,0,rot,0,1,0);Matrix.scaleM(model,0,w/2,h/2,1);Matrix.multiplyMM(mvp,0,vp,0,model,0);GLES20.glUseProgram(texProgram);GLES20.glUniformMatrix4fv(tm,1,false,mvp,0);GLES20.glUniform1i(ts,0);GLES20.glActiveTexture(GLES20.GL_TEXTURE0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,id);quad.position(0);uv.position(0);GLES20.glEnableVertexAttribArray(ta);GLES20.glVertexAttribPointer(ta,3,GLES20.GL_FLOAT,false,0,quad);GLES20.glEnableVertexAttribArray(tu);GLES20.glVertexAttribPointer(tu,2,GLES20.GL_FLOAT,false,0,uv);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,6);GLES20.glDisableVertexAttribArray(ta);GLES20.glDisableVertexAttribArray(tu);}
    public boolean onTouch(MotionEvent e){float x=e.getX(),y=e.getY();if(e.getActionMasked()==MotionEvent.ACTION_DOWN){lastX=x;lastY=y;moving=x<900;if(!moving)fire();return true;}if(e.getActionMasked()==MotionEvent.ACTION_MOVE){if(moving){stickX=Math.max(-1,Math.min(1,(x-lastX)/120));stickY=Math.max(-1,Math.min(1,(y-lastY)/120));}else{yaw+=(x-lastX)*.004f;if(Math.abs(x-lastX)>5)fire();}lastX=x;lastY=y;return true;}if(e.getActionMasked()==MotionEvent.ACTION_UP||e.getActionMasked()==MotionEvent.ACTION_CANCEL){stickX=stickY=0;moving=false;}return true;}
    private void fire(){if(ammo<=0||fireTimer>0)return;ammo--;fireTimer=.25f;Enemy target=null;float best=999,fx=(float)Math.sin(yaw),fz=(float)Math.cos(yaw);for(Enemy e:enemies)if(e.hp>0){float ax=e.x-px,az=e.z-pz,d=(float)Math.hypot(ax,az);if(d>1&&d<20&&(ax/d)*fx+(az/d)*fz>.84f&&d<best){best=d;target=e;}}if(target!=null){target.hp=0;kills++;}}
    public int getHp(){return hp;} public int getAmmo(){return ammo;} public int getKills(){return kills;} public void pause(){} public void resume(){}
    private int program(String v,String f){int a=shader(GLES20.GL_VERTEX_SHADER,v),b=shader(GLES20.GL_FRAGMENT_SHADER,f),p=GLES20.glCreateProgram();GLES20.glAttachShader(p,a);GLES20.glAttachShader(p,b);GLES20.glLinkProgram(p);return p;}
    private int shader(int t,String s){int q=GLES20.glCreateShader(t);GLES20.glShaderSource(q,s);GLES20.glCompileShader(q);return q;}
    private static FloatBuffer buffer(float[] a){FloatBuffer b=ByteBuffer.allocateDirect(a.length*4).order(ByteOrder.nativeOrder()).asFloatBuffer();b.put(a).position(0);return b;}
    private static final class Enemy{float x,z;int hp=100;Enemy(float x,float z){this.x=x;this.z=z;}}
}
