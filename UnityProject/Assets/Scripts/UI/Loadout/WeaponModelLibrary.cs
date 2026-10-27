using UnityEngine; using PersiaWar.Unity2D5D;
namespace PersiaWar.Unity2D5D.UI.Loadout {
public static class WeaponModelLibrary {
const string RootName="PW_RichWeaponModel";
public static void Refresh(Transform parent,WeaponController.WeaponKind kind){
if(parent==null||GraphicsQualityProfile.CurrentTier==GraphicsQualityProfile.QualityTier.Low||Time.frameCount<2)return;
Transform root=parent.Find(RootName);
if(root==null){GameObject g=new GameObject(RootName);root=g.transform;root.SetParent(parent,false);}
string n=kind==WeaponController.WeaponKind.LightPistol?"LightPistol":kind==WeaponController.WeaponKind.AssaultRifle?"AssaultRifle":kind==WeaponController.WeaponKind.HeavyMachineGun?"HeavyMachineGun":kind==WeaponController.WeaponKind.Axe?"Axe":kind==WeaponController.WeaponKind.VoidRifle?"VoidRifle":"AlienSword";
Transform selected=root.Find(n);
if(selected==null){Add(root,n,kind);selected=root.Find(n);}
for(int i=0;i<root.childCount;i++)root.GetChild(i).gameObject.SetActive(false);
if(selected!=null)selected.gameObject.SetActive(true);}
public static bool BuildPickupModel(Transform parent, WeaponController.WeaponKind kind)
{
if(parent==null)return false;
Transform root=parent.Find(RootName);
if(root==null){GameObject g=new GameObject(RootName);root=g.transform;root.SetParent(parent,false);}
string n=kind==WeaponController.WeaponKind.LightPistol?"LightPistol":kind==WeaponController.WeaponKind.AssaultRifle?"AssaultRifle":kind==WeaponController.WeaponKind.HeavyMachineGun?"HeavyMachineGun":kind==WeaponController.WeaponKind.Axe?"Axe":kind==WeaponController.WeaponKind.VoidRifle?"VoidRifle":"AlienSword";
Transform selected=root.Find(n);
if(selected==null){Add(root,n,kind);selected=root.Find(n);}
for(int i=0;i<root.childCount;i++)root.GetChild(i).gameObject.SetActive(false);
if(selected!=null)selected.gameObject.SetActive(true);
return selected!=null && selected.GetComponentsInChildren<Renderer>(true).Length>0;
}
static void Add(Transform p,string n,WeaponController.WeaponKind k){Transform m=new GameObject(n).transform;m.SetParent(p,false);Color metal=new Color(.12f,.17f,.22f),a=k==WeaponController.WeaponKind.VoidRifle?Color.cyan:k==WeaponController.WeaponKind.AlienSword?Color.green:k==WeaponController.WeaponKind.Axe?new Color(.8f,.8f,.82f):new Color(.62f,.48f,.27f);
if(k==WeaponController.WeaponKind.Axe){Part(m,"WrappedHandle",PrimitiveType.Cube,Vector3.zero,new Vector3(.065f,.075f,.78f),metal);Part(m,"AxeHead",PrimitiveType.Cube,new Vector3(0,.05f,.3f),new Vector3(.3f,.25f,.12f),a);}
else if(k==WeaponController.WeaponKind.AlienSword){Part(m,"Blade",PrimitiveType.Cylinder,new Vector3(0,0,.42f),new Vector3(.055f,.55f,.055f),a);Part(m,"Guard",PrimitiveType.Cube,Vector3.zero,new Vector3(.38f,.07f,.09f),metal);}
else{bool pistol=k==WeaponController.WeaponKind.LightPistol,heavy=k==WeaponController.WeaponKind.HeavyMachineGun;Part(m,"Receiver",PrimitiveType.Cube,Vector3.zero,pistol?new Vector3(.17f,.12f,.34f):new Vector3(.22f,.17f,.62f),metal);Part(m,"Barrel",PrimitiveType.Cylinder,new Vector3(0,0,.48f),new Vector3(.045f,heavy?.42f:.32f,.045f),metal);Part(m,"Magazine",PrimitiveType.Cube,new Vector3(0,-.16f,.05f),new Vector3(.12f,heavy?.36f:.28f,.17f),a);Part(m,"Stock",PrimitiveType.Cube,new Vector3(0,0,-.4f),new Vector3(.15f,.14f,.34f),metal);Part(m,"Sight",PrimitiveType.Cube,new Vector3(0,.13f,.04f),new Vector3(.12f,.07f,.12f),a);if(k==WeaponController.WeaponKind.VoidRifle)for(int i=0;i<3;i++)Part(m,"Coil"+i,PrimitiveType.Cylinder,new Vector3(0,0,.18f+i*.17f),new Vector3(.12f,.035f,.12f),a);if(heavy)Part(m,"AmmoBox",PrimitiveType.Cube,new Vector3(.14f,-.1f,-.02f),new Vector3(.22f,.25f,.22f),a);}}
static void Part(Transform p,string n,PrimitiveType t,Vector3 pos,Vector3 scale,Color c){GameObject g=GameObject.CreatePrimitive(t);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;Collider col=g.GetComponent<Collider>();if(col!=null)Object.Destroy(col);Renderer r=g.GetComponent<Renderer>();if(r!=null)r.sharedMaterial=RuntimeMaterialFactory.Create("PW_Weapon_"+n,c);}
}}