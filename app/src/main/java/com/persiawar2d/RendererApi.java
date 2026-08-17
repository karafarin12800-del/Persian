package com.persiawar2d;

/** Small renderer-facing contract so gameplay does not know how the world is drawn. */
public interface RendererApi {
    void setViewport(int width, int height);
    void setCamera(float worldX, float worldY, float zoom);
    void beginFrame();
    void endFrame();
}
