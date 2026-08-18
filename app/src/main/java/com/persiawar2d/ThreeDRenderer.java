package com.persiawar2d;

import android.content.Context;
import android.opengl.GLES20;
import android.opengl.GLSurfaceView;
import android.opengl.Matrix;
import android.view.MotionEvent;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.FloatBuffer;
import java.util.ArrayList;
import java.util.Random;

/** Lightweight OpenGL ES 2.0 renderer for the 3D migration vertical slice. */
public final class ThreeDRenderer implements GLSurfaceView.Renderer {
    private final Random random = new Random(20260818L);
    private final float[] projection=new float[16], view=new float[16], vp=new float[16], model=new float[16], mvp=new float[16];
    private final ArrayList<Box> buildings=new ArrayList<>();
    private final ArrayList<Box> enemies=new ArrayList<>();
    private final BoxMesh mesh=new BoxMesh();
    private int program, aPos, aNormal, uMvp, uModel, uColor, uLight;
    private float px=0, pz=8, yaw=0, moveX, moveY;
    private float touchStartX,touchStartY; private boolean touchingMove;
    private long lastNs;
    public ThreeDRenderer(Context c){
        for(int z=-24;z<=24;z+=8) for(int x=-24;x<=24;x+=8){
            if(Math.abs(x)<5&&Math.abs(z)<5) continue;
            float h=2.0f+random.nextFloat()*4.5f; buildings.add(new Box(x,h*.5f,z,2.8f,h,2.8f));
        }
        for(int i=0;i<12;i++){double a=i*Math.PI*2/12;enemies.add(new Box((float)Math.cos(a)*12,1,(float)Math.sin(a)*12,1.2f,2f,1.2f));}
    }
    @Override public void onSurfaceCreated(javax.microedition.khronos.egl.EGLConfig config){
        GLES20.glClearColor(.055f,.08f,.10f,1); GLES20.glEnable(GLES20.GL_DEPTH_TEST); GLES20.glDepthFunc(GLES20.GL_LEQUAL);
        program=buildProgram(); aPos=GLES20.glGetAttribLocation(program,"aPos"); aNormal=GLES20.glGetAttribLocation(program,"aNormal");
        uMvp=GLES20.glGetUniformLocation(program,"uMvp");uModel=GLES20.glGetUniformLocation(program,"uModel");uColor=GLES20.glGetUniformLocation(program,"uColor");uLight=GLES20.glGetUniformLocation(program,"uLight");lastNs=System.nanoTime();
    }
    @Override public void onSurfaceChanged(javax.microedition.khronos.opengles.GL10 gl,int w,int h){GLES20.glViewport(0,0,w,h);Matrix.perspectiveM(projection,0,60f,(float)w/Math.max(1,h),.1f,180f);}
    @Override public void onDrawFrame(javax.microedition.khronos.opengles.GL10 gl){
        long now=System.nanoTime();float dt=Math.min(.05f,(now-lastNs)/1_000_000_000f);lastNs=now;float len=(float)Math.hypot(moveX,moveY);
        if(len>.08f){float speed=5.5f*dt;px+=moveX/len*speed;pz+=moveY/len*speed;}
        float camX=px-(float)Math.sin(yaw)*11f,camY=8f,camZ=pz+(float)Math.cos(yaw)*11f;Matrix.setLookAtM(view,0,camX,camY,camZ,px,1f,pz,0,1,0);Matrix.multiplyMM(vp,0,projection,0,view,0);
        GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT|GLES20.GL_DEPTH_BUFFER_BIT);GLES20.glUseProgram(program);GLES20.glUniform3f(uLight,-.35f,1f,.25f);
        drawBox(0,-.15f,0,60,.3f,60,new float[]{.30f,.25f,.18f});
        for(int x=-28;x<=28;x+=4)drawBox(x,.01f,0,.08f,.03f,56,new float[]{.38f,.34f,.26f});
        for(int z=-28;z<=28;z+=4)drawBox(0,.02f,z,56,.03f,.08f,new float[]{.38f,.34f,.26f});
        for(Box b:buildings)drawBox(b.x,b.y,b.z,b.w,b.h,b.d,new float[]{.42f,.34f,.24f});
        for(Box e:enemies)drawBox(e.x,e.y,e.z,e.w,e.h,e.d,new float[]{.55f,.16f,.12f});
        drawBox(px,1f,pz,1.2f,2f,1.2f,new float[]{.72f,.57f,.24f});
    }
    private void drawBox(float x,float y,float z,float w,float h,float d,float[] color){Matrix.setIdentityM(model,0);Matrix.translateM(model,0,x,y,z);Matrix.scaleM(model,0,w,h,d);Matrix.multiplyMM(mvp,0,vp,0,model,0);GLES20.glUniformMatrix4fv(uMvp,1,false,mvp,0);GLES20.glUniformMatrix4fv(uModel,1,false,model,0);GLES20.glUniform3fv(uColor,1,color,0);mesh.draw(aPos,aNormal);}
    public boolean onTouch(MotionEvent e){float x=e.getX(),y=e.getY();
        if(e.getActionMasked()==MotionEvent.ACTION_DOWN){touchStartX=x;touchStartY=y;touchingMove=x<900;updateMove(x,y);return true;}
        if(e.getActionMasked()==MotionEvent.ACTION_MOVE){if(touchingMove)updateMove(x,y);else yaw+=(x-touchStartX)*.004f;touchStartX=x;return true;}
        if(e.getActionMasked()==MotionEvent.ACTION_UP||e.getActionMasked()==MotionEvent.ACTION_CANCEL){moveX=moveY=0;touchingMove=false;return true;}return true;}
    private void updateMove(float x,float y){moveX=Math.max(-1,Math.min(1,(x-touchStartX)/130f));moveY=Math.max(-1,Math.min(1,(y-touchStartY)/130f));}
    public void pause(){} public void resume(){}
    private int buildProgram(){String vs="attribute vec3 aPos;attribute vec3 aNormal;uniform mat4 uMvp;uniform mat4 uModel;varying vec3 vN;void main(){vN=mat3(uModel)*aNormal;gl_Position=uMvp*vec4(aPos,1.0);}";String fs="precision mediump float;uniform vec3 uColor;uniform vec3 uLight;varying vec3 vN;void main(){vec3 n=normalize(vN);float l=.35+.65*max(dot(n,normalize(uLight)),0.0);gl_FragColor=vec4(uColor*l,1.0);}";int v=shader(GLES20.GL_VERTEX_SHADER,vs),f=shader(GLES20.GL_FRAGMENT_SHADER,fs),p=GLES20.glCreateProgram();GLES20.glAttachShader(p,v);GLES20.glAttachShader(p,f);GLES20.glLinkProgram(p);return p;}
    private int shader(int type,String src){int s=GLES20.glCreateShader(type);GLES20.glShaderSource(s,src);GLES20.glCompileShader(s);return s;}
    private static final class Box{final float x,y,z,w,h,d;Box(float x,float y,float z,float w,float h,float d){this.x=x;this.y=y;this.z=z;this.w=w;this.h=h;this.d=d;}}
    private static final class BoxMesh{
        private final FloatBuffer pos,norm;private final int count=36;
        BoxMesh(){float[] p={-1,-1,1,1,-1,1,1,1,1,-1,-1,1,1,1,1,-1,1,1,1,-1,-1,-1,-1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,1,-1,-1,1,1,1,1,1,1,-1,-1,1,1,1,1,-1,-1,1,-1,-1,-1,1,-1,-1,1,-1,1,-1,-1,-1,1,-1,1,-1,-1,1,1,-1,1,1,-1,-1,1,1,-1,1,-1,1,1,1,-1,1,1,1,-1,-1,-1,-1,-1,1,-1,1,1,-1,-1,-1,-1,1,1,-1,1,-1};
            float[] n=new float[108];float[][] qs={{0,0,1},{0,0,-1},{0,1,0},{0,-1,0},{1,0,0},{-1,0,0}};for(int i=0;i<6;i++){float[] q=qs[i];for(int j=0;j<6;j++){int k=(i*6+j)*3;n[k]=q[0];n[k+1]=q[1];n[k+2]=q[2];}}pos=buffer(p);norm=buffer(n);}
        void draw(int ap,int an){pos.position(0);norm.position(0);GLES20.glEnableVertexAttribArray(ap);GLES20.glVertexAttribPointer(ap,3,GLES20.GL_FLOAT,false,0,pos);GLES20.glEnableVertexAttribArray(an);GLES20.glVertexAttribPointer(an,3,GLES20.GL_FLOAT,false,0,norm);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,count);GLES20.glDisableVertexAttribArray(ap);GLES20.glDisableVertexAttribArray(an);}
        private FloatBuffer buffer(float[] a){FloatBuffer b=ByteBuffer.allocateDirect(a.length*4).order(ByteOrder.nativeOrder()).asFloatBuffer();b.put(a).position(0);return b;}
    }
}
