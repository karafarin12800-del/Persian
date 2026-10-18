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
    private readonly List<GameObject> plantedBombs = new List<GameObject>();
    private readonly List<GameObject> storedCharges = new List<GameObject>();
    private readonly List<Vector3> bombPositions = new List<Vector3>();
    private readonly List<Transform> bombSites = new List<Transform>();
    private readonly List<GameObject> bombSiteIndicators = new List<GameObject>();
    private bool finaleStarted;
    private bool doorOpened;
    private bool boarded;
    private bool chargesPickedUp;
    private bool supplyBoxOpened;
    private bool requestOpenSupplyBox;
    private bool requestPickup;
    private bool requestPlant;
    private bool requestOpenDoor;
    private int chargesInInventory;
    private GameObject supplyCrate;
    private Transform supplyLidHinge;
    private GameObject redDoorPanel;
    private Material redBeaconMat;
    private Camera sceneCamera;
    private Material redDoorMat, doorPanelMat, bombMat, blastMat, supplyMat, greenBeaconMat;
    private GUIStyle messageStyle;
    private string message = "FIND THE CHARGE SUPPLY BOX";

    public static bool IsFinaleActive => active != null && active.finaleStarted;

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
        BuildEscapeHallAndDoor();
    }

    private void Update()
    {
        if (player == null || worldRoot == null || finaleStarted) return;

        bool nearSupplyBox = supplyCrate != null && supplyCrate.activeSelf &&
            Vector3.Distance(player.position, supplyCrate.transform.position) <= 4.5f;
        if (requestOpenSupplyBox)
        {
            requestOpenSupplyBox = false;
            if (!chargesPickedUp && nearSupplyBox && !supplyBoxOpened)
            {
                supplyBoxOpened = true;
                if (supplyLidHinge != null) StartCoroutine(OpenSupplyLidRoutine());
                message = "SUPPLY BOX OPEN • TAKE THE 6 CHARGES";
                StartupCheckpoint.Set("StageFourSupplyBoxOpened");
            }
        }
        if (requestPickup)
        {
            requestPickup = false;
            if (nearSupplyBox && supplyBoxOpened && !chargesPickedUp)
            {
                chargesPickedUp = true;
                chargesInInventory = 6;
                for (int i = 0; i < storedCharges.Count; i++)
                    if (storedCharges[i] != null) Destroy(storedCharges[i]);
                storedCharges.Clear();
                message = "6 SQUARE CHARGES COLLECTED • VISIT ALL 6 RED STATIONS";
                StartupCheckpoint.Set("StageFourSixChargesCollected");
            }
        }

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

        // The exit is physically inside the corridor and requires a deliberate tap.
        float doorDistance = redDoorPanel != null
            ? Vector3.Distance(player.position, redDoorPanel.transform.position)
            : float.MaxValue;
        if (requestOpenDoor)
        {
            requestOpenDoor = false;
            if (doorDistance <= 8f && plantedBombs.Count >= 6)
                OpenRedDoor();
            else if (doorDistance <= 8f)
                message = "EXIT LOCKED • PLANT ALL 6 CHARGES FIRST";
        }

        // After manually opening the door, crossing its threshold starts the shuttle cutscene.
        if (doorOpened && !boarded && player.position.z >= 114f &&
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

        // The door sits within the playable corridor, not beyond the map boundary.
        // Only its frame is red; the dark panel slides out after the player taps OPEN.
        redDoorPanel = MakeBox("S4_RedEscapeDoorPanel", new Vector3(0f, 2.35f, 110f), new Vector3(7.8f, 4.7f, 0.8f), doorPanelMat);
        MakeBox("S4_EscapeDoorFrameL", new Vector3(-4.5f, 2.55f, 109f), new Vector3(0.38f, 5.4f, 0.7f), redDoorMat);
        MakeBox("S4_EscapeDoorFrameR", new Vector3(4.5f, 2.55f, 109f), new Vector3(0.38f, 5.4f, 0.7f), redDoorMat);
        MakeBox("S4_EscapeDoorHeader", new Vector3(0f, 5.1f, 109f), new Vector3(9.4f, 0.38f, 0.7f), redDoorMat);
        CreateDoorLabel();
        escapeShuttle = BuildEscapeShuttle(new Vector3(0f, 1.4f, 124f));
        escapeShuttle.gameObject.SetActive(false);

        // The player must find the charge box in the side of the command room.
        supplyCrate = MakeBox("S4_ChargeSupplyBox", new Vector3(5f, 1.1f, -67f), new Vector3(3.2f, 2.2f, 2.6f), supplyMat);
        // A real hinged lid: the player must open it before the pickup action appears.
        var hinge = new GameObject("S4_ChargeSupplyBoxHinge");
        hinge.transform.SetParent(worldRoot, false);
        hinge.transform.localPosition = new Vector3(3.325f, 2.25f, -67f);
        supplyLidHinge = hinge.transform;
        var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "S4_ChargeSupplyBoxLid";
        lid.transform.SetParent(supplyLidHinge, false);
        lid.transform.localPosition = new Vector3(1.675f, 0f, 0f);
        lid.transform.localScale = new Vector3(3.35f, 0.18f, 2.72f);
        lid.GetComponent<Renderer>().material = redDoorMat;
        redBeaconMat = MakeMaterial("S4_ChargeRedBeacon", new Color(1f, 0.025f, 0.01f), new Color(2f, 0.015f, 0.005f));
        // Six visible square charges sit inside the box with green status LEDs until collected.
        for (int i = 0; i < 6; i++)
        {
            float x = 4.15f + (i % 3) * 0.82f;
            float z = -67.55f + (i / 3) * 0.92f;
            var stored = MakeBox("S4_StoredSquareCharge_" + (i + 1), new Vector3(x, 1.48f, z), new Vector3(0.58f, 0.48f, 0.58f), bombMat);
            storedCharges.Add(stored);
            var storedLed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            storedLed.name = "S4_StoredChargeGreenLED_" + (i + 1);
            storedLed.transform.SetParent(stored.transform, false);
            storedLed.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            storedLed.transform.localScale = Vector3.one * 0.18f;
            var storedCol = storedLed.GetComponent<Collider>();
            if (storedCol != null) Destroy(storedCol);
            var storedRenderer = storedLed.GetComponent<Renderer>();
            if (storedRenderer != null) storedRenderer.sharedMaterial = greenBeaconMat;
        }
        // Keep the supply box unlabelled; the interaction prompt appears only nearby.

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
            // No floating text over planting stations; the nearby action button is enough.
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
        // A compact, streamlined craft that fits inside the boarding corridor.
        var craft = new GameObject("S4_FinalEscapeShuttle").transform;
        craft.SetParent(worldRoot, false);
        craft.localPosition = pos;

        MakeChildPrimitive(craft, PrimitiveType.Sphere, "ShuttleHull",
            Vector3.zero, new Vector3(3.6f, 1.15f, 7.3f),
            MakeMaterial("S4_ShuttleHull", new Color(0.58f, 0.70f, 0.80f)));
        MakeChildPrimitive(craft, PrimitiveType.Sphere, "ShuttleNose",
            new Vector3(0f, 0.02f, 3.25f), new Vector3(2.0f, 0.72f, 3.1f),
            MakeMaterial("S4_ShuttleNose", new Color(0.78f, 0.86f, 0.92f)));
        MakeChildPrimitive(craft, PrimitiveType.Sphere, "ShuttleCockpit",
            new Vector3(0f, 0.68f, 0.72f), new Vector3(1.7f, 0.64f, 2.0f),
            MakeMaterial("S4_ShuttleGlass", new Color(0.02f, 0.22f, 0.34f), new Color(0.02f, 0.35f, 0.8f)));

        for (int side = -1; side <= 1; side += 2)
        {
            MakeChildBox(craft, "SweptWing" + side,
                new Vector3(side * 2.15f, -0.12f, -0.65f),
                new Vector3(1.65f, 0.22f, 3.6f),
                MakeMaterial("S4_ShuttleWing", new Color(0.27f, 0.39f, 0.49f)));
            MakeChildPrimitive(craft, PrimitiveType.Cylinder, "EngineTube" + side,
                new Vector3(side * 0.9f, -0.03f, -3.35f), new Vector3(0.42f, 1.3f, 0.42f),
                MakeMaterial("S4_ShuttleEngine", new Color(0.30f, 0.48f, 0.58f)),
                Quaternion.Euler(90f, 0f, 0f));
            MakeChildPrimitive(craft, PrimitiveType.Sphere, "EngineGlow" + side,
                new Vector3(side * 0.9f, -0.03f, -4.0f), Vector3.one * 0.48f, greenBeaconMat);
        }

        return craft;
    }

    private IEnumerator OpenSupplyLidRoutine()
    {
        if (supplyLidHinge == null) yield break;
        Quaternion closed = Quaternion.identity;
        Quaternion open = Quaternion.Euler(0f, 0f, -112f);
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            supplyLidHinge.localRotation = Quaternion.Slerp(closed, open, Mathf.Clamp01(t / 0.45f));
            yield return null;
        }
        supplyLidHinge.localRotation = open;
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
        // No floating label above the boarding door.
    }

    private void PlantTimedBomb(int siteIndex)
    {
        if (siteIndex < 0 || siteIndex >= bombSites.Count ||
            siteIndex >= bombSiteIndicators.Count || chargesInInventory <= 0 ||
            bombSiteIndicators[siteIndex] == null || bombSiteIndicators[siteIndex].activeSelf)
            return;

        Vector3 p = bombSites[siteIndex].position + Vector3.up * 0.62f;
        var bomb = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bomb.name = "S4_SquareTimedCharge_" + (siteIndex + 1);
        bomb.transform.SetParent(worldRoot, false);
        bomb.transform.position = p;
        bomb.transform.localScale = new Vector3(0.82f, 0.72f, 0.82f);
        var collider = bomb.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        var renderer = bomb.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = bombMat;
        var led = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        led.name = "S4_ChargeStatusLED_" + (siteIndex + 1);
        led.transform.SetParent(bomb.transform, false);
        led.transform.localPosition = new Vector3(0f, 0.56f, 0f);
        led.transform.localScale = Vector3.one * 0.22f;
        var ledCollider = led.GetComponent<Collider>();
        if (ledCollider != null) Destroy(ledCollider);
        var ledRenderer = led.GetComponent<Renderer>();
        if (ledRenderer != null) ledRenderer.sharedMaterial = greenBeaconMat;

        // Once the square charge is planted, its green status LED turns red.
        var plantedLed = bomb.transform.Find("S4_ChargeStatusLED_" + (siteIndex + 1));
        if (plantedLed != null)
        {
            var plantedLedRenderer = plantedLed.GetComponent<Renderer>();
            if (plantedLedRenderer != null) plantedLedRenderer.sharedMaterial = redBeaconMat;
        }
        bombPositions.Add(bombSites[siteIndex].position);
        plantedBombs.Add(bomb);
        chargesInInventory--;
        bombSiteIndicators[siteIndex].GetComponent<Renderer>().sharedMaterial = redBeaconMat;
        bombSiteIndicators[siteIndex].SetActive(true);
        message = "SQUARE CHARGE PLANTED • RED LED ACTIVE • " + plantedBombs.Count + "/6";
        StartupCheckpoint.Set("StageFourChargePlantedAtStation_" + (siteIndex + 1) + "_of_6");
    }

    private IEnumerator PlayEscapeFinale()
    {
        finaleStarted = true;
        message = "ESCAPE ROUTE • BOARDING SHUTTLE";

        PlayerController controller = player != null ? player.GetComponent<PlayerController>() : null;
        if (controller != null)
        {
            controller.SetMovementEnabled(false);
            controller.enabled = false;
            if (controller.Weapon != null) controller.Weapon.enabled = false;
            if (controller.Aim != null) controller.Aim.enabled = false;
        }

        if (sceneCamera == null) sceneCamera = Camera.main;
        CameraFollow25D cameraFollow = FindFirstObjectByType<CameraFollow25D>();
        if (cameraFollow != null) cameraFollow.enabled = false;

        // Do not cut straight to an outside shot: first show the character
        // automatically moving down the corridor and entering the little shuttle.
        if (player != null && escapeShuttle != null)
        {
            Vector3 walkStart = player.position;
            Vector3 boardingPoint = escapeShuttle.position + new Vector3(0f, 0.05f, -1.65f);
            Quaternion walkRotation = player.rotation;
            Vector3 facing = escapeShuttle.position - player.position;
            facing.y = 0f;
            Quaternion boardingRotation = facing.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(facing.normalized, Vector3.up)
                : walkRotation;

            float walkTime = 0f;
            const float walkDuration = 1.25f;
            while (walkTime < walkDuration)
            {
                walkTime += Time.deltaTime;
                float step = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(walkTime / walkDuration));
                player.position = Vector3.Lerp(walkStart, boardingPoint, step);
                player.rotation = Quaternion.Slerp(walkRotation, boardingRotation, step);

                if (sceneCamera != null)
                {
                    Vector3 cameraTarget = player.position + Vector3.up * 1.1f;
                    sceneCamera.transform.position = Vector3.Lerp(
                        sceneCamera.transform.position,
                        player.position + new Vector3(8f, 6f, -12f), 0.18f);
                    sceneCamera.transform.LookAt(cameraTarget);
                }
                yield return null;
            }

            player.position = boardingPoint;
            Renderer[] playerRenderers = player.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in playerRenderers)
                if (r != null) r.enabled = false;
        }

        message = "SHUTTLE AIRLOCK SEALED";
        yield return new WaitForSeconds(0.45f);

        // The interior is now behind us. Remove its visible geometry so the
        // following unbroken exterior view clearly shows the whole mothership/base.
        HideInteriorForExteriorShot();

        Vector3 shuttleStart = new Vector3(0f, 36f, 184f);
        Vector3 shuttleEnd = new Vector3(520f, 190f, 292f);
        Quaternion shuttleStartRotation = Quaternion.LookRotation(
            new Vector3(0.78f, 0.13f, 0.60f), Vector3.up);
        Quaternion shuttleEndRotation = Quaternion.LookRotation(
            new Vector3(0.94f, 0.20f, 0.28f), Vector3.up);

        if (escapeShuttle != null)
        {
            escapeShuttle.position = shuttleStart;
            escapeShuttle.rotation = shuttleStartRotation;
            escapeShuttle.localScale = Vector3.one * 0.82f;
        }

        // Start just outside the bow, then pull back to a high diagonal reveal.
        // The camera is no longer attached to the player or shuttle during detonations.
        Vector3 closeCamera = new Vector3(18f, 122f, 438f);
        Vector3 wideCamera = new Vector3(650f, 420f, 690f);
        Vector3 closeLook = new Vector3(0f, 27f, 15f);
        Vector3 wideLook = new Vector3(25f, 18f, 0f);
        if (sceneCamera != null)
        {
            sceneCamera.transform.position = closeCamera;
            sceneCamera.transform.LookAt(closeLook);
        }

        message = "EXTERIOR VIEW • ESCAPE SHUTTLE DETACHING";
        float revealTime = 0f;
        const float revealDuration = 1.35f;
        while (revealTime < revealDuration)
        {
            revealTime += Time.deltaTime;
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(revealTime / revealDuration));
            if (sceneCamera != null)
            {
                sceneCamera.transform.position = Vector3.Lerp(closeCamera, wideCamera, reveal);
                sceneCamera.transform.LookAt(Vector3.Lerp(closeLook, wideLook, reveal));
            }
            yield return null;
        }

        message = "ESCAPE SHUTTLE RELEASED • MOTHERSHIP IN VIEW";

        // Six exterior blast locations move from the far/stern section toward the
        // central reactor. The final charge is deliberately much larger than the rest.
        Vector3[] blasts =
        {
            new Vector3(-18f, 23f, -132f),
            new Vector3(-58f, 31f, -66f),
            new Vector3(51f, 39f, -18f),
            new Vector3(-42f, 43f, 28f),
            new Vector3(28f, 29f, 88f),
            new Vector3(0f, 35f, -4f)
        };

        float elapsed = 0f;
        const float shuttleFlightDuration = 6.4f;
        float nextChargeTime = 0.75f;
        GameObject finalBlast = null;

        // Shuttle movement and the explosions run together. A fixed wide camera
        // keeps the mothership visible while the craft visibly separates and escapes.
        for (int i = 0; i < blasts.Length; i++)
        {
            while (elapsed < nextChargeTime)
            {
                elapsed += Time.deltaTime;
                float flight = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(elapsed / shuttleFlightDuration));

                if (escapeShuttle != null)
                {
                    escapeShuttle.position = Vector3.Lerp(shuttleStart, shuttleEnd, flight);
                    escapeShuttle.rotation = Quaternion.Slerp(shuttleStartRotation, shuttleEndRotation, flight);
                }

                if (sceneCamera != null)
                {
                    Vector3 lookTarget = Vector3.Lerp(
                        new Vector3(25f, 18f, 0f),
                        new Vector3(100f, 30f, 32f), flight);
                    sceneCamera.transform.LookAt(lookTarget);
                }
                yield return null;
            }

            message = i == blasts.Length - 1
                ? "FINAL CHARGE • MOTHERSHIP CORE DETONATION"
                : "MOTHERSHIP DETONATION • " + (i + 1) + "/6";

            float radius = i == blasts.Length - 1 ? 78f : 20f + i * 1.8f;
            GameObject explosion = CreateBlast(
                "S4_MothershipDetonation_" + (i + 1), blasts[i], radius,
                i == blasts.Length - 1);
            if (i == blasts.Length - 1)
                finalBlast = explosion;

            if (i < plantedBombs.Count && plantedBombs[i] != null)
                Destroy(plantedBombs[i]);

            nextChargeTime += 0.97f;
        }

        while (elapsed < shuttleFlightDuration)
        {
            elapsed += Time.deltaTime;
            float flight = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / shuttleFlightDuration));
            if (escapeShuttle != null)
            {
                escapeShuttle.position = Vector3.Lerp(shuttleStart, shuttleEnd, flight);
                escapeShuttle.rotation = Quaternion.Slerp(shuttleStartRotation, shuttleEndRotation, flight);
            }
            if (sceneCamera != null)
                sceneCamera.transform.LookAt(Vector3.Lerp(
                    new Vector3(25f, 18f, 0f),
                    new Vector3(100f, 30f, 32f), flight));
            yield return null;
        }

        message = "MOTHERSHIP CORE DESTROYED";
        // The last detonation remains visible for the final beat while the hull,
        // docking districts and lights disappear behind the escaping craft.
        HideMothershipAfterFinalExplosion(finalBlast);
        yield return new WaitForSeconds(1.2f);

        PrototypeFlow flow = FindFirstObjectByType<PrototypeFlow>();
        if (flow != null)
            flow.CompleteStageFourEscapeToMainMenu();
        else
            message = "STAGE 4 COMPLETE";
    }

    private void HideInteriorForExteriorShot()
    {
        if (worldRoot == null) return;

        Renderer[] renderers = worldRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null || (escapeShuttle != null &&
                (r.transform == escapeShuttle || r.transform.IsChildOf(escapeShuttle))))
                continue;

            Transform node = r.transform;
            bool hide = false;
            while (node != null && node != worldRoot)
            {
                string n = node.name;
                if (n.StartsWith("S4_Interior") ||
                    n.StartsWith("S4_Hangar") ||
                    n.StartsWith("S4_EscapeCommand") ||
                    n.StartsWith("S4_EscapeCorridor") ||
                    n.StartsWith("S4_EscapeDoor") ||
                    n.StartsWith("S4_RedEscapeDoor") ||
                    n.StartsWith("S4_Charge") ||
                    n.StartsWith("S4_StoredSquareCharge") ||
                    n.StartsWith("S4_RedChargeStation") ||
                    n.StartsWith("S4_SquareTimedCharge") ||
                    n.StartsWith("S4_AlienTrooper_"))
                {
                    hide = true;
                    break;
                }
                node = node.parent;
            }

            if (hide) r.enabled = false;
        }
    }

    private void HideMothershipAfterFinalExplosion(GameObject finalBlast)
    {
        if (worldRoot == null) return;

        Renderer[] renderers = worldRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (escapeShuttle != null &&
                (r.transform == escapeShuttle || r.transform.IsChildOf(escapeShuttle)))
                continue;
            if (finalBlast != null &&
                (r.transform == finalBlast.transform || r.transform.IsChildOf(finalBlast.transform)))
                continue;
            r.enabled = false;
        }
    }

    private GameObject CreateBlast(string name, Vector3 position, float radius, bool persistAfterExpansion = false)
    {
        var blast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blast.name = name;
        blast.transform.SetParent(worldRoot, false);
        blast.transform.position = position;
        blast.transform.localScale = Vector3.one * 0.2f;
        var col = blast.GetComponent<Collider>();
        if (col != null) Destroy(col);
        blast.GetComponent<Renderer>().material = blastMat;
        StartCoroutine(ExpandBlast(blast, radius, persistAfterExpansion));
        return blast;
    }

    private IEnumerator ExpandBlast(GameObject blast, float radius, bool persistAfterExpansion)
    {
        float t = 0f;
        const float expandDuration = 0.75f;
        while (t < expandDuration && blast != null)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / expandDuration);
            blast.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, radius, f);
            yield return null;
        }
        if (persistAfterExpansion) yield break;
        if (blast != null) Destroy(blast, 0.75f);
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(message)) return;
        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.label);
            messageStyle.fontSize = Mathf.Max(18, Screen.height / 34);
            messageStyle.alignment = TextAnchor.MiddleCenter;
            messageStyle.fontStyle = FontStyle.Bold;
            messageStyle.normal.textColor = Color.white;
            messageStyle.wordWrap = true;
        }

        if (finaleStarted)
        {
            // Cinematic captions intentionally sit at the bottom instead of covering
            // the normal health/shield area or the upper-right radar.
            messageStyle.fontSize = Mathf.Clamp(Screen.height / 34, 18, 30);
            Rect caption = new Rect(Screen.width * 0.08f, Screen.height * 0.79f,
                Screen.width * 0.84f, Mathf.Max(48f, Screen.height * 0.11f));
            Color previous = GUI.color;
            GUI.color = new Color(0.03f, 0.055f, 0.08f, 0.78f);
            GUI.Box(caption, GUIContent.none);
            GUI.color = previous;
            GUI.Label(caption, message, messageStyle);
            return;
        }

        bool landscape = Screen.width >= Screen.height;
        float titleX = landscape ? Mathf.Min(350f, Screen.width * 0.22f) : 12f;
        float rightReserve = landscape ? Mathf.Min(320f, Screen.width * 0.22f) : 12f;
        float statusWidth = Mathf.Max(100f, Screen.width - titleX - rightReserve);
        float titleY = landscape ? 8f : 150f;
        float statusFont = Mathf.Clamp(Screen.height / 36f, 18f, 23f);
        messageStyle.fontSize = Mathf.RoundToInt(statusFont);
        GUI.Label(new Rect(titleX, titleY, statusWidth, 34f), message, messageStyle);

        GUIStyle chargeStyle = new GUIStyle(messageStyle);
        chargeStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 39f), 17, 21);
        chargeStyle.normal.textColor = new Color(1f, 0.42f, 0.20f);
        float counterY = landscape ? 43f : 190f;
        GUI.Label(new Rect(titleX, counterY, statusWidth, 30f),
            "BOMBS: " + plantedBombs.Count + "/6 PLANTED   •   " + chargesInInventory + "/6 CARRIED", chargeStyle);

        int site = FindNearbyBombSite();
        bool nearCrate = !chargesPickedUp && supplyCrate != null && supplyCrate.activeSelf &&
            player != null && Vector3.Distance(player.position, supplyCrate.transform.position) <= 4.5f;
        bool nearEmptySite = site >= 0 && site < bombSiteIndicators.Count &&
            bombSiteIndicators[site] != null && !bombSiteIndicators[site].activeSelf;
        bool nearDoor = !doorOpened && redDoorPanel != null && player != null &&
            Vector3.Distance(player.position, redDoorPanel.transform.position) <= 8f;
        float buttonW = Mathf.Min(Screen.width - 40, 420);
        Rect buttonRect = new Rect((Screen.width - buttonW) * 0.5f, Screen.height - 142, buttonW, 58);
        if (nearCrate && !supplyBoxOpened && GUI.Button(buttonRect, "OPEN SUPPLY BOX"))
            requestOpenSupplyBox = true;
        else if (nearCrate && supplyBoxOpened && GUI.Button(buttonRect, "TAKE 6 CHARGES"))
            requestPickup = true;
        else if (nearEmptySite && chargesPickedUp && chargesInInventory > 0 &&
                 GUI.Button(buttonRect, "PLANT SQUARE CHARGE"))
            requestPlant = true;
        else if (nearDoor && GUI.Button(buttonRect, plantedBombs.Count >= 6 ? "OPEN RED EXIT DOOR" : "EXIT LOCKED • PLANT 6 CHARGES"))
            requestOpenDoor = true;
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
        MakeChildPrimitive(parent, PrimitiveType.Cube, name, pos, scale, material);
    }

    private void MakeChildPrimitive(Transform parent, PrimitiveType primitiveType, string name,
        Vector3 pos, Vector3 scale, Material material, Quaternion localRotation = default(Quaternion))
    {
        var go = GameObject.CreatePrimitive(primitiveType);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (localRotation != default(Quaternion)) go.transform.localRotation = localRotation;
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
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


 