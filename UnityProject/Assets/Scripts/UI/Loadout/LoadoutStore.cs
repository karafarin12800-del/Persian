using UnityEngine;
using PersiaWar.Unity2D5D;
namespace PersiaWar.Unity2D5D.UI.Loadout
{
 public static class LoadoutStore
 {
  public const string SelectedHeroKey="PW.SelectedHero";
  public const string SelectedWeaponKey="PW.SelectedWeaponKind";
  public static int Coins=>PlayerPrefs.GetInt("PW.StoreCoins",500);
  public static int Tokens=>PlayerPrefs.GetInt("PW.StoreTokens",0);
  public static int LastStageTokenReward=>PlayerPrefs.GetInt("PW.LastStageTokenReward",0);
  public static void EnsureDefaults()
  {
   if(!PlayerPrefs.HasKey("PW.HeroOwned.0"))PlayerPrefs.SetInt("PW.HeroOwned.0",1);
   if(!PlayerPrefs.HasKey("PW.WeaponOwned.1"))PlayerPrefs.SetInt("PW.WeaponOwned.1",1);
   if(!PlayerPrefs.HasKey(SelectedHeroKey))PlayerPrefs.SetInt(SelectedHeroKey,0);
   if(!PlayerPrefs.HasKey(SelectedWeaponKey))PlayerPrefs.SetInt(SelectedWeaponKey,1);
   PlayerPrefs.Save();
  }
  public static int AddTokens(int amount)
  {
   if(amount<=0)return Tokens;
   int value=Tokens+amount;PlayerPrefs.SetInt("PW.StoreTokens",value);PlayerPrefs.Save();return value;
  }
  public static bool IsHeroOwned(int i)=>i==0||PlayerPrefs.GetInt("PW.HeroOwned."+i,0)!=0;
  public static bool CanUseHero(int i,bool rescued)
  {
   if(LoadoutTestSwitch.UnlockEverything)return true;
   return i==0||IsHeroOwned(i)||(i==1&&rescued);
  }
  public static bool TryBuyHero(int i,bool rescued)
  {
   if(i<0||i>=HeroCatalog.Names.Length)return false;
   if(LoadoutTestSwitch.UnlockEverything||CanUseHero(i,rescued))return true;
   if(i==1&&!rescued)return false;
   int tokenPrice=HeroCatalog.TokenPrice(i);
   if(tokenPrice>0)
   {
    if(Tokens<tokenPrice)return false;
    PlayerPrefs.SetInt("PW.StoreTokens",Tokens-tokenPrice);
   }
   else
   {
    int price=HeroCatalog.Price(i);if(Coins<price)return false;
    PlayerPrefs.SetInt("PW.StoreCoins",Coins-price);
   }
   PlayerPrefs.SetInt("PW.HeroOwned."+i,1);PlayerPrefs.Save();return true;
  }
  public static bool IsCampaignStageCompleted(int stage)
  {
   if(stage<1||stage>4)return false;
   bool done=PlayerPrefs.GetInt("PW.StageCompleted."+stage,0)!=0;
   if(stage==4)done|=PlayerPrefs.GetInt("PW.Stage4Completed",0)!=0;
   return done;
  }
  public static bool IsCampaignStageAvailable(int stage)
  {
   switch(stage)
   {
    case 1:return true;
    case 2:return IsCampaignStageCompleted(1);
    case 3:return IsCampaignStageCompleted(2)||PlayerPrefs.GetInt("PW.Stage3Unlocked",0)!=0;
    case 4:return IsCampaignStageCompleted(3)||PlayerPrefs.GetInt("PW.Stage4Unlocked",0)!=0;
    default:return false;
   }
  }
  public static int GetNextCampaignStage()
  {
   for(int stage=1;stage<=4;stage++)
    if(IsCampaignStageAvailable(stage)&&!IsCampaignStageCompleted(stage))return stage;
   return 5;
  }
  public static bool IsStageUnlocked(int stage)=>stage<=0||IsCampaignStageCompleted(stage);
  public static bool IsWeaponOwned(WeaponController.WeaponKind k)
  {
   if(k==WeaponController.WeaponKind.AssaultRifle)return true;
   if(PlayerPrefs.GetInt("PW.WeaponOwned."+(int)k,0)!=0)return true;
   if(k==WeaponController.WeaponKind.VoidRifle)return PlayerPrefs.GetInt("PW.Reward.VoidRifle",0)!=0;
   if(k==WeaponController.WeaponKind.AlienSword)return PlayerPrefs.GetInt("PW.Reward.AlienSword",0)!=0;
   return false;
  }
  public static bool CanUseWeapon(WeaponController.WeaponKind k)
  {
   if(LoadoutTestSwitch.UnlockEverything)return true;
   WeaponCatalog.Entry e=WeaponCatalog.Get(k);
   return IsStageUnlocked(e.RequiredCompletedStage)&&IsWeaponOwned(k);
  }
  public static bool TryBuyWeapon(WeaponController.WeaponKind k)
  {
   if(k==WeaponController.WeaponKind.AssaultRifle)return true;
   if(LoadoutTestSwitch.UnlockEverything||CanUseWeapon(k))return true;
   WeaponCatalog.Entry e=WeaponCatalog.Get(k);
   if(!IsStageUnlocked(e.RequiredCompletedStage))return false;
   if(e.TokenPrice>0)
   {
    if(Tokens<e.TokenPrice)return false;
    PlayerPrefs.SetInt("PW.StoreTokens",Tokens-e.TokenPrice);
   }
   else
   {
    if(Coins<e.Price)return false;
    PlayerPrefs.SetInt("PW.StoreCoins",Coins-e.Price);
   }
   PlayerPrefs.SetInt("PW.WeaponOwned."+(int)k,1);
   if(k==WeaponController.WeaponKind.VoidRifle)PlayerPrefs.SetInt("PW.Reward.VoidRifle",1);
   if(k==WeaponController.WeaponKind.AlienSword)PlayerPrefs.SetInt("PW.Reward.AlienSword",1);
   PlayerPrefs.Save();return true;
  }
  public static bool TrySellWeapon(WeaponController.WeaponKind k,out int refund,out bool refundInTokens)
  {
   refund=0;refundInTokens=false;
   if(LoadoutTestSwitch.UnlockEverything||k==WeaponController.WeaponKind.AssaultRifle||!IsWeaponOwned(k))return false;
   WeaponCatalog.Entry e=WeaponCatalog.Get(k);
   if(e.TokenPrice>0)
   {refundInTokens=true;refund=Mathf.Max(1,e.TokenPrice/2);PlayerPrefs.SetInt("PW.StoreTokens",Tokens+refund);}
   else
   {refund=Mathf.Max(1,e.Price/2);PlayerPrefs.SetInt("PW.StoreCoins",Coins+refund);}
   PlayerPrefs.SetInt("PW.WeaponOwned."+(int)k,0);
   if(k==WeaponController.WeaponKind.VoidRifle)PlayerPrefs.SetInt("PW.Reward.VoidRifle",0);
   if(k==WeaponController.WeaponKind.AlienSword)PlayerPrefs.SetInt("PW.Reward.AlienSword",0);
   if(SelectedWeapon==k)SelectedWeapon=WeaponController.WeaponKind.AssaultRifle;
   PlayerPrefs.Save();return true;
  }
  public static int MarkStageCompleted(int stage,bool test)
  {
   if(test||stage<1||stage>4){PlayerPrefs.SetInt("PW.LastStageTokenReward",0);return 0;}
   string key="PW.StageCompleted."+stage;bool repeat=PlayerPrefs.GetInt(key,0)!=0;PlayerPrefs.SetInt(key,1);
   if(stage==3)PlayerPrefs.SetInt("PW.Stage3Unlocked",1);
   if(stage==4){PlayerPrefs.SetInt("PW.Stage4Unlocked",1);PlayerPrefs.SetInt("PW.Stage4Completed",1);}
   int reward=repeat?100:250,tokenReward=repeat?10:25;
   PlayerPrefs.SetInt("PW.StoreCoins",Coins+reward);PlayerPrefs.SetInt("PW.StoreTokens",Tokens+tokenReward);
   PlayerPrefs.SetInt("PW.LastStageTokenReward",tokenReward);PlayerPrefs.Save();return reward;
  }
  public static int SelectedHero{get=>PlayerPrefs.GetInt(SelectedHeroKey,0);set{PlayerPrefs.SetInt(SelectedHeroKey,Mathf.Clamp(value,0,5));PlayerPrefs.Save();}}
  public static WeaponController.WeaponKind SelectedWeapon{get=>(WeaponController.WeaponKind)PlayerPrefs.GetInt(SelectedWeaponKey,1);set{PlayerPrefs.SetInt(SelectedWeaponKey,(int)value);PlayerPrefs.Save();}}
 }
}