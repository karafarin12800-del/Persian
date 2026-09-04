using UnityEngine;
using PersiaWar.Unity2D5D;

/// <summary>
/// Lightweight visual layer for the prototype. It keeps PlayerController intact
/// and styles its existing PlayerVisual as a compact, toy-like Persian hero.
/// Sprite sheets can be connected later without changing gameplay code.
/// </summary>
public sealed class PrototypeCharacterVisual : MonoBehaviour
{
    private int hero;
    private Transform root;

    public void SetHero(int index)
    {
        hero = Mathf.Clamp(index, 0, 1);
        if (root == null) root = transform.Find("PlayerVisual");
        if (root == null) return;

        // Keep the existing gameplay rig and only adjust presentation.
        root.localScale = hero == 0 ? new Vector3(1.05f, 1.08f, 1.05f) : new Vector3(0.92f, 1.12f, 0.92f);

        Transform body = root.Find("Body");
        Transform head = root.Find("Head");
        Transform crown = root.Find("Crown");
        Transform armor = root.Find("ShoulderArmor");

        if (hero == 0)
        {
            SetMaterial(body, new Color(0.22f, 0.29f, 0.38f));
            SetMaterial(head, new Color(0.72f, 0.45f, 0.27f));
            SetMaterial(crown, new Color(0.88f, 0.63f, 0.10f));
            SetMaterial(armor, new Color(0.10f, 0.14f, 0.19f));
        }
        else
        {
            SetMaterial(body, new Color(0.12f, 0.27f, 0.34f));
            SetMaterial(head, new Color(0.82f, 0.57f, 0.38f));
            SetMaterial(crown, new Color(0.48f, 0.19f, 0.10f));
            SetMaterial(armor, new Color(0.12f, 0.16f, 0.20f));
        }
    }

    private static void SetMaterial(Transform target, Color color)
    {
        if (target == null) return;
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = RuntimeMaterialFactory.Create(target.name + "PrototypeMaterial", color);
    }
}
