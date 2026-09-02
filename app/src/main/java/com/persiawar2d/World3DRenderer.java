package com.persiawar2d;

import android.opengl.GLES20;
import android.opengl.GLSurfaceView;
import android.opengl.Matrix;
import com.persiawar2d.game.GameCore;
import com.persiawar2d.world.WorldMap;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.FloatBuffer;

/** Real perspective 3D renderer. GameCore remains the authoritative gameplay model. */
public final class World3DRenderer implements GLSurfaceView.Renderer {
    private static final String VS="attribute vec3 aPosition; uniform mat4 uMvp; void main(){gl_Position=uMvp*vec4(aPosition,1.0);}";
    private static final String FS="precision mediump float; uniform vec4 uColor; void main(){gl_FragColor=uColor;}";
    private final GameCore core;
    private final float[] proj=new float[16],view=new float[16],model=new float[16],mvp=new float[16];
    private int program,posHandle,colorHandle,mvpHandle;
    private int width,height;
    private final FloatBuffer cube;
    private final FloatBuffer quad;

    public World3DRenderer(GameCore core){
        this.core=core;
        cube=buffer(new float[]{
            -1,0,-1, 1,0,-1, 1,0,1, -1,0,1,
            -1,1,-1, 1,1,-1, 1,1,1, -1,1,1,
            -1,0,-1, -1,1,-1, 1,1,-1, 1,0,-1,
            1,0,-1, 1,1,-1, 1,1,1, 1,0,1,
            1,0,1, 1,1,1, -1,1,1, -1,0,1,
            -1,0,1, -1,1,1, -1,1,-1, -1,0,-1,
            -1,1,-1, -1,1,1, 1,1,1, 1,1,-1,
            -1,0,1, 1,0,1, 1,0,-1, -1,0,-1
        });
        quad=buffer(new float[]{-1,0,-1, 1,0,-1, 1,0,1, -1,0,1});
    }

    private FloatBuffer buffer(float[] a){FloatBuffer b=ByteBuffer.allocateDirect(a.length*4).order(ByteOrder.nativeOrder()).asFloatBuffer();b.put(a).position(0);return b;}
    private int shader(int type,String src){int s=GLES20.glCreateShader(type);GLES20.glShaderSource(s,src);GLES20.glCompileShader(s);return s;}

    @Override public void onSurfaceCreated(javax.microedition.khronos.egl.EGLConfig cfg){
        int v=shader(GLES20.GL_VERTEX_SHADER,VS),f=shader(GLES20.GL_FRAGMENT_SHADER,FS);
        program=GLES20.glCreateProgram();GLES20.glAttachShader(program,v);GLES20.glAttachShader(program,f);GLES20.glLinkProgram(program);
        posHandle=GLES20.glGetAttribLocation(program,"aPosition");colorHandle=GLES20.glGetUniformLocation(program,"uColor");mvpHandle=GLES20.glGetUniformLocation(program,"uMvp");
        GLES20.glEnable(GLES20.GL_DEPTH_TEST);GLES20.glDepthFunc(GLES20.GL_LEQUAL);GLES20.glDisable(GLES20.GL_CULL_FACE);GLES20.glClearColor(.035f,.065f,.05f,1f);
    }
    @Override public void onSurfaceChanged(javax.microedition.khronos.opengles.GL10 gl,int w,int h){width=w;height=h;GLES20.glViewport(0,0,w,h);Matrix.perspectiveM(proj,0,55f,Math.max(.6f,w/(float)Math.max(1,h)),8f,10000f);}
    @Override public void onDrawFrame(javax.microedition.khronos.opengles.GL10 gl){
        synchronized(core){core.update(1f/60f, coreInput(core)); GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT|GLES20.GL_DEPTH_BUFFER_BIT); GameCore.Player p=core.player();
            float camX=p.x+720f,camY=1050f,camZ=p.y+860f; Matrix.setLookAtM(view,0,camX,camY,camZ,p.x,0,p.y,0,1,0);
            drawWorld(core.world()); drawCombat();
        }
    }
    private GameCore.Input coreInput(GameCore c){return cInputHolder.get(c);}
    private final java.util.WeakHashMap<GameCore,GameCore.Input> cInputHolder=new java.util.WeakHashMap<>();
    public void bindInput(GameCore.Input input){cInputHolder.put(core,input);}

    private void drawWorld(WorldMap map){
        drawBox(3000, -8,3000, 3000,8,3000, new float[]{.29f,.25f,.18f,1});
        for(WorldMap.Road r:map.roads()){
            if(r.horizontal) drawBox((r.x1+r.x2)*.5f,1,(r.y1+r.y2)*.5f,r.x2-r.x1,r.width,2,new float[]{.13f,.13f,.12f,1});
            else drawBox((r.x1+r.x2)*.5f,1,(r.y1+r.y2)*.5f,2,r.width,r.y2-r.y1,new float[]{.13f,.13f,.12f,1});
        }
        for(WorldMap.Building b:map.buildings()){
            float h=120+(b.style%5)*22;float roof=(b.style%3==0)?0.32f:0.38f;
            drawBox(b.x+b.w*.5f,h*.5f,b.y+b.h*.5f,b.w,h,b.h,new float[]{roof,.25f,.16f,1});
            drawBox(b.x+b.w*.5f,h+5,b.y+b.h*.5f,b.w*1.04f,10,b.h*1.04f,new float[]{.16f,.12f,.09f,1});
        }
        for(WorldMap.Vehicle v:map.vehicles()){
            drawBox(v.x,22,v.y,v.w*1.35f,44,v.h*1.35f,new float[]{.18f,.28f,.30f,1});
            drawBox(v.x,50,v.y,v.w*.72f,12,v.h*.68f,new float[]{.06f,.09f,.10f,1});
        }
        for(WorldMap.Fence f:map.fences()){
            float dx=f.x2-f.x1, dz=f.y2-f.y1, len=(float)Math.hypot(dx,dz);float x=(f.x1+f.x2)*.5f,z=(f.y1+f.y2)*.5f;
            if(Math.abs(dx)>=Math.abs(dz)) drawBox(x,25,z,len,50,7,new float[]{.48f,.39f,.25f,1}); else drawBox(x,25,z,7,50,len,new float[]{.48f,.39f,.25f,1});
        }
        for(WorldMap.Prop t:map.trees()){
            drawBox(t.x,22,t.y,10,44,10,new float[]{.30f,.20f,.10f,1});
            drawBox(t.x,74,t.y,t.size*1.35f,105,t.size*1.35f,new float[]{.15f,.32f,.17f,1});
        }
        for(WorldMap.Prop b:map.bushes()) drawBox(b.x,b.size*.45f,b.y,b.size*1.7f,b.size,b.size*1.7f,new float[]{.20f,.36f,.18f,1});
    }
    private void drawCombat(){
        for(GameCore.Enemy e:core.enemies()){
            if(e.dead) continue; float h=e.type==3?125:e.type==2?105:90;float s=e.type==3?62:e.type==2?52:45;
            drawBox(e.x,h*.5f,e.y,s,h,s,new float[]{e.type==3?.70f:.62f,.18f,.16f,1});
            drawBox(e.x,h+10,e.y,s*1.06f,18,s*1.06f,new float[]{.72f,.55f,.30f,1});
        }
        GameCore.Player p=core.player();
        float ph=132;
        drawBox(p.x,ph*.5f,p.y,58,ph,58,new float[]{.16f,.33f,.28f,1});
        drawBox(p.x,ph+12,p.y,62,20,62,new float[]{.79f,.64f,.37f,1});
        for(GameCore.Projectile b:core.projectiles()) drawBox(b.x,8,b.y,10,10,10,b.fromPlayer?new float[]{1f,.82f,.32f,1}:new float[]{1f,.25f,.20f,1});
        for(GameCore.Grenade g:core.grenades()) drawBox(g.x,12,g.y,20,20,20,new float[]{.25f,.55f,.30f,1});
        for(GameCore.Pickup pck:core.pickups()){
            float[] c=pck.type==GameCore.PickupType.AMMO?new float[]{.85f,.68f,.18f,1}:pck.type==GameCore.PickupType.MEDKIT?new float[]{.88f,.24f,.22f,1}:pck.type==GameCore.PickupType.GRENADE?new float[]{.20f,.62f,.33f,1}:new float[]{.30f,.62f,.88f,1};
            drawBox(pck.x,14,pck.y,28,28,28,c);
        }
    }
    private void drawBox(float x,float y,float z,float sx,float sy,float sz,float[] color){
        Matrix.setIdentityM(model,0);Matrix.translateM(model,0,x,y,z);Matrix.scaleM(model,0,sx*.5f,sy,sz*.5f);Matrix.multiplyMM(mvp,0,view,0,model,0);Matrix.multiplyMM(mvp,0,proj,0,mvp,0);
        GLES20.glUseProgram(program);GLES20.glUniformMatrix4fv(mvpHandle,1,false,mvp,0);GLES20.glUniform4fv(colorHandle,1,color,0);cube.position(0);GLES20.glEnableVertexAttribArray(posHandle);GLES20.glVertexAttribPointer(posHandle,3,GLES20.GL_FLOAT,false,0,cube);GLES20.glDrawArrays(GLES20.GL_TRIANGLE_FAN,0,36);GLES20.glDisableVertexAttribArray(posHandle);
    }
}
