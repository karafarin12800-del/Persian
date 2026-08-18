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

/** Stable 3D gameplay renderer using the project's building panels as facade textures. */
public final class AdvancedThreeDRenderer implements GLSurfaceView.Renderer {
    private final Context context; private final ArrayList<Building> buildings=new ArrayList<>(); private final ArrayList<Integer> textures=new ArrayList<>(); private final ArrayList<byte[]> pending=new ArrayList<>();
    private final float[] proj=new float[16],view=new float[16],vp=new float[16],model=new float[16],mvp=new float[16];
    private FloatBuffer cubePos,quadPos,quadUv; private int colorProgram,texProgram,cp,cc,cm,tp,tu,tm,ts;
    private float px=0,pz=18,yaw=0,moveX,moveY,lastX,lastY; private boolean moving; private long lastTime;
    public AdvancedThreeDRenderer(Context c){context=c;loadPanels();buildCity();}
    private void loadPanels(){try(InputStream in=context.getAssets().open("original_packages/kenney_isometric-buildings.zip");ZipInputStream z=new ZipInputStream(in)){ZipEntry e;while((e=z.getNextEntry())!=null&&pending.size()<24){String n=e.getName().toLowerCase();if(e.isDirectory()||!n.endsWith(".png")||!n.contains("buildingtile"))continue;byte[] d=read(z);Bitmap b=BitmapFactory.decodeByteArray(d,0,d.length);if(b!=null){pending.add(d);b.recycle();}}}catch(Exception ignored){}}
    private byte[] read(ZipInputStream z)throws Exception{ByteArrayOutputStream o=new ByteArrayOutputStream();byte[] b=new byte[8192];int n;while((n=z.read(b))>0)o.write(b,0,n);return o.toByteArray();}
    private void buildCity(){for(int z=-30;z<=30;z+=10)for(int x=-30;x<=30;x+=10){if(Math.abs(x)<8&&Math.abs(z)<8)continue;buildings.add(new Building(x,z,5,5,2.5f+(buildings.size()%4)*.65f,buildings.size()));}}
    @Override public void onSurfaceCreated(javax.microedition.khronos.egl.EGLConfig cfg){GLES20.glClearColor(.045f,.065f,.085f,1);GLES20.glEnable(GLES20.GL_DEPTH_TEST);colorProgram=link("attribute vec3 A;uniform mat4 M;uniform vec4 C;varying vec4 V;void main(){V=C;gl_Position=M*vec4(A,1.);}","precision mediump float;varying vec4 V;void main(){gl_FragColor=V;}");texProgram=link("attribute vec3 A;attribute vec2 U;uniform mat4 M;varying vec2 V;void main(){V=U;gl_Position=M*vec4(A,1.);}","precision mediump float;uniform sampler2D S;varying vec2 V;void main(){gl_FragColor=texture2D(S,V);}");cp=GLES20.glGetAttribLocation(colorProgram,"A");cc=GLES20.glGetUniformLocation(colorProgram,"C");cm=GLES20.glGetUniformLocation(colorProgram,"M");tp=GLES20.glGetAttribLocation(texProgram,"A");tu=GLES20.glGetAttribLocation(texProgram,"U");tm=GLES20.glGetUniformLocation(texProgram,"M");ts=GLES20.glGetUniformLocation(texProgram,"S");cubePos=buf(new float[]{-1,-1,1,1,-1,1,1,1,1,-1,-1,1,1,1,1,-1,1,1,-1,-1,-1,-1,1,-1,1,1,-1,-1,-1,-1,1,1,-1,1,-1,1,-1,1,1,1,1,1,1,-1,1,1,-1,1,-1,-1,-1,-1,-1,1,-1,1,1,-1,-1,-1,1,-1,1,-1,1,-1,1,1,1,-1,-1,-1,-1,-1,1,-1,1,1,-1,-1,-1,-1,1,1,-1,1,-1});quadPos=buf(new float[]{-1,-1,0,1,-1,0,1,1,0,-1,-1,0,1,1,0,-1,1,0});quadUv=buf(new float[]{0,1,1,1,1,0,0,1,1,0,0,0});for(byte[] d:pending){Bitmap b=BitmapFactory.decodeByteArray(d,0,d.length);if(b==null)continue;int[] id=new int[1];GLES20.glGenTextures(1,id,0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,id[0]);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MIN_FILTER,GLES20.GL_LINEAR);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MAG_FILTER,GLES20.GL_LINEAR);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_S,GLES20.GL_CLAMP_TO_EDGE);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_T,GLES20.GL_CLAMP_TO_EDGE);GLUtils.texImage2D(GLES20.GL_TEXTURE_2D,0,b,0);b.recycle();textures.add(id[0]);}lastTime=System.nanoTime();}
    @Override public void onSurfaceChanged(javax.microedition.khronos.opengles.GL10 g,int w,int h){GLES20.glViewport(0,0,w,h);Matrix.perspectiveM(proj,0,60f,(float)w/Math.max(1,h),.1f,180f);}
    @Override public void onDrawFrame(javax.microedition.khronos.opengles.GL10 g){float dt=Math.min(.05f,(System.nanoTime()-lastTime)/1e9f);lastTime=System.nanoTime();move(dt);float cx=px-(float)Math.sin(yaw)*11,cz=pz+(float)Math.cos(yaw)*11;Matrix.setLookAtM(view,0,cx,7,cz,px,1,pz,0,1,0);Matrix.multiplyMM(vp,0,proj,0,view,0);GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT|GLES20.GL_DEPTH_BUFFER_BIT);drawBox(0,-.2f,0,70,.4f,70,new float[]{.28f,.25f,.20f,1});for(Building b:buildings){drawBox(b.x,b.h/2,b.z,b.w,b.h,b.d,new float[]{.40f,.31f,.22f,1});drawFacade(b);}}
    private void move(float dt){float len=(float)Math.hypot(moveX,moveY);if(len<.1)return;float nx=px+moveX/len*5*dt,nz=pz+moveY/len*5*dt;if(!blocked(nx,pz,1))px=nx;if(!blocked(px,nz,1))pz=nz;}
    private boolean blocked(float x,float z,float r){for(Building b:buildings)if(x>b.x-b.w/2-r&&x<b.x+b.w/2+r&&z>b.z-b.d/2-r&&z<b.z+b.d/2+r)return true;return x<-65||x>65||z<-65||z>65;}
    private void drawBox(float x,float y,float z,float w,float h,float d,float[] color){Matrix.setIdentityM(model,0);Matrix.translateM(model,0,x,y,z);Matrix.scaleM(model,0,w,h,d);Matrix.multiplyMM(mvp,0,vp,0,model,0);GLES20.glUseProgram(colorProgram);GLES20.glUniformMatrix4fv(cm,1,false,mvp,0);GLES20.glUniform4fv(cc,1,color,0);cubePos.position(0);GLES20.glEnableVertexAttribArray(cp);GLES20.glVertexAttribPointer(cp,3,GLES20.GL_FLOAT,false,0,cubePos);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,36);GLES20.glDisableVertexAttribArray(cp);}
    private void drawFacade(Building b){if(textures.isEmpty())return;int id=textures.get(b.panel%textures.size());facade(id,b.x,b.z-b.d/2-.03f,b.w,b.h,0);facade(id,b.x,b.z+b.d/2+.03f,b.w,b.h,180);facade(id,b.x-b.w/2-.03f,b.z,b.d,b.h,90);facade(id,b.x+b.w/2+.03f,b.z,b.d,b.h,-90);}
    private void facade(int tex,float x,float z,float w,float h,float rot){Matrix.setIdentityM(model,0);Matrix.translateM(model,0,x,h/2,z);Matrix.rotateM(model,0,rot,0,1,0);Matrix.scaleM(model,0,w/2,h/2,1);Matrix.multiplyMM(mvp,0,vp,0,model,0);GLES20.glUseProgram(texProgram);GLES20.glUniformMatrix4fv(tm,1,false,mvp,0);GLES20.glUniform1i(ts,0);GLES20.glActiveTexture(GLES20.GL_TEXTURE0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,tex);quadPos.position(0);quadUv.position(0);GLES20.glEnableVertexAttribArray(tp);GLES20.glVertexAttribPointer(tp,3,GLES20.GL_FLOAT,false,0,quadPos);GLES20.glEnableVertexAttribArray(tu);GLES20.glVertexAttribPointer(tu,2,GLES20.GL_FLOAT,false,0,quadUv);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,6);GLES20.glDisableVertexAttribArray(tp);GLES20.glDisableVertexAttribArray(tu);}
    public boolean onTouch(MotionEvent e){float x=e.getX(),y=e.getY();if(e.getActionMasked()==MotionEvent.ACTION_DOWN){lastX=x;lastY=y;moving=x<900;setMove(x,y);return true;}if(e.getActionMasked()==MotionEvent.ACTION_MOVE){if(moving)setMove(x,y);else yaw+=(x-lastX)*.004f;lastX=x;return true;}if(e.getActionMasked()==MotionEvent.ACTION_UP||e.getActionMasked()==MotionEvent.ACTION_CANCEL){moveX=moveY=0;moving=false;}return true;}
    private void setMove(float x,float y){moveX=Math.max(-1,Math.min(1,(x-lastX)/120));moveY=Math.max(-1,Math.min(1,(y-lastY)/120));}
    public void pause(){}public void resume(){}
    private int link(String v,String f){int a=shader(GLES20.GL_VERTEX_SHADER,v),b=shader(GLES20.GL_FRAGMENT_SHADER,f),p=GLES20.glCreateProgram();GLES20.glAttachShader(p,a);GLES20.glAttachShader(p,b);GLES20.glLinkProgram(p);return p;}private int shader(int t,String s){int x=GLES20.glCreateShader(t);GLES20.glShaderSource(x,s);GLES20.glCompileShader(x);return x;}
    private static FloatBuffer buf(float[] a){FloatBuffer b=ByteBuffer.allocateDirect(a.length*4).order(ByteOrder.nativeOrder()).asFloatBuffer();b.put(a).position(0);return b;}
    private static final class Building{final float x,z,w,d,h;final int panel;Building(float x,float z,float w,float d,float h,int p){this.x=x;this.z=z;this.w=w;this.d=d;this.h=h;this.panel=p;}}
}
