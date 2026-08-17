package com.persiawar2d;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.opengl.GLES20;
import android.opengl.GLUtils;
import android.opengl.Matrix;

import java.io.InputStream;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.FloatBuffer;

import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

/**
 * True world-space 2.5D/3D presentation pass.
 * Buildings are real 3D volumes, the camera is orthographic, and the player is
 * a billboard anchored to the ground. The existing gameplay model can later
 * feed these same world transforms without changing the renderer.
 */
public final class GlSceneRenderer implements android.opengl.GLSurfaceView.Renderer {
    private static final String VERTEX =
            "uniform mat4 uMvp;\n" +
            "attribute vec3 aPos;\n" +
            "attribute vec4 aColor;\n" +
            "varying vec4 vColor;\n" +
            "void main(){ gl_Position=uMvp*vec4(aPos,1.0); vColor=aColor; }";
    private static final String FRAGMENT =
            "precision mediump float;\n" +
            "varying vec4 vColor;\n" +
            "void main(){ gl_FragColor=vColor; }";
    private static final String TEX_VERTEX =
            "uniform mat4 uMvp;\n" +
            "attribute vec3 aPos;\n" +
            "attribute vec2 aUv;\n" +
            "varying vec2 vUv;\n" +
            "void main(){ gl_Position=uMvp*vec4(aPos,1.0); vUv=aUv; }";
    private static final String TEX_FRAGMENT =
            "precision mediump float;\n" +
            "uniform sampler2D uTex;\n" +
            "varying vec2 vUv;\n" +
            "void main(){ vec4 c=texture2D(uTex,vUv); if(c.a<0.08) discard; gl_FragColor=c; }";

    private final Context context;
    private final float[] projection = new float[16];
    private final float[] view = new float[16];
    private final float[] model = new float[16];
    private final float[] mvp = new float[16];
    private final float[] temp = new float[16];
    private int colorProgram;
    private int textureProgram;
    private int colorMvp;
    private int colorPos;
    private int colorColor;
    private int textureMvp;
    private int texturePos;
    private int textureUv;
    private int textureSampler;
    private int characterTexture;
    private int width;
    private int height;
    private float panX;
    private float panY;
    private FloatBuffer quadVertices;
    private FloatBuffer quadUvs;
    private final FloatBuffer cubeVertices;
    private final FloatBuffer cubeColors;

    public GlSceneRenderer(Context context) {
        this.context = context;
        cubeVertices = directBuffer(new float[36 * 3]);
        cubeColors = directBuffer(new float[36 * 4]);
    }

    @Override
    public void onSurfaceCreated(GL10 gl, EGLConfig config) {
        GLES20.glClearColor(0.12f, 0.15f, 0.12f, 1f);
        GLES20.glEnable(GLES20.GL_DEPTH_TEST);
        GLES20.glDepthFunc(GLES20.GL_LEQUAL);
        GLES20.glEnable(GLES20.GL_CULL_FACE);
        GLES20.glCullFace(GLES20.GL_BACK);

        colorProgram = program(VERTEX, FRAGMENT);
        textureProgram = program(TEX_VERTEX, TEX_FRAGMENT);
        colorMvp = GLES20.glGetUniformLocation(colorProgram, "uMvp");
        colorPos = GLES20.glGetAttribLocation(colorProgram, "aPos");
        colorColor = GLES20.glGetAttribLocation(colorProgram, "aColor");
        textureMvp = GLES20.glGetUniformLocation(textureProgram, "uMvp");
        texturePos = GLES20.glGetAttribLocation(textureProgram, "aPos");
        textureUv = GLES20.glGetAttribLocation(textureProgram, "aUv");
        textureSampler = GLES20.glGetUniformLocation(textureProgram, "uTex");
        characterTexture = loadCharacterTexture();
        makeBillboardBuffers();
        makeCubeBuffers();
    }

    @Override
    public void onSurfaceChanged(GL10 gl, int width, int height) {
        this.width = width;
        this.height = height;
        GLES20.glViewport(0, 0, width, height);
        float aspect = width / (float) Math.max(1, height);
        float span = 18f;
        GlMath.orthoForLandscape(projection, aspect, span);
    }

    @Override
    public void onDrawFrame(GL10 gl) {
        GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT | GLES20.GL_DEPTH_BUFFER_BIT);
        Matrix.setLookAtM(view, 0,
                13f + panX, 16f + panY, 19f,
                8f + panX, 0f, 8f + panY,
                0f, 1f, 0f);
        drawGround();
        drawRoads();
        drawBuildings();
        drawTrees();
        drawPlayer();
    }

    public void pan(float dx, float dy) {
        panX -= dx / 120f;
        panY += dy / 120f;
        panX = Math.max(-6f, Math.min(6f, panX));
        panY = Math.max(-6f, Math.min(6f, panY));
    }

    private void drawGround() {
        drawBox(8f, -0.35f, 8f, 16.5f, 0.25f, 16.5f,
                new float[]{0.48f, 0.43f, 0.30f, 1f});
    }

    private void drawRoads() {
        float roadY = 0f;
        drawBox(8f, roadY, 4.1f, 16.5f, 0.05f, 1.0f,
                new float[]{0.15f, 0.15f, 0.14f, 1f});
        drawBox(8f, roadY + 0.01f, 4.1f, 1.0f, 0.06f, 16.5f,
                new float[]{0.15f, 0.15f, 0.14f, 1f});
    }

    private void drawBuildings() {
        float[][] b = {
                {2f, 2f, 2.2f, 1.8f, 1.6f, 0.74f, 0.33f, 0.30f},
                {6f, 2f, 2.4f, 1.9f, 1.8f, 0.83f, 0.83f, 0.77f},
                {10f, 2f, 2.1f, 1.8f, 1.5f, 0.61f, 0.25f, 0.24f},
                {2f, 7f, 2.0f, 2.0f, 1.7f, 0.78f, 0.73f, 0.63f},
                {10f, 7f, 2.5f, 2.1f, 1.9f, 0.74f, 0.76f, 0.72f},
                {2f, 12f, 2.2f, 1.9f, 1.7f, 0.69f, 0.38f, 0.35f},
                {6f, 12f, 2.5f, 1.9f, 1.5f, 0.82f, 0.81f, 0.76f},
                {10f, 12f, 2.2f, 2.0f, 1.8f, 0.56f, 0.56f, 0.57f}
        };
        for (float[] q : b) {
            drawBuilding(q[0], q[1], q[2], q[3], q[4], q[5], q[6], q[7]);
        }
    }

    private void drawBuilding(float x, float y, float w, float d, float h,
                              float r, float g, float b) {
        drawBox(x, h * 0.5f, y, w, h, d, new float[]{r, g, b, 1f});
        drawRoof(x, h + 0.10f, y, w + 0.18f, d + 0.18f, r * 0.82f, g * 0.55f, b * 0.50f);
        drawWindows(x, y, w, d, h);
    }

    private void drawRoof(float x, float y, float z, float w, float d,
                          float r, float g, float b) {
        drawBox(x, y, z, w, 0.18f, d, new float[]{r, g, b, 1f});
    }

    private void drawWindows(float x, float z, float w, float d, float h) {
        // Small emissive-looking facade panels; kept deliberately low-poly.
        float[] xs = {x - w * 0.28f, x + w * 0.28f};
        for (float px : xs) {
            drawBox(px, h * 0.62f, z - d * 0.505f, 0.23f, 0.28f, 0.03f,
                    new float[]{0.38f, 0.64f, 0.76f, 1f});
        }
    }

    private void drawTrees() {
        float[][] t = {{4f, 5f, 0.75f}, {8.1f, 5.2f, 0.95f}, {13f, 5.5f, 0.90f},
                {4.2f, 10.1f, 0.85f}, {12.6f, 10.4f, 1.0f}};
        for (float[] q : t) {
            drawBox(q[0], 0.45f, q[1], 0.18f, 0.9f, 0.18f,
                    new float[]{0.38f, 0.23f, 0.13f, 1f});
            drawBox(q[0], 1.15f, q[1], q[2] * 1.5f, 1.2f, q[2] * 1.5f,
                    new float[]{0.20f, 0.48f, 0.18f, 1f});
        }
    }

    private void drawPlayer() {
        if (characterTexture == 0) return;
        Matrix.setIdentityM(model, 0);
        Matrix.translateM(model, 0, 8f, 0f, 8f);
        Matrix.multiplyMM(temp, 0, view, 0, model, 0);
        Matrix.multiplyMM(mvp, 0, projection, 0, temp, 0);

        GLES20.glUseProgram(textureProgram);
        GLES20.glUniformMatrix4fv(textureMvp, 1, false, mvp, 0);
        GLES20.glEnableVertexAttribArray(texturePos);
        GLES20.glVertexAttribPointer(texturePos, 3, GLES20.GL_FLOAT, false, 0, quadVertices);
        GLES20.glEnableVertexAttribArray(textureUv);
        GLES20.glVertexAttribPointer(textureUv, 2, GLES20.GL_FLOAT, false, 0, quadUvs);
        GLES20.glActiveTexture(GLES20.GL_TEXTURE0);
        GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, characterTexture);
        GLES20.glUniform1i(textureSampler, 0);
        GLES20.glDisable(GLES20.GL_DEPTH_TEST);
        GLES20.glDrawArrays(GLES20.GL_TRIANGLE_STRIP, 0, 4);
        GLES20.glEnable(GLES20.GL_DEPTH_TEST);
        GLES20.glDisableVertexAttribArray(texturePos);
        GLES20.glDisableVertexAttribArray(textureUv);
    }

    private void drawBox(float x, float y, float z, float w, float h, float d, float[] color) {
        float[] v = {
                x-w/2,y-h/2,z-d/2, x+w/2,y-h/2,z-d/2, x+w/2,y+h/2,z-d/2,
                x-w/2,y-h/2,z-d/2, x+w/2,y+h/2,z-d/2, x-w/2,y+h/2,z-d/2,
                x-w/2,y-h/2,z+d/2, x+w/2,y-h/2,z+d/2, x+w/2,y+h/2,z+d/2,
                x-w/2,y-h/2,z+d/2, x+w/2,y+h/2,z+d/2, x-w/2,y+h/2,z+d/2,
                x-w/2,y-h/2,z-d/2, x-w/2,y+h/2,z-d/2, x-w/2,y+h/2,z+d/2,
                x-w/2,y-h/2,z-d/2, x-w/2,y+h/2,z+d/2, x-w/2,y-h/2,z+d/2,
                x+w/2,y-h/2,z-d/2, x+w/2,y+h/2,z-d/2, x+w/2,y+h/2,z+d/2,
                x+w/2,y-h/2,z-d/2, x+w/2,y+h/2,z+d/2, x+w/2,y-h/2,z+d/2,
                x-w/2,y+h/2,z-d/2, x+w/2,y+h/2,z-d/2, x+w/2,y+h/2,z+d/2,
                x-w/2,y+h/2,z-d/2, x+w/2,y+h/2,z+d/2, x-w/2,y+h/2,z+d/2,
                x-w/2,y-h/2,z-d/2, x+w/2,y-h/2,z-d/2, x+w/2,y-h/2,z+d/2,
                x-w/2,y-h/2,z-d/2, x+w/2,y-h/2,z+d/2, x-w/2,y-h/2,z+d/2
        };
        cubeVertices.clear(); cubeVertices.put(v).position(0);
        float[] colors = new float[36 * 4];
        float[][] face = {{color[0]*0.78f,color[1]*0.78f,color[2]*0.78f,1f},{color[0]*1.04f,color[1]*1.04f,color[2]*1.04f,1f},{color[0]*0.72f,color[1]*0.72f,color[2]*0.72f,1f},{color[0]*0.92f,color[1]*0.92f,color[2]*0.92f,1f},{Math.min(1f,color[0]*1.12f),Math.min(1f,color[1]*1.12f),Math.min(1f,color[2]*1.12f),1f},{color[0]*0.60f,color[1]*0.60f,color[2]*0.60f,1f}};
        int pos = 0;
        for (float[] fc : face) for (int i = 0; i < 6; i++) { colors[pos++] = fc[0]; colors[pos++] = fc[1]; colors[pos++] = fc[2]; colors[pos++] = 1f; }
        cubeColors.clear(); cubeColors.put(colors).position(0);
        Matrix.setIdentityM(model, 0);
        Matrix.translateM(model, 0, 0f, 0f, 0f);
        Matrix.multiplyMM(temp, 0, view, 0, model, 0);
        Matrix.multiplyMM(mvp, 0, projection, 0, temp, 0);
        GLES20.glUseProgram(colorProgram);
        GLES20.glUniformMatrix4fv(colorMvp, 1, false, mvp, 0);
        GLES20.glEnableVertexAttribArray(colorPos);
        GLES20.glVertexAttribPointer(colorPos, 3, GLES20.GL_FLOAT, false, 0, cubeVertices);
        GLES20.glEnableVertexAttribArray(colorColor);
        GLES20.glVertexAttribPointer(colorColor, 4, GLES20.GL_FLOAT, false, 0, cubeColors);
        GLES20.glDrawArrays(GLES20.GL_TRIANGLES, 0, 36);
        GLES20.glDisableVertexAttribArray(colorPos);
        GLES20.glDisableVertexAttribArray(colorColor);
    }

    private void makeCubeBuffers() {
        cubeVertices.clear();
        cubeColors.clear();
    }

    private void makeBillboardBuffers() {
        quadVertices = directBuffer(new float[]{-0.65f,0f,0f, 0.65f,0f,0f, -0.65f,1.55f,0f, 0.65f,1.55f,0f});
        quadUvs = directBuffer(new float[]{0f,1f, 1f,1f, 0f,0f, 1f,0f});
    }

    private int loadCharacterTexture() {
        try (InputStream in = context.getAssets().open("player/king_sprite_sheet.png")) {
            Bitmap sheet = BitmapFactory.decodeStream(in);
            Bitmap frame = KingFrameLayout.frame(sheet, 1, 0, 1);
            int[] tex = new int[1];
            GLES20.glGenTextures(1, tex, 0);
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, tex[0]);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MIN_FILTER, GLES20.GL_LINEAR);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MAG_FILTER, GLES20.GL_LINEAR);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_WRAP_S, GLES20.GL_CLAMP_TO_EDGE);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_WRAP_T, GLES20.GL_CLAMP_TO_EDGE);
            GLUtils.texImage2D(GLES20.GL_TEXTURE_2D, 0, frame, 0);
            frame.recycle();
            sheet.recycle();
            return tex[0];
        } catch (Exception e) {
            return 0;
        }
    }

    private FloatBuffer directBuffer(float[] data) {
        ByteBuffer b = ByteBuffer.allocateDirect(data.length * 4).order(ByteOrder.nativeOrder());
        FloatBuffer f = b.asFloatBuffer();
        f.put(data).position(0);
        return f;
    }

    private int program(String v, String f) {
        int vs = shader(GLES20.GL_VERTEX_SHADER, v);
        int fs = shader(GLES20.GL_FRAGMENT_SHADER, f);
        int p = GLES20.glCreateProgram();
        GLES20.glAttachShader(p, vs);
        GLES20.glAttachShader(p, fs);
        GLES20.glLinkProgram(p);
        int[] ok = new int[1]; GLES20.glGetProgramiv(p, GLES20.GL_LINK_STATUS, ok, 0);
        if (ok[0] == 0) throw new IllegalStateException(GLES20.glGetProgramInfoLog(p));
        return p;
    }

    private int shader(int type, String source) {
        int s = GLES20.glCreateShader(type);
        GLES20.glShaderSource(s, source);
        GLES20.glCompileShader(s);
        int[] ok = new int[1]; GLES20.glGetShaderiv(s, GLES20.GL_COMPILE_STATUS, ok, 0);
        if (ok[0] == 0) throw new IllegalStateException(GLES20.glGetShaderInfoLog(s));
        return s;
    }
}
