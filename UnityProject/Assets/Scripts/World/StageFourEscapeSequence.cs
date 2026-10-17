using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Isolated Stage 4 escape finale. Runtime bootstrap attaches this only while
/// StageFourWorldBuilder is present and built; Stages 1-3 are never touched.
/// </summary>
public sealed class StageFourEscapeSequence : MonoBehaviour
{
    private const string WorldRootName = "StageFourMothershipMegabase";
    private static StageFourEscapeSequence active;
    private Transform player;
    private Transform worldRoot;
    private Transform escapeShuttle;
    private GameObject blueDoor;
    private readonly List<GameObject> plantedBombs = new List<GameObject>();
    private readonly List<Vector3> bombPositions = new List<Vector3>();
    private Vector3 lastBombPosition;
    private float distanceSinceBomb;
    private bool finaleStarted;
    private bool doorOpened;
    private bool boarded;
    private Camera sceneCamera;
    private Material blueMat, bombMat, blastMat;
    private GUIStyle messageStyle;
    private string message = "REACH THE BLUE ESCAPE HATCH";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallBootstrap()
    {
        if (GameObject.Find("StageFourEscapeSequenceBootstrap") != null) return;
        var go = new GameObject("StageFourEscapeSequenceBootstrap");
        DontDestroyOnLoad(go);
        go.AddComponent<StageFourEscapeBootstrap>();
    }

    private void Awake()
    {
        active = this;
        worldRoot = GameObject.Find(WorldRootName)?.transform;
        var p = FindFirstObjectByType<PlayerController>();
        player = p != null ? p.transform : null;
        sceneCamera = Camera.main;
        blueMat = MakeMaterial("S4_EscapeBlue", new Color(0.05f, 0.25f, 1f), new Color(0.08f, 0.3f, 1f));
        bombMat = MakeMaterial("S4_TimedBomb", new Color(0.95f, 0.12f, 0.035f), new Color(1f, 0.025f, 0.005f));
        blastMat = MakeMaterial("S4_FinalBlast", new Color(1f, 0.48f, 0.08f), new Color(1f, 0.15f, 0.015f));
        if (player != null) lastBombPosition = player.position;
        BuildEscapeHallAndDoor();
    }

    private void Update()
    {
        if (player == null || worldRoot == null || finaleStarted) return;
        if (Vector3.Distance(player.position, lastBombPosition) >= 10f)
        {
            PlantTimedBomb(player.position);
            lastBombPosition = player.position;
        }

        // The blue hatch sits beyond the command room, on the forward (+Z) route.
        if (!doorOpened && player.position.z >= 116f)
            OpenBlueDoor();
        if (doorOpened && !boarded && player.position.z >= 130f)
        {
            boarded = true;
            StartCoroutine(PlayEscapeFinale());
        }
    }

    private void BuildEscapeHallAndDoor()
    {
        if (worldRoot == null) return;
        // Open the center of the command-room rear wall so the escape corridor is reachable.
        Transform commandRear = worldRoot.Find("S4_InteriorCommandRearWall");
        if (commandRear != null)
        {
            var wallRenderer = commandRear.GetComponent<Renderer>();
            var wallCollider = commandRear.GetComponent<Collider>();
            if (wallRenderer != null) wallRenderer.enabled = false;
            if (wallCollider != null) wallCollider.enabled = false;
        }
        var wallMat = MakeMaterial("S4_EscapeWall", new Color(0.2f, 0.3f, 0.4f));
        MakeBox("S4_EscapeCommandWallLeft", new Vector3(-20f, 6.5f, 103f), new Vector3(12f, 13f, 1.5f), wallMat);
        MakeBox("S4_EscapeCommandWallRight", new Vector3(20f, 6.5f, 103f), new Vector3(12f, 13f, 1.5f), wallMat);
        MakeBox("S4_EscapeCorridorFloor", new Vector3(0f, -0.35f, 112f), new Vector3(24f, 0.7f, 32f), MakeMaterial("S4_EscapeFloor", new Color(0.07f, 0.1f, 0.15f)));
        MakeBox("S4_EscapeCorridorLeft", new Vector3(-12f, 6f, 112f), new Vector3(1.4f, 12f, 32f), MakeMaterial("S4_EscapeWall", new Color(0.2f, 0.3f, 0.4f)));
        MakeBox("S4_EscapeCorridorRight", new Vector3(12f, 6f, 112f), new Vector3(1.4f, 12f, 32f), MakeMaterial("S4_EscapeWall", new Color(0.2f, 0.3f, 0.4f)));
        MakeBox("S4_EscapeCorridorCeiling", new Vector3(0f, 13f, 112f), new Vector3(24f, 1.2f, 32f), MakeMaterial("S4_EscapeCeiling", new Color(0.035f, 0.055f, 0.085f)));
        blueDoor = MakeBox("S4_BlueEscapeDoor", new Vector3(0f, 6f, 127f), new Vector3(10f, 11f, 1.2f), blueMat);
        MakeBox("S4_EscapeDoorFrameL", new Vector3(-5.7f, 6f, 127f), new Vector3(1f, 12f, 2f), blueMat);
        MakeBox("S4_EscapeDoorFrameR", new Vector3(5.7f, 6f, 127f), new Vector3(1f, 12f, 2f), blueMat);
        MakeBox("S4_EscapeDoorHeader", new Vector3(0f, 12f, 127f), new Vector3(12.5f, 1f, 2f), blueMat);
        escapeShuttle = BuildEscapeShuttle(new Vector3(0f, 1.8f, 143f));
    }

    private Transform BuildEscapeShuttle(Vector3 pos)
    {
        var craft = new GameObject("S4_FinalEscapeShuttle").transform;
        craft.SetParent(worldRoot, false);
        craft.localPosition = pos;
        MakeChildBox(craft, "Hull", Vector3.zero, new Vector3(5f, 1.8f, 9f), MakeMaterial("S4_ShuttleHull", new Color(0.55f, 0.68f, 0.78f)));
        MakeChildBox(craft, "Nose", new Vector3(0f, 0.1f, 4.4f), new Vector3(3.1f, 1.3f, 2.2f), MakeMaterial("S4_ShuttleNose", new Color(0.72f, 0.82f, 0.9f)));
        MakeChildBox(craft, "Cockpit", new Vector3(0f, 1.05f, 0.9f), new Vector3(2.5f, 0.6f, 2.4f), MakeMaterial("S4_ShuttleGlass", new Color(0.02f, 0.25f, 0.38f), new Color(0.02f, 0.4f, 0.8f)));
        for (int side = -1; side <= 1; side += 2)
        {
            MakeChildBox(craft, "Wing" + side, new Vector3(side * 3.2f, 0f, -0.7f), new Vector3(2.4f, 0.35f, 4.8f), MakeMaterial("S4_ShuttleWing", new Color(0.25f, 0.35f, 0.45f)));
            MakeChildBox(craft, "Engine" + side, new Vector3(side * 1.45f, 0.1f, -4.5f), new Vector3(0.9f, 1f, 1.4f), blueMat);
        }
        return craft;
    }

    private void OpenBlueDoor()
    {
        doorOpened = true;
        message = "ESCAPE HATCH OPEN • BOARD THE SHUTTLE";
        if (blueDoor != null)
        {
            var renderer = blueDoor.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
            var collider = blueDoor.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }
        if (escapeShuttle != null) escapeShuttle.gameObject.SetActive(true);
    }

    private void PlantTimedBomb(Vector3 position)
    {
        Vector3 p = new Vector3(position.x, 0.35f, position.z);
        var bomb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bomb.name = "S4_TimedBomb_" + plantedBombs.Count;
        bomb.transform.SetParent(worldRoot, false);
        bomb.transform.position = p;
        bomb.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f);
        bomb.GetComponent<Renderer>().material = bombMat;
        plantedBombs.Add(bomb);
        bombPositions.Add(p);
        StartCoroutine(DetonateBomb(bomb, plantedBombs.Count > 5 ? 5.5f : 7.5f));
    }

    private IEnumerator DetonateBomb(GameObject bomb, float delay)
    {
        float t = 0f;
        var renderer = bomb != null ? bomb.GetComponent<Renderer>() : null;
        while (t < delay && bomb != null)
        {
            t += Time.deltaTime;
            if (renderer != null) renderer.enabled = Mathf.FloorToInt(t * 4f) % 2 == 0;
            yield return null;
        }
        if (bomb != null)
        {
            Vector3 p = bomb.transform.position;
            CreateBlast("S4_BombBlast", p, 2.8f);
            Destroy(bomb);
        }
    }

    private IEnumerator PlayEscapeFinale()
    {
        finaleStarted = true;
        message = "ESCAPE SHUTTLE BOARDING";
        if (player != null)
        {
            var controller = player.GetComponent<PlayerController>();
            if (controller != null) controller.SetMovementEnabled(false);
        }
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (player != null && escapeShuttle != null)
        {
            player.position = escapeShuttle.position + new Vector3(0f, 0f, -1.5f);
            var renderers = player.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) if (r != null) r.enabled = false;
        }
        yield return new WaitForSeconds(0.8f);
        message = "SHUTTLE RELEASED • MOTHERSHIP CRITICAL";
        float t = 0f;
        Vector3 start = escapeShuttle != null ? escapeShuttle.position : new Vector3(0f, 2f, 143f);
        while (t < 3.2f)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / 3.2f);
            if (escapeShuttle != null) escapeShuttle.position = start + new Vector3(0f, 1.5f * f, 55f * f);
            if (sceneCamera != null && escapeShuttle != null)
            {
                Vector3 target = escapeShuttle.position;
                sceneCamera.transform.position = target + new Vector3(16f, 11f, -28f);
                sceneCamera.transform.LookAt(target + Vector3.forward * 8f);
            }
            yield return null;
        }

        // Six distinct detonation beats make the whole mother-ship/base collapse readable.
        Vector3[] blasts =
        {
            new Vector3(-55f, 22f, -70f), new Vector3(42f, 28f, -42f),
            new Vector3(-28f, 39f, 4f), new Vector3(35f, 42f, 38f),
            new Vector3(0f, 34f, 83f), new Vector3(0f, 18f, 0f)
        };
        for (int i = 0; i < blasts.Length; i++)
        {
            message = "MOTHERSHIP CORE FAILURE • " + (i + 1) + "/6";
            CreateBlast("S4_MothershipDetonation_" + (i + 1), blasts[i], 18f + i * 2.5f);
            if (sceneCamera != null) sceneCamera.transform.position += new Vector3((i % 2 == 0 ? 1f : -1f) * 0.35f, 0.15f, 0f);
            yield return new WaitForSeconds(0.48f);
        }
        message = "MOTHERSHIP AND ORBITAL BASE DESTROYED";
        // Fade exterior and ship structures, but leave the escape craft visible.
        if (worldRoot != null)
        {
            var all = worldRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var r in all)
            {
                if (r == null || r.transform.IsChildOf(escapeShuttle)) continue;
                if (r.name.Contains("EscapeShuttle")) continue;
                r.enabled = false;
            }
        }
        yield return new WaitForSeconds(1.8f);
        message = "STAGE 4 COMPLETE";
    }

    private void CreateBlast(string name, Vector3 position, float radius)
    {
        var blast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blast.name = name;
        blast.transform.SetParent(worldRoot, false);
        blast.transform.position = position;
        blast.transform.localScale = Vector3.one * 0.2f;
        var col = blast.GetComponent<Collider>();
        if (col != null) Destroy(col);
        blast.GetComponent<Renderer>().material = blastMat;
        StartCoroutine(ExpandBlast(blast, radius));
    }

    private IEnumerator ExpandBlast(GameObject blast, float radius)
    {
        float t = 0f;
        while (t < 0.55f && blast != null)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / 0.55f);
            blast.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, radius, f);
            yield return null;
        }
        if (blast != null) Destroy(blast, 0.6f);
    }

    private void OnGUI()
    {
        if (!finaleStarted && string.IsNullOrEmpty(message)) return;
        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.label);
            messageStyle.fontSize = Mathf.Max(20, Screen.height / 28);
            messageStyle.alignment = TextAnchor.UpperCenter;
            messageStyle.fontStyle = FontStyle.Bold;
            messageStyle.normal.textColor = Color.white;
        }
        GUI.Label(new Rect(0, 20, Screen.width, 60), message, messageStyle);
    }

    private GameObject MakeBox(string name, Vector3 pos, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(worldRoot, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material = material;
        return go;
    }

    private void MakeChildBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material = material;
    }

    private Material MakeMaterial(string name, Color color, Color? emission = null)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(shader) { name = name, color = color };
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        return m;
    }

}

 
 public sealed class StageFourEscapeBootstrap : MonoBehaviour
 {
     private void Update()
     {
         var builders = FindObjectsByType<StageFourWorldBuilder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
         foreach (var builder in builders)
         {
             if (builder == null || !builder.IsBuilt || builder.GetComponent<StageFourEscapeSequence>() != null) continue;
             builder.gameObject.AddComponent<StageFourEscapeSequence>();
         }
     }
 }
 