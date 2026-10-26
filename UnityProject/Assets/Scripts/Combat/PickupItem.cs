using System.Collections.Generic;
using UnityEngine;
namespace PersiaWar.Unity2D5D
{
 public sealed class PickupItem:MonoBehaviour
 {
  public enum PickupType{Ammo,Medkit,Grenade,Shield,Weapon,QuickDash}
  [SerializeField] private PickupType type=PickupType.Ammo;
  [SerializeField] private int amount=30;
  [SerializeField] private float rotateSpeed=90f;
  [SerializeField] private float bobHeight=.15f;
  [SerializeField] private WeaponController.WeaponKind weaponKind=WeaponController.WeaponKind.AssaultRifle;
  private static readonly Dictionary<int,Material> Materials=new Dictionary<int,Material>();
  private readonly List<Mesh> generatedMeshes=new List<Mesh>();
  private Transform visualRoot;
  private float startY;
  private bool started;
  private bool collected;

  public void Configure(PickupType pickupType,int value){type=pickupType;amount=Mathf.Max(1,value);if(started)BuildConsistentVisual();}
  public void ConfigureWeapon(WeaponController.WeaponKind kind,int reserveAmmo){type=PickupType.Weapon;weaponKind=kind;amount=Mathf.Max(1,reserveAmmo);if(started)BuildConsistentVisual();}

  private void Awake()
  {
   startY=transform.position.y;
   SphereCollider trigger=GetComponent<SphereCollider>();
   if(trigger==null)trigger=gameObject.AddComponent<SphereCollider>();
   trigger.isTrigger=true;trigger.radius=.7f;
   Collider[] colliders=GetComponents<Collider>();
   for(int i=0;i<colliders.Length;i++)
   {
    if(colliders[i]==null)continue;
    if(colliders[i]==trigger){colliders[i].isTrigger=true;continue;}
    colliders[i].enabled=false;
   }
  }
  private void Start(){started=true;BuildConsistentVisual();}
  private void OnDestroy(){for(int i=0;i<generatedMeshes.Count;i++)if(generatedMeshes[i]!=null)Destroy(generatedMeshes[i]);generatedMeshes.Clear();}

  private void BuildConsistentVisual()
  {
   if(visualRoot!=null)Destroy(visualRoot.gameObject);
   for(int i=0;i<generatedMeshes.Count;i++)if(generatedMeshes[i]!=null)Destroy(generatedMeshes[i]);
   generatedMeshes.Clear();
   Renderer[] old=GetComponentsInChildren<Renderer>(true);
   for(int i=0;i<old.Length;i++)if(old[i]!=null)old[i].enabled=false;
   // Disable colliders owned by the old primitive visuals, but preserve the root
   // SphereCollider trigger so collection still works.
   Collider[] oldColliders=GetComponentsInChildren<Collider>(true);
   for(int i=0;i<oldColliders.Length;i++)
   {
    Collider oldCollider=oldColliders[i];
    if(oldCollider==null)continue;
    if(oldCollider.transform==transform && oldCollider is SphereCollider)
    {
     oldCollider.isTrigger=true;
     continue;
    }
    oldCollider.enabled=false;
   }
   visualRoot=new GameObject("PW_PickupVisual").transform;
   visualRoot.SetParent(transform,false);
   Vector3 s=transform.localScale;
   visualRoot.localScale=new Vector3(1f/Mathf.Max(.01f,Mathf.Abs(s.x)),1f/Mathf.Max(.01f,Mathf.Abs(s.y)),1f/Mathf.Max(.01f,Mathf.Abs(s.z)));
   visualRoot.localPosition=Vector3.zero;visualRoot.localRotation=Quaternion.identity;
   switch(type)
   {
    case PickupType.Medkit:BuildMedkit();break;
    case PickupType.Shield:BuildShield();break;
    case PickupType.Grenade:BuildGrenade();break;
    case PickupType.Weapon:BuildWeaponCrate();break;
    case PickupType.Ammo:BuildAmmoCrate();break;
    case PickupType.QuickDash:BuildQuickDash();break;
   }
  }
  private void BuildMedkit()
  {
   Color red=new Color(.88f,.08f,.10f);
   Part(PrimitiveType.Cube,"MedkitCase",new Vector3(0,.12f,0),new Vector3(.78f,.28f,.66f),new Color(.92f,.96f,1f));
   Part(PrimitiveType.Cube,"MedkitLid",new Vector3(0,.27f,0),new Vector3(.72f,.06f,.60f),Color.white);
   Part(PrimitiveType.Cube,"MedkitCrossVertical",new Vector3(0,.31f,0),new Vector3(.15f,.035f,.44f),red);
   Part(PrimitiveType.Cube,"MedkitCrossHorizontal",new Vector3(0,.312f,0),new Vector3(.46f,.035f,.15f),red);
  }
  private void BuildShield()
  {
   ShieldMesh("ShieldOuter",.42f,.46f,new Color(.96f,.72f,.18f),.12f);
   ShieldMesh("ShieldCore",.34f,.37f,new Color(.12f,.60f,1f),.145f);
   Part(PrimitiveType.Cylinder,"ShieldHub",new Vector3(0,.19f,.015f),new Vector3(.18f,.045f,.18f),new Color(.75f,.94f,1f));
  }
  private void BuildGrenade()
  {
   Part(PrimitiveType.Capsule,"GrenadeBody",new Vector3(0,.20f,0),new Vector3(.30f,.38f,.30f),new Color(.20f,.52f,.24f));
   Part(PrimitiveType.Cylinder,"GrenadeCap",new Vector3(0,.42f,0),new Vector3(.19f,.06f,.19f),new Color(.25f,.28f,.24f));
   Part(PrimitiveType.Cube,"GrenadePin",new Vector3(.15f,.43f,0),new Vector3(.16f,.045f,.055f),new Color(.75f,.78f,.72f),new Vector3(0,0,24));
  }
  private void BuildWeaponCrate()
  {
   // Use a recognizable dropped rifle silhouette instead of a generic cube.
   Color receiver=new Color(.18f,.23f,.27f);
   Color metal=new Color(.58f,.64f,.67f);
   Color grip=new Color(.12f,.15f,.17f);
   Part(PrimitiveType.Cube,"DroppedWeaponReceiver",new Vector3(0,.30f,0),new Vector3(.18f,.13f,.78f),receiver);
   Part(PrimitiveType.Cylinder,"DroppedWeaponMuzzle",new Vector3(0,.30f,.49f),new Vector3(.045f,.16f,.045f),metal,new Vector3(90,0,0));
   Part(PrimitiveType.Cube,"DroppedWeaponStock",new Vector3(0,.30f,-.49f),new Vector3(.14f,.11f,.30f),grip);
   Part(PrimitiveType.Cube,"DroppedWeaponMagazine",new Vector3(0,.19f,.04f),new Vector3(.11f,.25f,.17f),metal,new Vector3(8,0,0));
   Part(PrimitiveType.Cube,"DroppedWeaponGrip",new Vector3(0,.19f,-.10f),new Vector3(.12f,.22f,.14f),grip,new Vector3(-12,0,0));
   Part(PrimitiveType.Cube,"DroppedWeaponSight",new Vector3(0,.39f,.08f),new Vector3(.08f,.07f,.18f),metal);
  }
  private void BuildAmmoCrate()
  {
   Part(PrimitiveType.Cube,"AmmoSupplyCrate",new Vector3(0,.13f,0),new Vector3(.70f,.26f,.58f),new Color(.28f,.34f,.24f));
   Part(PrimitiveType.Cube,"AmmoCrateBand",new Vector3(0,.28f,0),new Vector3(.16f,.035f,.54f),new Color(.92f,.68f,.17f));
   for(int i=-1;i<=1;i++)Part(PrimitiveType.Cylinder,"AmmoRound_"+i,new Vector3(i*.18f,.31f,.12f),new Vector3(.07f,.10f,.07f),new Color(.82f,.70f,.35f));
  }
  private void BuildQuickDash()
  {
   Part(PrimitiveType.Cube,"QuickDashCrystal",new Vector3(0,.22f,0),new Vector3(.32f,.32f,.32f),new Color(.90f,.10f,.88f),new Vector3(45,45,45));
   Part(PrimitiveType.Cube,"QuickDashCore",new Vector3(0,.24f,0),new Vector3(.12f,.12f,.12f),new Color(.70f,.95f,1f),new Vector3(45,45,45));
  }
  private void ShieldMesh(string name,float w,float d,Color color,float y)
  {
   GameObject go=new GameObject(name);go.transform.SetParent(visualRoot,false);go.transform.localPosition=new Vector3(0,y,0);
   Mesh mesh=new Mesh{name="PW_"+name+"Mesh"};
   mesh.vertices=new[]{new Vector3(-w,0,d*.65f),new Vector3(w,0,d*.65f),new Vector3(w*.86f,0,-d*.12f),new Vector3(0,0,-d),new Vector3(-w*.86f,0,-d*.12f)};
   mesh.triangles=new[]{0,1,2,0,2,3,0,3,4};mesh.RecalculateNormals();mesh.RecalculateBounds();generatedMeshes.Add(mesh);
   go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=GetMaterial(color);
  }
  private GameObject Part(PrimitiveType primitive,string name,Vector3 pos,Vector3 scale,Color color,Vector3 rotation=default(Vector3))
  {
   GameObject go=GameObject.CreatePrimitive(primitive);go.name=name;go.transform.SetParent(visualRoot,false);
   go.transform.localPosition=pos;go.transform.localScale=scale;go.transform.localEulerAngles=rotation;
   Collider col=go.GetComponent<Collider>();if(col!=null)col.enabled=false;
   Renderer rend=go.GetComponent<Renderer>();if(rend!=null)rend.sharedMaterial=GetMaterial(color);
   return go;
  }
  private static Material GetMaterial(Color color)
  {
   Color32 c=color;int key=(c.r<<24)|(c.g<<16)|(c.b<<8)|c.a;Material m;
   if(Materials.TryGetValue(key,out m)&&m!=null)return m;
   Shader shader=RuntimeMaterialFactory.GetShader();if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");if(shader==null)shader=Shader.Find("Unlit/Color");
   if(shader==null)return null;
   m=new Material(shader);m.color=color;if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.18f);Materials[key]=m;return m;
  }
  private void Update()
  {
   transform.Rotate(0f,rotateSpeed*Time.deltaTime,0f,Space.World);
   Vector3 pos=transform.position;pos.y=startY+Mathf.Sin(Time.time*3f)*bobHeight;transform.position=pos;
  }
  private void OnTriggerEnter(Collider other)
  {
   if(collected)return;
   PlayerController player=other.GetComponentInParent<PlayerController>();
   if(player==null||player.IsDefeated)return;
   collected=true;
   switch(type)
   {
    case PickupType.Ammo:if(player.Weapon!=null)player.Weapon.AddReserveAmmo(amount);break;
    case PickupType.Medkit:player.Heal(amount);break;
    case PickupType.Grenade:PlayerInventory inv=player.GetComponent<PlayerInventory>();if(inv!=null)inv.AddGrenades(amount);break;
    case PickupType.Shield:player.AddShield(amount);break;
    case PickupType.Weapon:
     if (player.Weapon != null) player.Weapon.EquipWeaponFromPickup(weaponKind, amount);
     break;
    case PickupType.QuickDash:player.AddQuickDashCharges(2);break;
   }
   Destroy(gameObject);
  }
 }
 public sealed class PlayerInventory:MonoBehaviour
 {
  [SerializeField] private int grenades=3;
  public int Grenades=>grenades;
  public void AddGrenades(int amount){grenades=Mathf.Clamp(grenades+Mathf.Max(0,amount),0,9);}
  public bool TryConsumeGrenade(){if(grenades<=0)return false;grenades--;return true;}
 }
}