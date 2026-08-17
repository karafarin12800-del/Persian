package com.persiawar2d;

/** Camera state for the 2.5D world. UI coordinates are intentionally separate. */
public final class ProjectionCamera {
    private float x;
    private float y;
    private float zoom = RenderConfig.CAMERA_ZOOM;

    public void setPosition(float x, float y) {
        this.x = x;
        this.y = y;
    }

    public void move(float dx, float dy) {
        x += dx;
        y += dy;
    }

    public void setZoom(float zoom) {
        this.zoom = Math.max(0.65f, Math.min(1.65f, zoom));
    }

    public float getX() { return x; }
    public float getY() { return y; }
    public float getZoom() { return zoom; }

    public float projectX(float worldX, float worldY, float screenCenterX) {
        return screenCenterX + (worldX - x - (worldY - y)) * RenderConfig.TILE_WIDTH * 0.5f * zoom;
    }

    public float projectY(float worldX, float worldY, float height, float screenCenterY) {
        return screenCenterY + (worldX - x + worldY - y) * RenderConfig.TILE_HEIGHT * 0.5f * zoom
                - height * zoom;
    }
}
