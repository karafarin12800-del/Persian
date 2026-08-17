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
 * Real OpenGL ES 2.0 world-space renderer for the isolated 2.5D branch.
 * The scene is a low-poly 3D volume with an orthographic camera; the player
 * remains a billboard so the existing sprite art can be reused.
 */
public final class GlSceneRenderer implements android.opengl.GLSurfaceView.Renderer {
    private static final String VERTEX =
            "uniform mat4 uMvp;\n" +
            "attribute vec3 aPos;\n" +
            "attribute vec4 aColor;\n" +
            "varying vec4 vColor;\n" +
            "void main(){gl_Position=uMvp*vec4(aPos,1.0);vColor=aColor;}";
    private static final String FRAGMENT =
            "precision mediump float;\n" +
            "varying vec4 vColor;\n" +
            "void main(){gl_FragColor=vColor;}";
    private static final String TEX_VERTEX =
            "uniform mat4 uMvp;\n" +
            "attribute vec3 aPos;\n" +
            "attribute vec2 aUv;\n" +
            "varying vec2 vUv;\n" +
            "void main(){gl_Position=uMvp*vec4(aPos,1.0);vUv=aUv;}";
    private static final String TEX_FRAGMENT =
            "precision mediump float;\n" +
            "uniform sampler2D uTex;\n" +
            "varying vec2 vUv;\n" +
            "void main(){vec4 c=texture2D(uTex,vUv);if(c.a<0.08)discard;gl_FragColor=c;}";

    private final Context context;
    private final float[] projection = new float[16];
    private final float[] view = new float[16];
    private final float[] model = new float[16];
    private final float[] mvp = new float[16];
    private final float[] temp = new float[16];
    private int colorProgram, textureProgram;
    private int colorMvp, colorPos, colorColor;
    private int textureMvp, texturePos, textureUv, textureSampler;
    private int characterTexture;
    private float panX, panY;
    private final FloatBuffer cubeVertices = directBuffer(new float[36 * 3]);
    private final FloatBuffer cubeColors = directBuffer(new float[36 * 4]);
    private FloatBuffer quadVertices, quadUvs;

    public GlSceneRenderer(Context context) {
        this.context = context;
    }

    @Override public void onSurfaceCreated(GL10 gl, EGLConfig config) {
        GLES20.glClearColor(0.09f, 0.12f, 0.10f, 1f);
        GLES20.glEnable(GLES20.GL_DEPTH_TEST);
        GLES20.glDepthFunc(GLES20.GL_LEQUAL);
        GLES20.glEnable(GLES20.GL_CULL_FACE);
        GLES20.glCullFace(GLES20.GL_BACK);
        GLES20.glEnable(GLES20.GL_BLEND);
        GLES20.glBlendFunc(GLES20.GL_SRC_ALPHA, GLES20.GL_ONE_MINUS_SRC_ALPHA);

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
    }

    @Override public void onSurfaceChanged(GL10 gl, int width, int height) {
        GLES20.glViewport(0, 0, width, height);
        float aspect = width / (float) Math.max(1, height);
        GlMath.orthoForLandscape(projection, aspect, 18f);
    }

    @Override public void onDrawFrame(GL10 gl) {
        GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT | GLES20.GL_DEPTH_BUFFER_BIT);
        Matrix.setLookAtM(view, 0,
                15f + panX, 14f + panY, 18f,
                8f + panX, 0f, 8f + panY,
                0f, 1f, 0f);
        drawGround();
        drawRoadNetwork();
        drawSceneShadows();
        drawBuildings();
        drawTrees();
        drawPlayer();
    }

    public void pan(float dx, float dy) {
        panX = clamp(panX - dx / 150f, -5f, 5f);
        panY = clamp(panY + dy / 150f, -5f, 5f);
    }

    private void drawGround() {
        drawBox(8f, -0.35f, 8f, 16.5f, 0.25f, 16.5f,
                new float[]{0.48f, 0.42f, 0.29f, 1f}, true);
    }

    private void drawRoadNetwork() {
        float[] road = {0.14f, 0.14f, 0.13f, 1f};
        drawBox(8f, -0.20f, 4f, 16.5f, 0.12f, 1.0f, road, true);
        drawBox(8f, -0.20f, 12f, 16.5f, 0.12f, 1.0f, road, true);
        drawBox(4f, -0.20f, 8f, 1.0f, 0.12f, 16.5f, road, true);
        drawBox(12f, -0.20f, 8f, 1.0f, 0.12f, 16.5f, road, true);

        float[] lane = {0.78f, 0.69f, 0.49f, 1f};
        for (int x = 1; x < 16; x += 2) {
            drawBox(x, -0.13f, 4f, 0.55f, 0.025f, 0.035f, lane, true);
            drawBox(x, -0.13f, 12f, 0.55f, 0.025f, 0.035f, lane, true);
        }
        for (int z = 1; z < 16; z += 2) {
            drawBox(4f, -0.13f, z, 0.035f, 0.025f, 0.55f, lane, true);
            drawBox(12f, -0.13f, z, 0.035f, 0.025f, 0.55f, lane, true);
        }
    }

    private void drawSceneShadows() {
        float[] shadow = {0.03f, 0.03f, 0.025f, 0.30f};
        float[][] b = {
                {2f, 2f, 2.2f, 1.8f}, {6f, 2f, 2.4f, 1.9f}, {10f, 2f, 2.1f, 1.8f},
                {2f, 7f, 2.0f, 2.0f}, {10f, 7f, 2.5f, 2.1f},
                {2f, 12f, 2.2f, 1.9f}, {6f, 12f, 2.5f, 1.9f}, {10f, 12f, 2.2f, 2.0f}
        };
        for (float[] q : b) {
            drawBox(q[0] + 0.15f, -0.04f, q[1] + 0.15f, q[2] * 1.08f, 0.02f, q[3] * 1.08f, shadow, true);
        }
        float[][] t = {{4f, 5f, 0.8f}, {8.1f, 5.2f, 1.0f}, {13f, 5.5f, 0.95f},
                {4.2f, 10.1f, 0.9f}, {12.6f, 10.4f, 1.05f}};
        for (float[] q : t) drawBox(q[0] + 0.12f, -0.045f, q[1] + 0.12f,
                q[2] * 1.75f, 0.018f, q[2] * 1.05f, shadow, true);
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
        for (float[] q : b) drawBuilding(q[0], q[1], q[2], q[3], q[4], q[5], q[6], q[7]);
    }

    private void drawBuilding(float x, float z, float w, float d, float h, float r, float g, float b) {
        drawBox(x, h * 0.5f, z, w, h, d, new float[]{r, g, b, 1f}, true);
        drawBox(x, h + 0.10f, z, w + 0.18f, 0.16f, d + 0.18f,
                new float[]{r * 0.68f, g * 0.48f, b * 0.46f, 1f}, true);
        drawBox(x, h + 0.19f, z, w * 0.82f, 0.05f, d * 0.82f,
                new float[]{r * 0.80f, g * 0.74f, b * 0.70f, 1f}, true);
        drawWindows(x, z, w, d, h);
    }

    private void drawWindows(float x, float z, float w, float d, float h) {
        float[] glass = {0.30f, 0.58f, 0.72f, 1f};
        float[] side = {0.22f, 0.44f, 0.54f, 1f};
        for (float px : new float[]{x - w * 0.28f, x + w * 0.28f}) {
            drawBox(px, h * 0.60f, z - d * 0.505f, 0.22f, 0.28f, 0.03f, glass, true);
        }
        for (float pz : new float[]{z - d * 0.25f, z + d * 0.25f}) {
            drawBox(x + w * 0.505f, h * 0.60f, pz, 0.03f, 0.28f, 0.22f, side, true);
        }
    }

    private void drawTrees() {
        float[][] t = {{4f, 5f, 0.75f}, {8.1f, 5.2f, 0.95f}, {13f, 5.5f, 0.90f},
                {4.2f, 10.1f, 0.85f}, {12.6f, 10.4f, 1.0f}};
        for (float[] q : t) {
            drawBox(q[0], 0.45f, q[1], 0.18f, 0.9f, 0.18f,
                    new float[]{0.38f, 0.23f, 0.13f, 1f}, true);
            drawBox(q[0], 1.15f, q[1], q[2] * 1.5f, 1.2f, q[2] * 1.5f,
                    new float[]{0.20f, 0.48f, 0.18f, 1f}, true);
            drawBox(q[0] + 0.12f, 1.35f, q[1] + 0.06f, q[2], 0.35f, q[2],
                    new float[]{0.28f, 0.58f, 0.24f, 1f}, true);
        }
    }

    private void drawPlayer() {
        if (characterTexture == 0) return;
        Matrix.setIdentityM(model, 0);
        Matrix.translateM(model, 0, 8f, 0f, 8f);
        Matrix.rotateM(model, 0, 18f, 0f, 1f, 0f);
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
        GLES20.glEnable(GLES20.GL_DEPTH_TEST);
        GLES20.glDrawArrays(GLES20.GL_TRIANGLE_STRIP, 0, 4);
        GLES20.glDisableVertexAttribArray(texturePos);
        GLES20.glDisableVertexAttribArray(textureUv);
    }

    private void drawBox(float x, float y, float z, float w, float h, float d, float[] color, boolean depthWrite) {
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
        float[][] face = {
                {color[0]*0.78f,color[1]*0.78f,color[2]*0.78f,color[3]},
                {Math.min(1f,color[0]*1.04f),Math.min(1f,color[1]*1.04f),Math.min(1f,color[2]*1.04f),color[3]},
                {color[0]*0.72f,color[1]*0.72f,color[2]*0.72f,color[3]},
                {color[0]*0.92f,color[1]*0.92f,color[2]*0.92f,color[3]},
                {Math.min(1f,color[0]*1.12f),Math.min(1f,color[1]*1.12f),Math.min(1f,color[2]*1.12f),color[3]},
                {color[0]*0.60f,color[1]*0.60f,color[2]*0.60f,color[3]}
        };
        float[] colors = new float[36 * 4];
        int pos = 0;
        for (float[] fc : face) for (int i = 0; i < 6; i++) {
            colors[pos++] = fc[0]; colors[pos++] = fc[1]; colors[pos++] = fc[2]; colors[pos++] = fc[3];
        }
        cubeColors.clear(); cubeColors.put(colors).position(0);
        Matrix.setIdentityM(model, 0);
        Matrix.multiplyMM(temp, 0, view, 0, model, 0);
        Matrix.multiplyMM(mvp, 0, projection, 0, temp, 0);
        GLES20.glUseProgram(colorProgram);
        GLES20.glUniformMatrix4fv(colorMvp, 1, false, mvp, 0);
        if (!depthWrite) GLES20.glDepthMask(false);
        GLES20.glEnableVertexAttribArray(colorPos);
        GLES20.glVertexAttribPointer(colorPos, 3, GLES20.GL_FLOAT, false, 0, cubeVertices);
        GLES20.glEnableVertexAttribArray(colorColor);
        GLES20.glVertexAttribPointer(colorColor, 4, GLES20.GL_FLOAT, false, 0, cubeColors);
        GLES20.glDrawArrays(GLES20.GL_TRIANGLES, 0, 36);
        GLES20.glDisableVertexAttribArray(colorPos);
        GLES20.glDisableVertexAttribArray(colorColor);
        if (!depthWrite) GLES20.glDepthMask(true);
    }

    private void makeBillboardBuffers() {
        quadVertices = directBuffer(new float[]{-0.65f,0f,0f, 0.65f,0f,0f, -0.65f,1.55f,0f, 0.65f,1.55f,0f});
        quadUvs = directBuffer(new float[]{0f,1f,1f,1f,0f,0f,1f,0f});
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

    private int program(String vertex, String fragment) {
        int vs = shader(GLES20.GL_VERTEX_SHADER, vertex);
        int fs = shader(GLES20.GL_FRAGMENT_SHADER, fragment);
        int p = GLES20.glCreateProgram();
        GLES20.glAttachShader(p, vs);
        GLES20.glAttachShader(p, fs);
        GLES20.glLinkProgram(p);
        int[] ok = new int[1];
        GLES20.glGetProgramiv(p, GLES20.GL_LINK_STATUS, ok, 0);
        if (ok[0] == 0) throw new IllegalStateException(GLES20.glGetProgramInfoLog(p));
        return p;
    }

    private int shader(int type, String source) {
        int s = GLES20.glCreateShader(type);
        GLES20.glShaderSource(s, source);
        GLES20.glCompileShader(s);
        int[] ok = new int[1];
        GLES20.glGetShaderiv(s, GLES20.GL_COMPILE_STATUS, ok, 0);
        if (ok[0] == 0) throw new IllegalStateException(GLES20.glGetShaderInfoLog(s));
        return s;
    }

    private float clamp(float value, float min, float max) {
        return Math.max(min, Math.min(max, value));
    }
}
