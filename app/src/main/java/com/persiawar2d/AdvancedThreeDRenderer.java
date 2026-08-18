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

/** Playable 3D city scene using the project's artwork as ground and building textures. */
public final class AdvancedThreeDRenderer implements GLSurfaceView.Renderer {
    private final Context context;
    private final ArrayList<Building> buildings=new ArrayList<>();
    private final ArrayList<Enemy> enemies=new ArrayList<>();
    private final ArrayList<Bitmap> pending=new ArrayList<>();
    private final ArrayList<Integer> buildingTextures=new ArrayList<>();
    private final float[] proj=new float[16],view=new float[16],vp=new float[16],model=new float[16],mvp=new float[16];
    private FloatBuffer cube,quad,uv,groundPos,groundUv;
    private int colorProgram,textureProgram,ca,cc,cm,ta,tu,tm,ts,groundTexture;
    private float px=0,pz=18,yaw=0,moveX,moveY,lastX,lastY;
    private boolean moving,aiming;
    private long lastNanos;
    private float fireCooldown;
    private int hp=100,ammo=30,kills=0;

    public AdvancedThreeDRenderer(Context c){
        context=c; loadBuildings();
        CityKit kit=new CityKit(20260818L);
        for(CityKit.Piece q:kit.pieces()) buildings.add(new Building(q.x,q.z,q.w,q.d,q.h,q.panel,q.type));
        for(int i=0;i<8;i++){double a=i*Math.PI/4;enemies.add(new Enemy((float)Math.cos(a)*24,(float)Math.sin(a)*24));}
    }

    private void loadBuildings(){
        try(InputStream in=context.getAssets().open("original_packages/kenney_isometric-buildings.zip");ZipInputStream z=new ZipInputStream(in)){
            ZipEntry e; while((e=z.getNextEntry())!=null&&pending.size()<16){
                String n=e.getName().toLowerCase();
                if(e.isDirectory()||!n.endsWith(".png")||!n.contains("buildingtile")) continue;
                byte[] d=read(z); Bitmap b=BitmapFactory.decodeByteArray(d,0,d.length); if(b!=null) pending.add(b);
            }
        }catch(Exception ignored){}
    }
    private byte[] read(ZipInputStream z)throws Exception{ByteArrayOutputStream o=new ByteArrayOutputStream();byte[] b=new byte[8192];int n;while((n=z.read(b))>0)o.write(b,0,n);return o.toByteArray();}
    private Bitmap loadGround(){try(InputStream in=context.getAssets().open("references/world_texture_ref.jpg")){return BitmapFactory.decodeStream(in);}catch(Exception ignored){return null;}}

    @Override public void onSurfaceCreated(GL10 gl,EGLConfig config){
        GLES20.glClearColor(.035f,.045f,.055f,1); GLES20.glEnable(GLES20.GL_DEPTH_TEST); GLES20.glEnable(GLES20.GL_CULL_FACE);
        colorProgram=program("attribute vec3 A;uniform mat4 M;void main(){gl_Position=M*vec4(A,1.0);}","precision mediump float;uniform vec4 C;void main(){gl_FragColor=C;}");
        textureProgram=program("attribute vec3 A;attribute vec2 U;uniform mat4 M;varying vec2 T;void main(){T=U;gl_Position=M*vec4(A,1.0);}","precision mediump float;uniform sampler2D S;varying vec2 T;void main(){gl_FragColor=texture2D(S,T);}");
        ca=GLES20.glGetAttribLocation(colorProgram,"A");cc=GLES20.glGetUniformLocation(colorProgram,"C");cm=GLES20.glGetUniformLocation(colorProgram,"M");
        ta=GLES20.glGetAttribLocation(textureProgram,"A");tu=GLES20.glGetAttribLocation(textureProgram,"U");tm=GLES20.glGetUniformLocation(textureProgram,"M");ts=GLES20.glGetUniformLocation(textureProgram,"S");
        cube=buf(makeCube()); quad=buf(new float[]{-1,-1,0,1,-1,0,1,1,0,-1,-1,0,1,1,0,-1,1,0}); uv=buf(new float[]{0,1,1,1,1,0,0,1,1,0,0,0});
        groundPos=buf(new float[]{-35,0,-35,35,0,-35,35,0,35,-35,0,-35,35,0,35,-35,0,35}); groundUv=buf(new float[]{0,1,1,1,1,0,0,1,1,0,0,0});
        for(Bitmap b:pending){int[] id=new int[1];GLES20.glGenTextures(1,id,0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,id[0]);setTextureParams();GLUtils.texImage2D(GLES20.GL_TEXTURE_2D,0,b,0);buildingTextures.add(id[0]);b.recycle();} pending.clear();
        Bitmap ground=loadGround(); if(ground!=null){int[] id=new int[1];GLES20.glGenTextures(1,id,0);groundTexture=id[0];GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,groundTexture);setTextureParams();GLUtils.texImage2D(GLES20.GL_TEXTURE_2D,0,ground,0);ground.recycle();}
        lastNanos=System.nanoTime();
    }
    private void setTextureParams(){GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MIN_FILTER,GLES20.GL_LINEAR);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MAG_FILTER,GLES20.GL_LINEAR);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_S,GLES20.GL_CLAMP_TO_EDGE);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_T,GLES20.GL_CLAMP_TO_EDGE);}

    @Override public void onSurfaceChanged(GL10 gl,int w,int h){GLES20.glViewport(0,0,w,h);Matrix.perspectiveM(proj,0,52f,(float)w/Math.max(1,h),.1f,180f);}

    @Override public void onDrawFrame(GL10 gl){
        long now=System.nanoTime();float dt=Math.min(.05f,(now-lastNanos)/1_000_000_000f);lastNanos=now;fireCooldown=Math.max(0,fireCooldown-dt);
        movePlayer(dt); updateEnemies(dt);
        float distance=13f; float camX=px-(float)Math.sin(yaw)*distance; float camZ=pz+(float)Math.cos(yaw)*distance;
        Matrix.setLookAtM(view,0,camX,9.5f,camZ,px,0,pz,0,1,0); Matrix.multiplyMM(vp,0,proj,0,view,0);
        GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT|GLES20.GL_DEPTH_BUFFER_BIT);
        drawGround();
        for(Building b:buildings){box(b.x,b.h*.5f,b.z,b.w,b.h,b.d,.38f,.30f,.23f);facade(b);}
        for(Enemy e:enemies)if(e.hp>0)box(e.x,1,e.z,1.0f,2.0f,1.0f,.65f,.12f,.10f);
        box(px,1,pz,1.0f,2.0f,1.0f,.72f,.57f,.24f);
    }

    private void movePlayer(float dt){
        float len=(float)Math.hypot(moveX,moveY); if(len<.05f)return;
        float forwardX=(float)Math.sin(yaw),forwardZ=(float)Math.cos(yaw),rightX=(float)Math.cos(yaw),rightZ=-(float)Math.sin(yaw);
        float vx=(forwardX*moveY+rightX*moveX)/len, vz=(forwardZ*moveY+rightZ*moveX)/len;
        float nx=px+vx*5.5f*dt,nz=pz+vz*5.5f*dt;
        if(!blocked(nx,pz,.7f))px=nx; if(!blocked(px,nz,.7f))pz=nz;
    }
    private void updateEnemies(float dt){for(Enemy e:enemies){if(e.hp<=0)continue;float ax=px-e.x,az=pz-e.z,d=(float)Math.hypot(ax,az);if(d>2){e.x+=ax/d*1.45f*dt;e.z+=az/d*1.45f*dt;}else hp=Math.max(0,hp-(int)Math.ceil(8f*dt));}}
    private boolean blocked(float x,float z,float r){for(Building b:buildings)if(b.type!=CityKit.Type.WALL&&x>b.x-b.w/2-r&&x<b.x+b.w/2+r&&z>b.z-b.d/2-r&&z<b.z+b.d/2+r)return true;return x<-34||x>34||z<-34||z>34;}

    private void drawGround(){
        if(groundTexture==0){box(0,-.2f,0,35,.4f,35,.18f,.18f,.18f);return;}
        Matrix.setIdentityM(model,0);Matrix.multiplyMM(mvp,0,vp,0,model,0);GLES20.glUseProgram(textureProgram);GLES20.glUniformMatrix4fv(tm,1,false,mvp,0);GLES20.glUniform1i(ts,0);GLES20.glActiveTexture(GLES20.GL_TEXTURE0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,groundTexture);groundPos.position(0);groundUv.position(0);GLES20.glEnableVertexAttribArray(ta);GLES20.glVertexAttribPointer(ta,3,GLES20.GL_FLOAT,false,0,groundPos);GLES20.glEnableVertexAttribArray(tu);GLES20.glVertexAttribPointer(tu,2,GLES20.GL_FLOAT,false,0,groundUv);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,6);GLES20.glDisableVertexAttribArray(ta);GLES20.glDisableVertexAttribArray(tu);
    }
    private void box(float x,float y,float z,float w,float h,float d,float r,float g,float b){Matrix.setIdentityM(model,0);Matrix.translateM(model,0,x,y,z);Matrix.scaleM(model,0,w,h,d);Matrix.multiplyMM(mvp,0,vp,0,model,0);GLES20.glUseProgram(colorProgram);GLES20.glUniformMatrix4fv(cm,1,false,mvp,0);GLES20.glUniform4f(cc,r,g,b,1);cube.position(0);GLES20.glEnableVertexAttribArray(ca);GLES20.glVertexAttribPointer(ca,3,GLES20.GL_FLOAT,false,0,cube);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,36);GLES20.glDisableVertexAttribArray(ca);}
    private void facade(Building b){if(buildingTextures.isEmpty())return;int id=buildingTextures.get(b.panel%buildingTextures.size());face(id,b.x,b.z-b.d/2-.025f,b.w,b.h,0);face(id,b.x,b.z+b.d/2+.025f,b.w,b.h,180);face(id,b.x-b.w/2-.025f,b.z,b.d,b.h,90);face(id,b.x+b.w/2+.025f,b.z,b.d,b.h,-90);}
    private void face(int id,float x,float z,float w,float h,float rot){Matrix.setIdentityM(model,0);Matrix.translateM(model,0,x,h/2,z);Matrix.rotateM(model,0,rot,0,1,0);Matrix.scaleM(model,0,w/2,h/2,1);Matrix.multiplyMM(mvp,0,vp,0,model,0);GLES20.glUseProgram(textureProgram);GLES20.glUniformMatrix4fv(tm,1,false,mvp,0);GLES20.glUniform1i(ts,0);GLES20.glActiveTexture(GLES20.GL_TEXTURE0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,id);quad.position(0);uv.position(0);GLES20.glEnableVertexAttribArray(ta);GLES20.glVertexAttribPointer(ta,3,GLES20.GL_FLOAT,false,0,quad);GLES20.glEnableVertexAttribArray(tu);GLES20.glVertexAttribPointer(tu,2,GLES20.GL_FLOAT,false,0,uv);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,6);GLES20.glDisableVertexAttribArray(ta);GLES20.glDisableVertexAttribArray(tu);}

    public boolean onTouch(MotionEvent e){
        float x=e.getX(),y=e.getY();
        if(e.getActionMasked()==MotionEvent.ACTION_DOWN){lastX=x;lastY=y;if(x<900){moving=true;moveX=moveY=0;}else{aiming=true;fire();}return true;}
        if(e.getActionMasked()==MotionEvent.ACTION_MOVE){if(moving){moveX=Math.max(-1,Math.min(1,(x-lastX)/90f));moveY=Math.max(-1,Math.min(1,(y-lastY)/90f));}else if(aiming){yaw+=(x-lastX)*.0045f;if(Math.abs(x-lastX)>5)fire();}lastX=x;lastY=y;return true;}
        if(e.getActionMasked()==MotionEvent.ACTION_UP||e.getActionMasked()==MotionEvent.ACTION_CANCEL){moveX=moveY=0;moving=false;aiming=false;}return true;
    }
    private void fire(){if(ammo<=0||fireCooldown>0)return;ammo--;fireCooldown=.22f;float fx=(float)Math.sin(yaw),fz=(float)Math.cos(yaw);Enemy best=null;float bestDist=999;for(Enemy e:enemies)if(e.hp>0){float ax=e.x-px,az=e.z-pz,d=(float)Math.hypot(ax,az);if(d<.5f||d>20)continue;float dot=(ax/d)*fx+(az/d)*fz;if(dot>.86f&&d<bestDist){best=e;bestDist=d;}}if(best!=null){best.hp=0;kills++;}}
    public int getHp(){return hp;}public int getAmmo(){return ammo;}public int getKills(){return kills;}public void pause(){}public void resume(){}
    private int program(String v,String f){int a=shader(GLES20.GL_VERTEX_SHADER,v),b=shader(GLES20.GL_FRAGMENT_SHADER,f),p=GLES20.glCreateProgram();GLES20.glAttachShader(p,a);GLES20.glAttachShader(p,b);GLES20.glLinkProgram(p);return p;}
    private int shader(int type,String source){int s=GLES20.glCreateShader(type);GLES20.glShaderSource(s,source);GLES20.glCompileShader(s);return s;}
    private static FloatBuffer buf(float[] a){FloatBuffer b=ByteBuffer.allocateDirect(a.length*4).order(ByteOrder.nativeOrder()).asFloatBuffer();b.put(a).position(0);return b;}
    private static float[] makeCube(){float[][] f={{-1,-1,1,1,-1,1,1,1,1,-1,-1,1,1,1,1,-1,1,1},{1,-1,-1,-1,-1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,1,-1},{-1,1,1,1,1,1,1,1,-1,-1,1,1,1,1,-1,-1,1,-1},{-1,-1,-1,1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,-1,-1,1},{1,-1,1,1,-1,-1,1,1,-1,1,-1,1,1,1,-1,1,1,1},{-1,-1,-1,-1,-1,1,-1,1,1,-1,-1,-1,-1,1,1,-1,1,-1}};float[] o=new float[108];int q=0;for(float[] a:f)for(float n:a)o[q++]=n;return o;}
    private static final class Building{final float x,z,w,d,h;final int panel;final CityKit.Type type;Building(float x,float z,float w,float d,float h,int p,CityKit.Type t){this.x=x;this.z=z;this.w=w;this.d=d;this.h=h;this.panel=p;this.type=t;}}
    private static final class Enemy{float x,z;int hp=100;Enemy(float x,float z){this.x=x;this.z=z;}}
}
