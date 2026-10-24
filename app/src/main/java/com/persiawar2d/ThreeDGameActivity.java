package com.persiawar2d;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.drawable.Drawable;
import android.graphics.Typeface;
import android.opengl.GLSurfaceView;
import android.os.Bundle;
import android.view.MotionEvent;
import android.view.Gravity;
import android.view.View;
import android.widget.FrameLayout;

/** Main playable 2.5D battle screen. */
public final class ThreeDGameActivity extends Activity {
    private static final int REQUEST_NEXT_STAGE_SETUP = 6102;
    private AdvancedThreeDRenderer renderer;
    private int stageNumber = 1;
    private boolean resultHandled;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().setFlags(1024,1024);
        FrameLayout root=new FrameLayout(this);
        stageNumber = Math.max(1, getIntent().getIntExtra(CharacterSelectActivity.EXTRA_STAGE, 1));
        String selectedWeapon = getSharedPreferences("player",0)
                .getString(LoadoutSelectActivity.EXTRA_SELECTED_WEAPON, "royal_bow");
        renderer=new AdvancedThreeDRenderer(this, stageNumber, selectedWeapon);
        GLSurfaceView surface=new GLSurfaceView(this);
        surface.setEGLContextClientVersion(2);
        surface.setRenderer(renderer);
        surface.setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);
        surface.setOnTouchListener((v,e)->renderer.onTouch(e));
        root.addView(surface,new FrameLayout.LayoutParams(-1,-1));
        root.addView(new CharacterView(),new FrameLayout.LayoutParams(-1,-1,Gravity.TOP|Gravity.START));
        root.addView(new HudView(),new FrameLayout.LayoutParams(-1,-1,Gravity.TOP|Gravity.START));
        setContentView(root);
    }

    @Override protected void onPause(){super.onPause();if(renderer!=null)renderer.pause();}
    @Override protected void onResume(){super.onResume();if(renderer!=null)renderer.resume();}

    @Override public void onBackPressed() { returnHome(); }

    private void returnHome() {
        Intent home = new Intent(this, StartActivity.class);
        home.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
        startActivity(home);
        finish();
    }

    private void startNextStageSetup() {
        Intent setup = new Intent(this, CharacterSelectActivity.class);
        setup.putExtra(CharacterSelectActivity.EXTRA_STAGE, stageNumber + 1);
        setup.putExtra(CharacterSelectActivity.EXTRA_NEXT_STAGE_FLOW, true);
        startActivityForResult(setup, REQUEST_NEXT_STAGE_SETUP);
    }

    @Override protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST_NEXT_STAGE_SETUP || resultCode != RESULT_OK || data == null) return;
        int nextStage = Math.max(stageNumber + 1,
                data.getIntExtra(CharacterSelectActivity.EXTRA_STAGE, stageNumber + 1));
        String skin = data.getStringExtra(CharacterSelectActivity.EXTRA_SELECTED_SKIN);
        if (skin != null) getSharedPreferences("player", 0).edit().putString("skin", skin).apply();
        Intent next = new Intent(this, ThreeDGameActivity.class);
        next.putExtra(CharacterSelectActivity.EXTRA_STAGE, nextStage);
        startActivity(next);
        finish();
    }

    /** Transparent visual layer for the high-resolution King sprite. */
    private final class CharacterView extends View {
        private final KingSpriteDrawable sprite;
        private final Drawable staticCharacter;
        private final Runnable refresh=new Runnable(){@Override public void run(){invalidate();postDelayed(this,90);}};
        private int lastAction=-1,lastDirection=-1,lastFrame=-1;

        CharacterView(){
            super(ThreeDGameActivity.this);
            setWillNotDraw(false);
            setClickable(false);
            setFocusable(false);
            sprite=new KingSpriteDrawable(ThreeDGameActivity.this);
            String skin=getSharedPreferences("player",0).getString("skin","classic");
            int art=0;
            if("blue".equals(skin)) art=R.drawable.player_guard;
            else if("red".equals(skin)) art=R.drawable.player_scout;
            else if("darius".equals(skin)) art=R.drawable.achaemenid_player;
            staticCharacter=art==0?null:getDrawable(art);
            post(refresh);
        }

        @Override protected void onDraw(Canvas c){
            if(renderer==null || getWidth()<=0 || getHeight()<=0)return;
            if(staticCharacter!=null){
                int characterSize=Math.max(1,(int)(getHeight()*.36f));
                int cx=getWidth()/2, bottom=(int)(getHeight()*.76f);
                staticCharacter.setBounds(cx-characterSize/2,bottom-characterSize,cx+characterSize/2,bottom);
                staticCharacter.draw(c);
                return;
            }
            float yaw=renderer.getPlayerYaw();
            float fx=(float)Math.sin(yaw), fz=(float)Math.cos(yaw);
            int direction;
            if(Math.abs(fz)>=Math.abs(fx)) direction=fz>=0f?0:3; // Down / Up
            else direction=fx<0f?1:2; // Left / Right

            boolean firing=renderer.isPlayerFiringVisual();
            boolean moving=renderer.getPlayerSpeed()>.35f;
            int action=firing?KingSpriteDrawable.ACTION_ATTACK:
                    (moving?KingSpriteDrawable.ACTION_WALK:KingSpriteDrawable.ACTION_IDLE);
            float t=System.nanoTime()/1_000_000_000f;
            int frame=(action==KingSpriteDrawable.ACTION_IDLE)?0:(int)(t*10f)%KingSpriteDrawable.FRAME_COUNT;

            if(action!=lastAction || direction!=lastDirection || frame!=lastFrame){
                sprite.setState(direction,action,frame);
                lastAction=action;lastDirection=direction;lastFrame=frame;
            }

            float h=getHeight();
            int charH=(int)(h*.36f);
            int charW=Math.max(1,(int)(charH*342f/1024f));
            int cx=getWidth()/2;
            int bottom=(int)(h*.76f);
            int left=cx-charW/2;
            sprite.setBounds(left,bottom-charH,left+charW,bottom);
            sprite.draw(c);
        }
    }

    private final class HudView extends View {
        private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
        private final RectF nextRect=new RectF();
        private final RectF homeRect=new RectF();
        private boolean resultTouchActive;
        private final Runnable refresh=new Runnable(){@Override public void run(){invalidate();postDelayed(this,120);}};
        HudView(){super(ThreeDGameActivity.this);setWillNotDraw(false);setClickable(false);post(refresh);}

        @Override protected void onDraw(Canvas c){
            if(renderer==null)return;
            float w=getWidth(),h=getHeight(),pad=Math.max(18f,w*.018f);
            p.setStyle(Paint.Style.FILL);p.setColor(0xC918211D);c.drawRoundRect(pad,pad,w-pad,88,18,18,p);
            p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextSize(19);p.setColor(Color.rgb(244,211,126));p.setTextAlign(Paint.Align.LEFT);c.drawText("PERSIA WAR  •  2.5D",pad+18,43,p);
            p.setTypeface(Typeface.DEFAULT);p.setTextSize(15);p.setColor(Color.WHITE);c.drawText("STAGE  "+stageNumber+"  •  WAVE "+renderer.getWave()+"/"+renderer.getWavesPerStage()+"  •  KILLS "+renderer.getKills(),pad+18,67,p);
            float barX=w*.39f,barW=w*.27f;p.setColor(0xFF29332E);c.drawRoundRect(barX,30,barX+barW,54,12,12,p);p.setColor(renderer.getHp()>35?Color.rgb(73,178,92):Color.rgb(205,73,61));c.drawRoundRect(barX+3,33,barX+Math.max(6,barW*renderer.getHp()/100f-3),51,9,9,p);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(14);p.setColor(Color.WHITE);c.drawText("HP "+renderer.getHp(),barX+barW/2f,47,p);
            p.setTextAlign(Paint.Align.RIGHT);p.setTextSize(16);c.drawText(renderer.getWeaponDisplayName()+"  •  AMMO "+renderer.getAmmo()+"/"+renderer.getReserve()+"   G "+renderer.getGrenades(),w-pad-18,47,p);
            float joyX=w*.15f,joyY=h*.80f,fireX=w*.84f,fireY=h*.78f,r=Math.min(76f,h*.105f);
            p.setColor(0x4A405149);c.drawCircle(joyX,joyY,r+16,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xC8D7C58E);c.drawCircle(joyX,joyY,r+16,p);p.setStyle(Paint.Style.FILL);p.setColor(0xBFC1A85F);c.drawCircle(joyX,joyY,r*.42f,p);
            p.setColor(0xA54E433C);c.drawCircle(fireX,fireY,r*1.18f,p);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xD7E6D39B);c.drawCircle(fireX,fireY,r*1.18f,p);p.setStyle(Paint.Style.FILL);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextSize(24);p.setColor(Color.WHITE);p.setTextAlign(Paint.Align.CENTER);c.drawText("FIRE",fireX,fireY+8,p);
            p.setTypeface(Typeface.DEFAULT);p.setTextSize(14);p.setColor(0xDDEEE8D8);c.drawText("MOVE",joyX,joyY+r+34,p);c.drawText("AIM / FIRE",fireX,fireY+r*1.18f+30,p);

            float mini=Math.min(230f,w*.24f);
            renderer.drawMinimap(c,w-mini-pad,100,mini);

            float actionR=Math.min(58f,h*.078f);
            actionButton(c,w*.67f,h*.88f,actionR,0xA85A6B4E,"G",18);
            actionButton(c,w*.78f,h*.90f,actionR*.82f,0xA84B5651,"RELOAD",11);

            if(renderer.isStageComplete()){
                if(!resultHandled){
                    resultHandled=true;
                    int highest=getSharedPreferences("progress",0).getInt("maxUnlockedStage",1);
                    getSharedPreferences("progress",0).edit().putInt("maxUnlockedStage",Math.max(highest,stageNumber+1)).apply();
                }
                drawResultPanel(c,w,h,true);
            } else if(renderer.isGameOver()){
                if(!resultHandled){
                    resultHandled=true;
                    postDelayed(()->{if(!isFinishing())returnHome();},2400);
                }
                drawResultPanel(c,w,h,false);
            }
        }
        private void drawResultPanel(Canvas c,float w,float h,boolean won){
            p.setStyle(Paint.Style.FILL);p.setColor(0xEA07151B);
            RectF panel=new RectF(w*.19f,h*.13f,w*.81f,h*.88f);
            c.drawRoundRect(panel,24,24,p);
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(3);p.setColor(0xFFE8BE62);c.drawRoundRect(panel,24,24,p);
            p.setStyle(Paint.Style.FILL);p.setTextAlign(Paint.Align.CENTER);p.setTypeface(Typeface.DEFAULT_BOLD);
            p.setTextSize(Math.min(40f,h*.115f));p.setColor(won?Color.rgb(244,210,123):Color.rgb(242,158,133));
            c.drawText(won?"STAGE CLEARED":"BATTLE LOST",w*.5f,h*.34f,p);
            p.setTypeface(Typeface.DEFAULT);p.setTextSize(Math.min(18f,h*.052f));p.setColor(Color.WHITE);
            c.drawText(won?("Stage "+stageNumber+" complete • All waves cleared"):"Returning to the main menu…",w*.5f,h*.44f,p);
            if(won){
                nextRect.set(w*.32f,h*.55f,w*.68f,h*.68f);
                homeRect.set(w*.32f,h*.72f,w*.68f,h*.84f);
                drawResultButton(c,nextRect,"NEXT STAGE • SELECT HERO",true);
                drawResultButton(c,homeRect,"MAIN MENU",false);
            }else{
                nextRect.setEmpty();
                homeRect.set(w*.32f,h*.63f,w*.68f,h*.78f);
                drawResultButton(c,homeRect,"RETURN TO MAIN MENU",false);
            }
            p.setTypeface(Typeface.DEFAULT);p.setTextAlign(Paint.Align.LEFT);p.setStyle(Paint.Style.FILL);
        }

        private void drawResultButton(Canvas c,RectF rect,String label,boolean primary){
            p.setStyle(Paint.Style.FILL);p.setColor(primary?0xFF1B624A:0xFF24313A);c.drawRoundRect(rect,15,15,p);
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2);p.setColor(0xFFE8BE62);c.drawRoundRect(rect,15,15,p);
            p.setStyle(Paint.Style.FILL);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.CENTER);
            p.setTextSize(Math.min(17f,rect.height()*.36f));p.setColor(Color.WHITE);
            c.drawText(label,rect.centerX(),rect.centerY()+p.getTextSize()*.34f,p);
        }

        private void actionButton(Canvas c,float x,float y,float r,int fill,String label,float textSize){
            p.setStyle(Paint.Style.FILL);p.setColor(0x44000000);c.drawCircle(x,y+5,r+3,p);
            p.setColor(fill);c.drawCircle(x,y,r,p);
            p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2.5f);p.setColor(0xD7E6D39B);c.drawCircle(x,y,r,p);
            p.setStyle(Paint.Style.FILL);p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextAlign(Paint.Align.CENTER);p.setTextSize(textSize);p.setColor(Color.WHITE);c.drawText(label,x,y+textSize*.34f,p);
            p.setTypeface(Typeface.DEFAULT);
        }

        @Override public boolean onTouchEvent(MotionEvent event){
            float x=event.getX(),y=event.getY(),w=getWidth(),h=getHeight();
            int action=event.getActionMasked();
            if(renderer.isGameOver()||renderer.isStageComplete()){
                if(action==MotionEvent.ACTION_DOWN){resultTouchActive=true;return true;}
                if(resultTouchActive&&(action==MotionEvent.ACTION_UP||action==MotionEvent.ACTION_CANCEL)){
                    resultTouchActive=false;
                    if(action==MotionEvent.ACTION_UP){
                        if(homeRect.contains(x,y))returnHome();
                        else if(renderer.isStageComplete()&&nextRect.contains(x,y))startNextStageSetup();
                    }
                    return true;
                }
                return resultTouchActive;
            }
            if(action==MotionEvent.ACTION_DOWN){
                float ar=Math.min(58f,h*.078f);
                if(Math.hypot(x-w*.67f,y-h*.88f)<ar*1.15f){renderer.throwGrenade();invalidate();return true;}
                if(Math.hypot(x-w*.78f,y-h*.90f)<ar){renderer.reload();invalidate();return true;}
            }
            return false;
        }
    }
}
