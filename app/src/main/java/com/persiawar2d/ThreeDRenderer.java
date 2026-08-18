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
import java.util.Random;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/** 3D runtime: real 3D geometry with the project's existing building-panel images mapped onto it. */
public final class ThreeDRenderer implements GLSurfaceView.Renderer {
    private final Context context;
    private final Random random=new Random(20260818L);
    private final float[] projection=new float[16],view=new float[16],vp=new float[16],model=new float[16],mvp=new float[16];
    private final ArrayList<Box> buildings=new ArrayList<>(),enemies=new ArrayList<>();
    private final ArrayList<byte[]> panelBytes=new ArrayList<>();
    private final ArrayList<Integer> panelTextures=new ArrayList<>();
    private final BoxMesh mesh=new BoxMesh();
    private int program,texProgram,aPos,aNormal,uMvp,uModel,uColor,uLight,tPos,tUv,tMvp,tColor,tTex,tUseTex;
    private final FloatBuffer quadPos=buffer(new float[]{-1,0,0,1,0,0,1,2,0,-1,0,0,1,2,0,-1,2,0});
    private final FloatBuffer quadUv=buffer(new float[]{0,1,1,1,1,0,0,1,1,0,0,0});
    private float px=0,pz=8,yaw=0,moveX,moveY,touchStartX,touchStartY;private boolean touchingMove;private long lastNs;

    public ThreeDRenderer(Context c){context=c;loadBuildingPanels();
        for(int z=-28;z<=28;z+=8)for(int x=-28;x<=28;x+=8){if(Math.abs(x)<6&&Math.abs(z)<6)continue;float h=2.4f+random.nextFloat()*4.8f;buildings.add(new Box(x,h*.5f,z,2.9f,h,2.9f,buildings.size()%12));}
        for(int i=0;i<12;i++){double a=i*Math.PI*2/12;enemies.add(new Box((float)Math.cos(a)*13,1,(float)Math.sin(a)*13,1.2f,2,1.2f,0));}
    }
    private void loadBuildingPanels(){
        try(InputStream raw=context.getAssets().open("original_packages/kenney_isometric-buildings.zip");ZipInputStream zin=new ZipInputStream(raw)){
            ZipEntry e;while((e=zin.getNextEntry())!=null){if(e.isDirectory())continue;String n=e.getName().toLowerCase();if(!n.endsWith(".png")||!n.contains("buildingtile"))continue;int id=tileId(n);if(id<32)continue;byte[] data=read(zin);Bitmap b=BitmapFactory.decodeByteArray(data,0,data.length);if(b!=null&&b.getWidth()>=48&&b.getHeight()>=48){panelBytes.add(data);if(panelBytes.size()>=12)break;}}
        }catch(Exception ignored){}
    }
    private int tileId(String n){int u=n.lastIndexOf('_'),d=n.lastIndexOf('.');try{return Integer.parseInt(n.substring(u+1,d));}catch(Exception e){return -1;}}
    private byte[] read(ZipInputStream z)throws Exception{ByteArrayOutputStream o=new ByteArrayOutputStream();byte[] b=new byte[8192];int n;while((n=z.read(b))>0)o.write(b,0,n);return o.toByteArray();}

    @Override public void onSurfaceCreated(javax.microedition.khronos.egl.EGLConfig c){
        GLES20.glClearColor(.055f,.075f,.095f,1);GLES20.glEnable(GLES20.GL_DEPTH_TEST);GLES20.glDepthFunc(GLES20.GL_LEQUAL);GLES20.glEnable(GLES20.GL_CULL_FACE);
        program=buildColorProgram();texProgram=buildTextureProgram();aPos=GLES20.glGetAttribLocation(program,"aPos");aNormal=GLES20.glGetAttribLocation(program,"aNormal");uMvp=GLES20.glGetUniformLocation(program,"uMvp");uModel=GLES20.glGetUniformLocation(program,"uModel");uColor=GLES20.glGetUniformLocation(program,"uColor");uLight=GLES20.glGetUniformLocation(program,"uLight");
        tPos=GLES20.glGetAttribLocation(texProgram,"aPos");tUv=GLES20.glGetAttribLocation(texProgram,"aUv");tMvp=GLES20.glGetUniformLocation(texProgram,"uMvp");tColor=GLES20.glGetUniformLocation(texProgram,"uColor");tTex=GLES20.glGetUniformLocation(texProgram,"uTex");tUseTex=GLES20.glGetUniformLocation(texProgram,"uUseTex");
        for(byte[] data:panelBytes){Bitmap b=BitmapFactory.decodeByteArray(data,0,data.length);if(b==null)continue;int[] ids=new int[1];GLES20.glGenTextures(1,ids,0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,ids[0]);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MIN_FILTER,GLES20.GL_LINEAR);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MAG_FILTER,GLES20.GL_LINEAR);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_S,GLES20.GL_CLAMP_TO_EDGE);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_WRAP_T,GLES20.GL_CLAMP_TO_EDGE);GLUtils.texImage2D(GLES20.GL_TEXTURE_2D,0,b,0);b.recycle();panelTextures.add(ids[0]);}
        lastNs=System.nanoTime();
    }
    @Override public void onSurfaceChanged(javax.microedition.khronos.opengles.GL10 gl,int w,int h){GLES20.glViewport(0,0,w,h);Matrix.perspectiveM(projection,0,62f,(float)w/Math.max(1,h),.1f,180f);}
    @Override public void onDrawFrame(javax.microedition.khronos.opengles.GL10 gl){
        long now=System.nanoTime();float dt=Math.min(.05f,(now-lastNs)/1e9f);lastNs=now;float len=(float)Math.hypot(moveX,moveY);if(len>.08f){float speed=5.5f*dt;px+=moveX/len*speed;pz+=moveY/len*speed;}
        float camX=px-(float)Math.sin(yaw)*11f,camY=8f,camZ=pz+(float)Math.cos(yaw)*11f;Matrix.setLookAtM(view,0,camX,camY,camZ,px,1,pz,0,1,0);Matrix.multiplyMM(vp,0,projection,0,view,0);
        GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT|GLES20.GL_DEPTH_BUFFER_BIT);GLES20.glUseProgram(program);GLES20.glUniform3f(uLight,-.35f,1,.25f);
        drawBox(0,-.15f,0,60,.3f,60,new float[]{.30f,.25f,.18f});
        for(int x=-28;x<=28;x+=4)drawBox(x,.01f,0,.08f,.03f,56,new float[]{.38f,.34f,.26f});
        for(int z=-28;z<=28;z+=4)drawBox(0,.02f,z,56,.03f,.08f,new float[]{.38f,.34f,.26f});
        for(Box b:buildings){drawBox(b.x,b.y,b.z,b.w,b.h,b.d,new float[]{.38f,.30f,.22f});drawPanel(b);}
        for(Box e:enemies)drawBox(e.x,e.y,e.z,e.w,e.h,e.d,new float[]{.55f,.16f,.12f});
        drawBox(px,1,pz,1.2f,2,1.2f,new float[]{.72f,.57f,.24f});
    }
    private void drawBox(float x,float y,float z,float w,float h,float d,float[] color){Matrix.setIdentityM(model,0);Matrix.translateM(model,0,x,y,z);Matrix.scaleM(model,0,w,h,d);Matrix.multiplyMM(mvp,0,vp,0,model,0);GLES20.glUseProgram(program);GLES20.glUniformMatrix4fv(uMvp,1,false,mvp,0);GLES20.glUniformMatrix4fv(uModel,1,false,model,0);GLES20.glUniform3fv(uColor,1,color,0);mesh.draw(aPos,aNormal);}
    private void drawPanel(Box b){if(panelTextures.isEmpty())return;int id=panelTextures.get(b.panel%panelTextures.size());Matrix.setIdentityM(model,0);Matrix.translateM(model,0,b.x,b.y-b.h*.02f,b.z-b.d*.51f);Matrix.scaleM(model,0,b.w*.48f,b.h*.48f,1);Matrix.multiplyMM(mvp,0,vp,0,model,0);GLES20.glUseProgram(texProgram);GLES20.glUniformMatrix4fv(tMvp,1,false,mvp,0);GLES20.glUniform4f(tColor,1,1,1,1);GLES20.glUniform1i(tTex,0);GLES20.glUniform1i(tUseTex,1);GLES20.glActiveTexture(GLES20.GL_TEXTURE0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,id);quadPos.position(0);quadUv.position(0);GLES20.glEnableVertexAttribArray(tPos);GLES20.glVertexAttribPointer(tPos,3,GLES20.GL_FLOAT,false,0,quadPos);GLES20.glEnableVertexAttribArray(tUv);GLES20.glVertexAttribPointer(tUv,2,GLES20.GL_FLOAT,false,0,quadUv);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,6);GLES20.glDisableVertexAttribArray(tPos);GLES20.glDisableVertexAttribArray(tUv);}
    public boolean onTouch(MotionEvent e){float x=e.getX(),y=e.getY();if(e.getActionMasked()==MotionEvent.ACTION_DOWN){touchStartX=x;touchStartY=y;touchingMove=x<900;updateMove(x,y);return true;}if(e.getActionMasked()==MotionEvent.ACTION_MOVE){if(touchingMove)updateMove(x,y);else yaw+=(x-touchStartX)*.004f;touchStartX=x;return true;}if(e.getActionMasked()==MotionEvent.ACTION_UP||e.getActionMasked()==MotionEvent.ACTION_CANCEL){moveX=moveY=0;touchingMove=false;return true;}return true;}
    private void updateMove(float x,float y){moveX=Math.max(-1,Math.min(1,(x-touchStartX)/130f));moveY=Math.max(-1,Math.min(1,(y-touchStartY)/130f));}
    public void pause(){}public void resume(){}
    private int buildColorProgram(){String v="attribute vec3 aPos;attribute vec3 aNormal;uniform mat4 uMvp;uniform mat4 uModel;varying vec3 vN;void main(){vN=mat3(uModel)*aNormal;gl_Position=uMvp*vec4(aPos,1.0);}";String f="precision mediump float;uniform vec3 uColor;uniform vec3 uLight;varying vec3 vN;void main(){float l=.35+.65*max(dot(normalize(vN),normalize(uLight)),0.0);gl_FragColor=vec4(uColor*l,1.0);}";return link(v,f);}
    private int buildTextureProgram(){String v="attribute vec3 aPos;attribute vec2 aUv;uniform mat4 uMvp;varying vec2 vUv;void main(){vUv=aUv;gl_Position=uMvp*vec4(aPos,1.0);}";String f="precision mediump float;uniform sampler2D uTex;uniform vec4 uColor;uniform int uUseTex;varying vec2 vUv;void main(){vec4 c=texture2D(uTex,vUv);if(uUseTex==0)c=uColor;gl_FragColor=c*uColor;}";return link(v,f);}
    private int link(String vs,String fs){int v=shader(GLES20.GL_VERTEX_SHADER,vs),f=shader(GLES20.GL_FRAGMENT_SHADER,fs),p=GLES20.glCreateProgram();GLES20.glAttachShader(p,v);GLES20.glAttachShader(p,f);GLES20.glLinkProgram(p);return p;}
    private int shader(int type,String src){int s=GLES20.glCreateShader(type);GLES20.glShaderSource(s,src);GLES20.glCompileShader(s);return s;}
    private static FloatBuffer buffer(float[] a){FloatBuffer b=ByteBuffer.allocateDirect(a.length*4).order(ByteOrder.nativeOrder()).asFloatBuffer();b.put(a).position(0);return b;}
    private static final class Box{final float x,y,z,w,h,d;final int panel;Box(float x,float y,float z,float w,float h,float d,int panel){this.x=x;this.y=y;this.z=z;this.w=w;this.h=h;this.d=d;this.panel=panel;}}
    private static final class BoxMesh{final FloatBuffer pos,norm;final int count=36;BoxMesh(){float[] p={-1,-1,1,1,-1,1,1,1,1,-1,-1,1,1,1,1,-1,1,1,1,-1,-1,-1,-1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,1,-1,-1,1,1,1,1,1,1,-1,-1,1,1,1,1,-1,-1,1,-1,-1,-1,1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,-1,-1,1,1,-1,1,1,-1,-1,1,1,-1,1,-1,1,1,1,-1,1,1,1,-1,-1,-1,-1,-1,1,-1,1,1,-1,-1,-1,-1,1,1,-1,1,-1};float[] n=new float[108];float[][] q={{0,0,1},{0,0,-1},{0,1,0},{0,-1,0},{1,0,0},{-1,0,0}};for(int i=0;i<6;i++)for(int j=0;j<6;j++){int k=(i*6+j)*3;n[k]=q[i][0];n[k+1]=q[i][1];n[k+2]=q[i][2];}pos=buffer(p);norm=buffer(n);}void draw(int ap,int an){pos.position(0);norm.position(0);GLES20.glEnableVertexAttribArray(ap);GLES20.glVertexAttribPointer(ap,3,GLES20.GL_FLOAT,false,0,pos);GLES20.glEnableVertexAttribArray(an);GLES20.glVertexAttribPointer(an,3,GLES20.GL_FLOAT,false,0,norm);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,count);GLES20.glDisableVertexAttribArray(ap);GLES20.glDisableVertexAttribArray(an);}}
}