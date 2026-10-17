using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PersiaWar.Unity2D5D;

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
    private readonly List<Transform> bombSites = new List<Transform>();
    private readonly List<GameObject> bombSiteIndicators = new List<GameObject>();
    private Vector3 lastBombPosition;
    private bool finaleStarted;
    private bool doorOpened;
    private bool boarded;
    private bool chargesPickedUp;
    private int chargesInInventory;
    private bool requestPickup;
    private bool requestPlant;
    private GameObject supplyCrate;
    private GameObject redDoorPanel;
    private Camera sceneCamera;
    private Material redDoorMat, doorPanelMat, bombMat, blastMat, supplyMat, greenBeaconMat;
    private GUIStyle messageStyle;
    private string message = "FIND THE CHARGE SUPPLY BOX";

    private void Awake()
    {
        active = this;
        worldRoot = GameObject.Find(WorldRootName)?.transform;
        var p = FindFirstObjectByType<PlayerController>();
        player = p != null ? p.transform : null;
        sceneCamera = Camera.main;
        redDoorMat = MakeMaterial("S4_EscapeRedWarning", new Color(1f, 0.035f, 0.015f), new Color(2.2f, 0.025f, 0.008f));
        bombMat = MakeMaterial("S4_TimedBomb", new Color(0.95f, 0.12f, 0.035f), new Color(1f, 0.025f, 0.005f));
        blastMat = MakeMaterial("S4_FinalBlast", new Color(1f, 0.48f, 0.08f), new Color(1f, 0.15f, 0.015f));
        if (player != null) lastBombPosition = player.position;
        BuildEscapeHallAndDoor();
    }

    private void Update()
    {
        if (player == null || worldRoot == null || finaleStarted) return;

        if (!chargesPickedUp && supplyCrate != null &&
            Vector3.Distance(player.position, supplyCrate.transform.position) <= 4.5f)
        {
            if (requestPickup)
            {
                requestPickup = false;
                chargesPickedUp = true;
                chargesInInventory = 6;
                supplyCrate.SetActive(false);
                message = "6 EXPLOSIVE CHARGES COLLECTED • VISIT ALL 6 RED CRATES";
                StartupCheckpoint.Set("StageFourSixChargesCollected");
            }
        }
        else requestPickup = false;

        int nearbySite = FindNearbyBombSite();
        if (requestPlant)
        {
            requestPlant = false;
            if (!chargesPickedUp)
                message = "FIND THE SUPPLY BOX FIRST";
            else if (nearbySite < 0)
                message = "MOVE TO A RED CHARGE STATION";
            else if (chargesInInventory <= 0)
                message = "NO CHARGES LEFT • " + plantedBombs.Count + "/6 PLANTED";
            else if (bombSiteIndicators[nearbySite] != null && bombSiteIndicators[nearbySite].activeSelf)
                message = "THIS CHARGE STATION IS ALREADY ARMED";
            else
                PlantTimedBomb(nearbySite);
        }

        // The exit stays locked until every fixed charge station is armed.
        float doorDistance = redDoorPanel != null
            ? Vector3.Distance(player.position, redDoorPanel.transform.position)
            : float.MaxValue;
        if (!doorOpened && doorDistance <= 8f)
        {
            if (plantedBombs.Count >= 6)
                OpenRedDoor();
            else
                message = "LOCKED • PLANT CHARGES: " + plantedBombs.Count + "/6";
        }

        // Crossing beyond the red frame automatically starts the shuttle cutscene.
        if (doorOpened && !boarded && player.position.z >= 132f &&
            Mathf.Abs(player.position.x) <= 10f)
        {
            if (plantedBombs.Count < 6)
            {
                message = "RETURN TO THE RED STATIONS • " + plantedBombs.Count + "/6";
                return;
            }
            boarded = true;
            StartCoroutine(PlayEscapeFinale());
        }
    }

    private int FindNearbyBombSite()
    {
        if (player == null) return -1;
        for (int i = 0; i < bombSites.Count; i++)
        {
            if (bombSites[i] != null && Vector3.Distance(player.position, bombSites[i].position) <= 3.4f)
                return i;
        }
        return -1;
    }

    private void BuildEscapeHallAndDoor()
    {
        if (worldRoot == null) return;
        Transform commandRear = worldRoot.Find("S4_InteriorCommandRearWall");
        if (commandRear != null)
        {
            var wallRenderer = commandRear.GetComponent<Renderer>();
            var wallCollider = commandRear.GetComponent<Collider>();
            if (wallRenderer != null) wallRenderer.enabled = false;
            if (wallCollider != null) wallCollider.enabled = false;
        }

        var wallMat = MakeMaterial("S4_EscapeWall", new Color(0.2f, 0.3f, 0.4f));
        supplyMat = MakeMaterial("S4_ChargeSupplyCrate", new Color(0.45f, 0.22f, 0.08f));
        greenBeaconMat = MakeMaterial("S4_ChargeGreenBeacon", new Color(0.1f, 1f, 0.2f), new Color(0.1f, 2f, 0.15f));
        doorPanelMat = MakeMaterial("S4_ExitDoorPanel", new Color(0.06f, 0.09f, 0.13f));
        MakeBox("S4_EscapeCommandWallLeft", new Vector3(-20f, 6.5f, 103f), new Vector3(12f, 13f, 1.5f), wallMat);
        MakeBox("S4_EscapeCommandWallRight", new Vector3(20f, 6.5f, 103f), new Vector3(12f, 13f, 1.5f), wallMat);
        MakeBox("S4_EscapeCorridorFloor", new Vector3(0f, -0.35f, 112f), new Vector3(24f, 0.7f, 32f), MakeMaterial("S4_EscapeFloor", new Color(0.07f, 0.1f, 0.15f)));
        MakeBox("S4_EscapeCorridorLeft", new Vector3(-12f, 6f, 112f), new Vector3(1.4f, 12f, 32f), wallMat);
        MakeBox("S4_EscapeCorridorRight", new Vector3(12f, 6f, 112f), new Vector3(1.4f, 12f, 32f), wallMat);
        MakeBox("S4_EscapeCorridorCeiling", new Vector3(0f, 13f, 112f), new Vector3(24f, 1.2f, 32f), MakeMaterial("S4_EscapeCeiling", new Color(0.035f, 0.055f, 0.085f)));

        // Red is restricted to the frame/beacons; the actual door panel is dark metal
        // and sits slightly forward of the frame so crossing the threshold is obvious.
        redDoorPanel = MakeBox("S4_RedEscapeDoorPanel", new Vector3(0f, 6f, 128.2f), new Vector3(9.8f, 10.8f, 0.8f), doorPanelMat);
        MakeBox("S4_EscapeDoorFrameL", new Vector3(-5.7f, 6f, 127f), new Vector3(0.45f, 12f, 0.7f), redDoorMat);
        MakeBox("S4_EscapeDoorFrameR", new Vector3(5.7f, 6f, 127f), new Vector3(0.45f, 12f, 0.7f), redDoorMat);
        MakeBox("S4_EscapeDoorHeader", new Vector3(0f, 12f, 127f), new Vector3(11.8f, 0.45f, 0.7f), redDoorMat);
        MakeBox("S4_EscapeDoorBeaconL", new Vector3(-6.4f, 6f, 127f), new Vector3(0.22f, 12.5f, 0.22f), redDoorMat);
        MakeBox("S4_EscapeDoorBeaconR", new Vector3(6.4f, 6f, 127f), new Vector3(0.22f, 12.5f, 0.22f), redDoorMat);
        CreateDoorLabel();
        escapeShuttle = BuildEscapeShuttle(new Vector3(0f, 1.8f, 146f));
        escapeShuttle.gameObject.SetActive(false);

        // The player must find the charge box in the side of the command room.
        supplyCrate = MakeBox("S4_ChargeSupplyBox", new Vector3(20f, 1.1f, 88f), new Vector3(3.2f, 2.2f, 2.6f), supplyMat);
        MakeBox("S4_ChargeSupplyBoxLid", new Vector3(20f, 2.25f, 88f), new Vector3(3.35f, 0.18f, 2.72f), redDoorMat);
        CreateWorldLabel("S4_ChargeSupplyLabel", new Vector3(20f, 4f, 88f), "EXPLOSIVE CHARGES • 6", new Color(1f, 0.72f, 0.2f));

        // Six fixed stations run from the aft engine room to the forward command/exit side.
        float[] stationZ = { -88f, -61f, -24f, 22f, 60f, 92f };
        for (int i = 0; i < stationZ.Length; i++)
        {
            var site = MakeBox("S4_RedChargeStation_" + (i + 1), new Vector3(0f, 0.45f, stationZ[i]), new Vector3(2.2f, 0.9f, 1.8f), redDoorMat);
            site.GetComponent<Collider>().enabled = false;
            bombSites.Add(site.transform);
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = "S4_ChargeStationGreenBeacon_" + (i + 1);
            indicator.transform.SetParent(worldRoot, false);
            indicator.transform.position = new Vector3(0f, 1.2f, stationZ[i]);
            indicator.transform.localScale = Vector3.one * 0.38f;
            var col = indicator.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var renderer = indicator.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = greenBeaconMat;
            indicator.SetActive(false);
            bombSiteIndicators.Add(indicator);
            CreateWorldLabel("S4_ChargeStationLabel_" + (i + 1), new Vector3(0f, 2.3f, stationZ[i]), "CHARGE " + (i + 1), new Color(1f, 0.35f, 0.2f));
        }
    }

    private void CreateWorldLabel(string name, Vector3 position, string value, Color color)
    {
        var labelObject = new GameObject(name);
        labelObject.transform.SetParent(worldRoot, false);
        labelObject.transform.position = position;
        var text = labelObject.AddComponent<TextMesh>();
        text.text = value;
        text.characterSize = 0.28f;
        text.fontSize = 64;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
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
            MakeChildBox(craft, "Engine" + side, new Vector3(side * 1.45f, 0.1f, -4.5f), new Vector3(0.9f, 1f, 1.4f), redDoorMat);
        }
        return craft;
    }

    private void OpenRedDoor()
    {
        doorOpened = true;
        message = "ALL 6 CHARGES ARMED • EXIT OPEN • BOARD SHUTTLE";
        if (redDoorPanel != null)
        {
            var renderer = redDoorPanel.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
            var collider = redDoorPanel.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }
        if (escapeShuttle != null) escapeShuttle.gameObject.SetActive(true);
        StartupCheckpoint.Set("StageFourRedEscapeDoorOpened");
    }

    private void CreateDoorLabel()
    {
        CreateWorldLabel("S4_RedEscapeDoorLabel", new Vector3(0f, 14.1f, 126.2f),
            "ESCAPE • 6 CHARGES REQUIRED", new Color(1f, 0.12f, 0.06f));
    }

    private void PlantTimedBomb(int siteIndex)
    {
        if (siteIndex < 0 || siteIndex >= bombSites.Count ||
            siteIndex >= bombSiteIndicators.Count || chargesInInventory <= 0 ||
            bombSiteIndicators[siteIndex] == null || bombSiteIndicators[siteIndex].activeSelf)
            return;

        Vector3 p = bombSites[siteIndex].position + Vector3.up * 0.8f;
        var bomb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bomb.name = "S4_TimedCharge_" + (siteIndex + 1);
        bomb.transform.SetParent(worldRoot, false);
        bomb.transform.position = p;
        bomb.transform.localScale = new Vector3(0.58f, 0.22f, 0.58f);
        var collider = bomb.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        var renderer = bomb.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = bombMat;

        bombPositions.Add(bombSites[siteIndex].position);
        plantedBombs.Add(bomb);
        chargesInInventory--;
        bombSiteIndicators[siteIndex].SetActive(true);
        message = "CHARGE PLANTED • " + plantedBombs.Count + "/6 • GREEN BEACON ACTIVE";
        StartupCheckpoint.Set("StageFourChargePlantedAtStation_" + (siteIndex + 1) + "_of_6");
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
        message = "CHARGES ARMED: 6/6 • SHUTTLE RELEASED";
        float t = 0f;
        int nextCharge = 0;
        float nextChargeTime = 0.25f;
        Vector3 start = escapeShuttle != null ? escapeShuttle.position : new Vector3(0f, 2f, 143f);
        while (t < 3.2f)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / 3.2f);
            if (escapeShuttle != null) escapeShuttle.position = start + new Vector3(0f, 1.5f * f, 55f * f);
            if (nextCharge < plantedBombs.Count && t >= nextChargeTime)
            {
                message = "CHARGE DETONATION • " + (nextCharge + 1) + "/6";
                CreateBlast("S4_ChargeDetonation_" + (nextCharge + 1), bombPositions[nextCharge], 5.5f + nextCharge * 0.45f);
                if (plantedBombs[nextCharge] != null) Destroy(plantedBombs[nextCharge]);
                nextCharge++;
                nextChargeTime += 0.45f;
            }
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
        if (string.IsNullOrEmpty(message)) return;
        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.label);
            messageStyle.fontSize = Mathf.Max(20, Screen.height / 30);
            messageStyle.alignment = TextAnchor.UpperCenter;
            messageStyle.fontStyle = FontStyle.Bold;
            messageStyle.normal.textColor = Color.white;
            messageStyle.wordWrap = true;
        }
        GUI.Label(new Rect(8, 18, Screen.width - 16, 70), message, messageStyle);

        GUIStyle chargeStyle = new GUIStyle(messageStyle);
        chargeStyle.fontSize = Mathf.Max(18, Screen.height / 34);
        chargeStyle.normal.textColor = new Color(1f, 0.32f, 0.12f);
        GUI.Label(new Rect(0, 88, Screen.width, 40),
            "BOMBS: " + plantedBombs.Count + "/6 PLANTED   •   " + chargesInInventory + "/6 CARRIED", chargeStyle);

        if (finaleStarted) return;
        int site = FindNearbyBombSite();
        bool nearCrate = !chargesPickedUp && supplyCrate != null && supplyCrate.activeSelf &&
            player != null && Vector3.Distance(player.position, supplyCrate.transform.position) <= 4.5f;
        bool nearEmptySite = site >= 0 && site < bombSiteIndicators.Count &&
            bombSiteIndicators[site] != null && !bombSiteIndicators[site].activeSelf;
        float buttonW = Mathf.Min(Screen.width - 40, 420);
        Rect buttonRect = new Rect((Screen.width - buttonW) * 0.5f, Screen.height - 142, buttonW, 58);
        if (nearCrate && GUI.Button(buttonRect, "TAKE 6 CHARGES"))
            requestPickup = true;
        else if (nearEmptySite && chargesPickedUp && chargesInInventory > 0 &&
                 GUI.Button(buttonRect, "PLANT CHARGE HERE"))
            requestPlant = true;
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
        // StageFourWorldBuilder uses the project's Android-safe shader lookup because
        // Standard/URP shaders can be stripped from an IL2CPP player. Use the same
        // fallback here: this component is created during Stage 4, so a missing shader
        // must not leave the red exit, charge markers, and finale materials null.
        Shader shader = RuntimeMaterialFactory.GetShader();
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
            throw new System.InvalidOperationException("No compatible runtime shader found for Stage 4 escape effects.");

        var m = new Material(shader) { name = name, color = color };
        if (m.HasProperty("_Color"))
            m.SetColor("_Color", color);
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", color);
        if (m.HasProperty("_MainTex") && m.mainTexture == null)
            m.mainTexture = Texture2D.whiteTexture;
        if (emission.HasValue && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        return m;
    }

}


 