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
import java.util.List;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/** Mobile 3D vertical slice: city kit + textured facades + movement + collision + hitscan combat. */
public final class Battle3DRenderer implements GLSurfaceView.Renderer {
    private final Context ctx; private final ArrayList<CityKit.Piece> city=new ArrayList<>(); private final ArrayList<Enemy> enemies=new ArrayList<>();
    private final ArrayList<byte[]> panels=new ArrayList<>(); private final ArrayList<Integer> textures=new ArrayList<>();
    private final float[] p=new float[16],v=new float[16],vp=new float[16],m=new float[16],mm=new float[16]; private final BoxMesh box=new BoxMesh();
    private FloatBuffer qpos=buf(new float[]{-1,-1,0,1,-1,0,1,1,0,-1,-1,0,1,1,0,-1,1,0}); private FloatBuffer quv=buf(new float[]{0,1,1,1,1,0,0,1,1,0,0,0});
    private int cp,tp,ca,cn,cM,cW,cC,cL,ta,tu,tM,tS; private float x=0,z=8,yaw=0,mx,my,sx,sy; private boolean move; private int hp=100,ammo=30,kills=0; private long last;
    public Battle3DRenderer(Context c){ctx=c;city.addAll(new CityKit(20260818L).pieces());for(int i=0;i<12;i++){double a=i*Math.PI*2/12;enemies.add(new Enemy((float)Math.cos(a)*14,(float)Math.sin(a)*14));}loadPanels();}
    private void loadPanels(){try(InputStream in=ctx.getAssets().open("original_packages/kenney_isometric-buildings.zip");ZipInputStream z=new ZipInputStream(in)){ZipEntry e;while((e=z.getNextEntry())!=null&&panels.size()<20){String n=e.getName().toLowerCase();if(!n.contains("buildingtile")||!n.endsWith(".png"))continue;byte[] d=read(z);Bitmap b=BitmapFactory.decodeByteArray(d,0,d.length);if(b!=null){panels.add(d);b.recycle();}}}catch(Exception ignored){}}
    private byte[] read(ZipInputStream z)throws Exception{ByteArrayOutputStream o=new ByteArrayOutputStream();byte[] b=new byte[8192];int n;while((n=z.read(b))>0)o.write(b,0,n);return o.toByteArray();}
    @Override public void onSurfaceCreated(javax.microedition.khronos.egl.EGLConfig c){GLES20.glClearColor(.045f,.065f,.085f,1);GLES20.glEnable(GLES20.GL_DEPTH_TEST);GLES20.glEnable(GLES20.GL_CULL_FACE);cp=link("attribute vec3 A;attribute vec3 N;uniform mat4 M;uniform mat4 W;varying vec3 V;void main(){V=mat3(W)*N;gl_Position=M*vec4(A,1.);}","precision mediump float;uniform vec3 C,L;varying vec3 V;void main(){float d=.35+.65*max(dot(normalize(V),normalize(L)),0.);gl_FragColor=vec4(C*d,1.);}");tp=link("attribute vec3 A;attribute vec2 U;uniform mat4 M;varying vec2 V;void main(){V=U;gl_Position=M*vec4(A,1.);}","precision mediump float;uniform sampler2D S;varying vec2 V;void main(){gl_FragColor=texture2D(S,V);}");ca=GLES20.glGetAttribLocation(cp,"A");cn=GLES20.glGetAttribLocation(cp,"N");cM=GLES20.glGetUniformLocation(cp,"M");cW=GLES20.glGetUniformLocation(cp,"W");cC=GLES20.glGetUniformLocation(cp,"C");cL=GLES20.glGetUniformLocation(cp,"L");ta=GLES20.glGetAttribLocation(tp,"A");tu=GLES20.glGetAttribLocation(tp,"U");tM=GLES20.glGetUniformLocation(tp,"M");tS=GLES20.glGetUniformLocation(tp,"S");for(byte[] d:panels){Bitmap b=BitmapFactory.decodeByteArray(d,0,d.length);int[] id=new int[1];GLES20.glGenTextures(1,id,0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,id[0]);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MIN_FILTER,GLES20.GL_LINEAR);GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D,GLES20.GL_TEXTURE_MAG_FILTER,GLES20.GL_LINEAR);GLUtils.texImage2D(GLES20.GL_TEXTURE_2D,0,b,0);b.recycle();textures.add(id[0]);}last=System.nanoTime();}
    @Override public void onSurfaceChanged(javax.microedition.khronos.opengles.GL10 g,int w,int h){GLES20.glViewport(0,0,w,h);Matrix.perspectiveM(p,0,62,(float)w/Math.max(1,h),.1f,180);}
    @Override public void onDrawFrame(javax.microedition.khronos.opengles.GL10 g){float dt=Math.min(.05f,(System.nanoTime()-last)/1e9f);last=System.nanoTime();update(dt);float cx=x-(float)Math.sin(yaw)*10,cz=z+(float)Math.cos(yaw)*10;Matrix.setLookAtM(v,0,cx,7,cz,x,1,z,0,1,0);Matrix.multiplyMM(vp,0,p,0,v,0);GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT|GLES20.GL_DEPTH_BUFFER_BIT);GLES20.glUseProgram(cp);GLES20.glUniform3f(cL,-.4f,1,.3f);draw(0,-.15f,0,72,.3f,72,new float[]{.28f,.24f,.19f});for(CityKit.Piece b:city)building(b);for(Enemy e:enemies)if(e.hp>0)draw(e.x,1,e.z,1.2f,2,1.2f,new float[]{.65f,.12f,.10f});draw(x,1,z,1.2f,2,1.2f,new float[]{.72f,.57f,.24f});}
    private void update(float dt){float l=(float)Math.hypot(mx,my);if(l>.08){float nx=x+mx/l*5.5f*dt,nz=z+my/l*5.5f*dt;if(!blocked(nx,z,1))x=nx;if(!blocked(x,nz,1))z=nz;}for(Enemy e:enemies)if(e.hp>0){float dx=x-e.x,dz=z-e.z,d=(float)Math.hypot(dx,dz);if(d<13&&d>1.5){e.x+=dx/d*1.1f*dt;e.z+=dz/d*1.1f*dt;}if(d<2.1)e.hitTimer-=dt;if(d<2.1&&e.hitTimer<=0){hp=Math.max(0,hp-5);e.hitTimer=1.2f;}}}
    private boolean blocked(float a,float b,float r){if(a<-34||a>34||b<-34||b>34)return true;for(CityKit.Piece c:city)if(c.type!=CityKit.Type.WALL&&a>c.x-c.w/2-r&&a<c.x+c.w/2+r&&b>c.z-c.d/2-r&&b<c.z+c.d/2+r)return true;return false;}
    private void building(CityKit.Piece b){draw(b.x,b.h/2,b.z,b.w,b.h,b.d,b.type==CityKit.Type.WAREHOUSE?new float[]{.34f,.36f,.38f}:new float[]{.42f,.33f,.24f});if(b.type!=CityKit.Type.WALL&&!textures.isEmpty()){int id=textures.get(b.panel%textures.size());facade(id,b.x,b.z-b.d/2-.02f,b.w,b.h,0);facade(id,b.x,b.z+b.d/2+.02f,b.w,b.h,180);facade(id,b.x-b.w/2-.02f,b.z,b.d,b.h,90);facade(id,b.x+b.w/2+.02f,b.z,b.d,b.h,-90);}}
    private void facade(int id,float a,float b,float w,float h,float r){Matrix.setIdentityM(m,0);Matrix.translateM(m,0,a,h,b);Matrix.rotateM(m,0,r,0,1,0);Matrix.scaleM(m,0,w/2,h/2,1);Matrix.multiplyMM(mm,0,vp,0,m,0);GLES20.glUseProgram(tp);GLES20.glUniformMatrix4fv(tM,1,false,mm,0);GLES20.glUniform1i(tS,0);GLES20.glActiveTexture(GLES20.GL_TEXTURE0);GLES20.glBindTexture(GLES20.GL_TEXTURE_2D,id);qpos.position(0);quv.position(0);GLES20.glEnableVertexAttribArray(ta);GLES20.glVertexAttribPointer(ta,3,GLES20.GL_FLOAT,false,0,qpos);GLES20.glEnableVertexAttribArray(tu);GLES20.glVertexAttribPointer(tu,2,GLES20.GL_FLOAT,false,0,quv);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,6);GLES20.glDisableVertexAttribArray(ta);GLES20.glDisableVertexAttribArray(tu);}
    private void draw(float a,float b,float c,float w,float h,float d,float[] col){Matrix.setIdentityM(m,0);Matrix.translateM(m,0,a,b,c);Matrix.scaleM(m,0,w,h,d);Matrix.multiplyMM(mm,0,vp,0,m,0);GLES20.glUseProgram(cp);GLES20.glUniformMatrix4fv(cM,1,false,mm,0);GLES20.glUniformMatrix4fv(cW,1,false,m,0);GLES20.glUniform3fv(cC,1,col,0);box.draw(ca,cn);}
    public boolean onTouch(MotionEvent e){float a=e.getX(),b=e.getY();if(e.getActionMasked()==MotionEvent.ACTION_DOWN){sx=a;sy=b;move=a<900;setMove(a,b);return true;}if(e.getActionMasked()==MotionEvent.ACTION_MOVE){if(move)setMove(a,b);else yaw+=(a-sx)*.004f;sx=a;return true;}if(e.getActionMasked()==MotionEvent.ACTION_UP){if(!move&&a>=900)fire();mx=my=0;move=false;return true;}if(e.getActionMasked()==MotionEvent.ACTION_CANCEL){mx=my=0;move=false;}return true;}
    private void setMove(float a,float b){mx=Math.max(-1,Math.min(1,(a-sx)/130));my=Math.max(-1,Math.min(1,(b-sy)/130));}
    private void fire(){if(ammo<=0||hp<=0)return;ammo--;float dx=(float)Math.sin(yaw),dz=-(float)Math.cos(yaw);Enemy hit=null;float best=999;for(Enemy e:enemies)if(e.hp>0){float ex=e.x-x,ez=e.z-z,along=ex*dx+ez*dz,cross=Math.abs(ex*dz-ez*dx);if(along>0&&along<24&&cross<1.2&&along<best&&!blockedShot(x,z,e.x,e.z)){best=along;hit=e;}}if(hit!=null){hit.hp-=50;if(hit.hp<=0)kills++;}}
    private boolean blockedShot(float ax,float az,float bx,float bz){return false;}
    public int getHp(){return hp;}public int getAmmo(){return ammo;}public int getKills(){return kills;}
    public void pause(){}public void resume(){}
    private int link(String a,String b){int x=shader(GLES20.GL_VERTEX_SHADER,a),y=shader(GLES20.GL_FRAGMENT_SHADER,b),p=GLES20.glCreateProgram();GLES20.glAttachShader(p,x);GLES20.glAttachShader(p,y);GLES20.glLinkProgram(p);return p;}private int shader(int t,String s){int x=GLES20.glCreateShader(t);GLES20.glShaderSource(x,s);GLES20.glCompileShader(x);return x;}
    private static FloatBuffer buf(float[] a){FloatBuffer b=ByteBuffer.allocateDirect(a.length*4).order(ByteOrder.nativeOrder()).asFloatBuffer();b.put(a).position(0);return b;}
    private static final class Enemy{float x,z,hp=100,hitTimer=1;Enemy(float x,float z){this.x=x;this.z=z;}}
    private static final class BoxMesh{final FloatBuffer p,n;BoxMesh(){float[] a={-1,-1,1,1,-1,1,1,1,1,-1,-1,1,1,1,1,-1,1,1,-1,-1,-1,-1,1,-1,1,1,-1,-1,-1,-1,1,1,-1,1,-1,1,-1,1,1,1,1,-1,-1,1,1,1,-1,1,-1,-1,1,-1,-1,-1,-1,-1,-1,1,-1,1,1,-1,1,1,1,1,1,-1,1,-1,1,1,-1,-1,1,-1,1,-1,1,-1,-1,-1,1,-1,1,-1,-1,-1,-1,1,1,-1,-1,1,1,1,-1,1,-1,-1,-1,1,-1,-1,-1,1,1,-1,-1,1,-1,1};float[] q=new float[108];float[][] nn={{0,0,1},{0,0,-1},{0,1,0},{0,-1,0},{1,0,0},{-1,0,0}};for(int i=0;i<6;i++)for(int j=0;j<6;j++){int k=(i*6+j)*3;q[k]=nn[i][0];q[k+1]=nn[i][1];q[k+2]=nn[i][2];}p=buf(a);n=buf(q);}void draw(int ap,int an){p.position(0);n.position(0);GLES20.glEnableVertexAttribArray(ap);GLES20.glVertexAttribPointer(ap,3,GLES20.GL_FLOAT,false,0,p);GLES20.glEnableVertexAttribArray(an);GLES20.glVertexAttribPointer(an,3,GLES20.GL_FLOAT,false,0,n);GLES20.glDrawArrays(GLES20.GL_TRIANGLES,0,36);GLES20.glDisableVertexAttribArray(ap);GLES20.glDisableVertexAttribArray(an);}}
}
