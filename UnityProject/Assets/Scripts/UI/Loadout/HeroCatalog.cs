namespace PersiaWar.Unity2D5D.UI.Loadout
{
 public static class HeroCatalog
 {
  public static readonly string[] Names={"KING ARDESHIR","ANAHITA","PARS GUARD","ROYAL SCOUT","SILK WARRIOR","DESERT KNIGHT"};
  public static readonly int[] Prices={0,300,0,300,500,600};
  public static readonly int[] TokenPrices={0,0,140,0,50,60};
  public static int Price(int i)=>Prices[i<0||i>=Prices.Length?0:i];
  public static int TokenPrice(int i)=>TokenPrices[i<0||i>=TokenPrices.Length?0:i];
  public static string Name(int i)=>Names[i<0||i>=Names.Length?0:i];
 }
}