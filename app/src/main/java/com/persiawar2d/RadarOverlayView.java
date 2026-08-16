package com.persiawar2d;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.view.View;

/** Live, non-interactive mini radar for the active Persia War battle. */
public class RadarOverlayView extends View {
    private final MainActivity.GameView game;
    private final Paint fill=new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint stroke=new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint text=new Paint(Paint.ANTI_ALIAS_FLAG);
    private final float range=1800f;
    public RadarOverlayView(Context context,MainActivity.GameView gameView){super(context);game=gameView;setClickable(false);setFocusable(false);setWillNotDraw(false);stroke.setStyle(Paint.Style.STROKE);text.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);}
    @Override protected void onDraw(Canvas c){super.onDraw(c);if(game==null||game.gameOver){postInvalidateOnAnimation();return;}float w=Math.min(320f,getWidth()*.23f),h=w*.70f,left=getWidth()-w-18f,top=MainActivity.GameView.HUD_H+10f;RectF box=new RectF(left,top,left+w,top+h);fill.setStyle(Paint.Style.FILL);fill.setColor(Color.argb(82,8,15,12));c.drawRoundRect(box,18f,18f,fill);stroke.setStrokeWidth(3f);stroke.setColor(Color.argb(205,224,193,108));c.drawRoundRect(box,18f,18f,stroke);float cx=box.centerX(),cy=box.centerY()+3f,rx=w*.39f,ry=h*.34f,r=Math.min(rx,ry);stroke.setStrokeWidth(1.5f);stroke.setColor(Color.argb(105,235,235,220));c.drawCircle(cx,cy,r,stroke);c.drawCircle(cx,cy,r*.52f,stroke);c.drawLine(cx-rx,cy,cx+rx,cy,stroke);c.drawLine(cx,cy-ry,cx,cy+ry,stroke);fill.setColor(Color.rgb(70,235,105));c.drawCircle(cx,cy,9f,fill);int alive=0,shown=0;for(MainActivity.GameView.Enemy e:game.enemies){if(e.hp<=0)continue;alive++;float dx=e.x-game.px,dy=e.y-game.py,d=(float)Math.hypot(dx,dy),nx=d<.001f?0:dx/d,ny=d<.001f?0:dy/d,md=Math.min(d,range),sx=cx+nx*(md/range)*rx,sy=cy+ny*(md/range)*ry;sx=Math.max(box.left+12,Math.min(box.right-12,sx));sy=Math.max(box.top+30,Math.min(box.bottom-13,sy));if(d<=range){fill.setColor(e.type==3?Color.rgb(255,160,45):Color.rgb(245,65,65));c.drawCircle(sx,sy,e.type==3?9:8,fill);shown++;}else drawEnemyArrow(c,cx,cy,sx,sy,e.type==3);}fill.setColor(Color.argb(205,20,24,20));c.drawRoundRect(box.left+8,box.top+7,box.left+106,box.top+30,8,8,fill);text.setTextSize(14);text.setColor(alive>0?Color.rgb(255,105,95):Color.rgb(120,240,135));text.setTextAlign(Paint.Align.LEFT);c.drawText("ENEMY  "+alive,box.left+15,box.top+23,text);text.setTextSize(12);text.setColor(Color.WHITE);text.setTextAlign(Paint.Align.RIGHT);c.drawText(shown+" NEAR",box.right-10,box.bottom-8,text);postInvalidateOnAnimation();}
    private void drawEnemyArrow(Canvas c,float cx,float cy,float x,float y,boolean heavy){float dx=x-cx,dy=y-cy,len=Math.max(1,(float)Math.hypot(dx,dy)),ux=dx/len,uy=dy/len,px=-uy,py=ux,backX=x-ux*17,backY=y-uy*17;Path a=new Path();a.moveTo(x,y);a.lineTo(backX+px*8,backY+py*8);a.lineTo(backX-px*8,backY-py*8);a.close();fill.setColor(heavy?Color.rgb(255,160,45):Color.rgb(245,65,65));c.drawPath(a,fill);}
}
