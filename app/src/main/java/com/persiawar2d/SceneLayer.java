package com.persiawar2d;

/** Named scene layers keep ground, structures, foliage and characters separated. */
public enum SceneLayer {
    GROUND(0),
    ROAD(10),
    SHADOW(20),
    STRUCTURE(30),
    FOLIAGE(40),
    CHARACTER(50),
    EFFECT(60);

    public final int order;
    SceneLayer(int order) { this.order = order; }
}
