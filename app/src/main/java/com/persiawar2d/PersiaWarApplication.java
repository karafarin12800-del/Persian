package com.persiawar2d;

import android.app.Application;

public class PersiaWarApplication extends Application {
    @Override public void onCreate(){
        super.onCreate();
        final Thread.UncaughtExceptionHandler previous=Thread.getDefaultUncaughtExceptionHandler();
        Thread.setDefaultUncaughtExceptionHandler((thread,throwable)->{
            try{
                java.io.File f=new java.io.File(getFilesDir(),"last_crash.txt");
                java.io.PrintWriter out=new java.io.PrintWriter(new java.io.FileWriter(f,false));
                out.println("UNCAUGHT");
                throwable.printStackTrace(out);
                out.close();
            }catch(Throwable ignored){}
            if(previous!=null) previous.uncaughtException(thread,throwable);
        });
    }
}
