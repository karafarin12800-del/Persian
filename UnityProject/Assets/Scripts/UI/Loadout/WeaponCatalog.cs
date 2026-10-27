using PersiaWar.Unity2D5D;
namespace PersiaWar.Unity2D5D.UI.Loadout
{
 public static class WeaponCatalog
 {
  public struct Entry
  {
   public WeaponController.WeaponKind Kind;public string Name;public int Price;public int TokenPrice;public int RequiredCompletedStage;
   public Entry(WeaponController.WeaponKind kind,string name,int price,int tokenPrice,int requiredCompletedStage)
   {Kind=kind;Name=name;Price=price;TokenPrice=tokenPrice;RequiredCompletedStage=requiredCompletedStage;}
  }
  public static readonly Entry[] Entries={
   new Entry(WeaponController.WeaponKind.AssaultRifle,"ASSAULT RIFLE",0,0,0),
   new Entry(WeaponController.WeaponKind.LightPistol,"LIGHT PISTOL",0,140,1),
   new Entry(WeaponController.WeaponKind.HeavyMachineGun,"HEAVY MACHINE GUN",300,0,2),
   new Entry(WeaponController.WeaponKind.Axe,"COMBAT AXE",350,30,3),
   new Entry(WeaponController.WeaponKind.VoidRifle,"VOID RIFLE",450,45,4),
   new Entry(WeaponController.WeaponKind.AlienSword,"ALIEN SWORD",400,40,4)
  };
  public static int IndexOf(WeaponController.WeaponKind kind){for(int i=0;i<Entries.Length;i++)if(Entries[i].Kind==kind)return i;return 0;}
  public static Entry Get(WeaponController.WeaponKind kind)=>Entries[IndexOf(kind)];
  public static Entry GetAt(int i)=>Entries[i<0||i>=Entries.Length?0:i];
 }
}