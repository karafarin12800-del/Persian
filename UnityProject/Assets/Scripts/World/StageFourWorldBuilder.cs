using System.Collections;
using UnityEngine;
using PersiaWar.Unity2D5D;

/// <summary>
/// Isolated runtime-built Stage 4. The exterior is a city-scale alien orbital
/// megabase; gameplay begins in the interior hangar of its central mothership.
/// This builder owns its objects and restores the prior world when removed.
/// </summary>
public sealed class StageFourWorldBuilder : MonoBehaviour
{
    private const string RootName = "StageFourMothershipMegabase";
    private const float PlanetSurfaceY = -100f;
    private Transform root;
    private Transform player;
    private Camera gameplayCamera;
    private PlayerController controller;
    private Transform greenBeam;
    private Material dark, metal, brightMetal, glass, green, cyan, floor, planetSurface, planetLand;
    private bool built, introRunning;
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private float savedFarClip;
    private Color savedBackground;
    private CameraClearFlags savedClearFlags;
    private Transform legacyCity;
    private Color savedAmbient;
    private bool ambientCaptured;

    public bool IsCinematic => introRunning;
    public string IntroMessage { get; private set; } = "GREEN UPLINK DETECTED";
    public string BuildStatus { get; private set; } = "Stage 4: waiting to build orbital megabase...";
    public bool IsBuilt => built;
    public string BuildFailureCode { get; private set; } = string.Empty;

    public static void ClearExistingWorld()
    {
        var builders = FindObjectsByType<StageFourWorldBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in builders) if (b != null) b.ClearWorld();
    }

    /// <summary>
    /// Constructs Stage 4 over multiple frames. Large sets of runtime primitives must
    /// not all be created in one Android frame, otherwise the battlefield loading UI
    /// remains frozen for an unbounded-looking time.
    /// </summary>
    public IEnumerator BuildWorldRoutine(System.Action<bool> completed)
    {
        if (!PrepareWorldBuild())
        {
            completed?.Invoke(false);
            yield break;
        }

        System.Exception phaseFailure = null;
        BuildStatus = "Stage 4: assembling orbital districts...";
        yield return RunBuildStep(BuildExteriorMegabaseRoutine(), ex => phaseFailure = ex);
        if (phaseFailure != null)
        {
            FailWorldBuild(phaseFailure);
            completed?.Invoke(false);
            yield break;
        }

        BuildStatus = "Stage 4: positioning docked spacecraft...";
        yield return RunBuildStep(BuildDockedShipsRoutine(), ex => phaseFailure = ex);
        if (phaseFailure != null)
        {
            FailWorldBuild(phaseFailure);
            completed?.Invoke(false);
            yield break;
        }

        BuildStatus = "Stage 4: constructing mothership hangar...";
        yield return RunBuildStep(BuildMothershipAndInteriorRoutine(), ex => phaseFailure = ex);
        if (phaseFailure != null)
        {
            FailWorldBuild(phaseFailure);
            completed?.Invoke(false);
            yield break;
        }

        try
        {
            BuildGreenUplink();
            BuildLighting();
            built = true;
            BuildStatus = "Stage 4: megabase ready; preparing arrival...";
            StartupCheckpoint.Set("StageFourMegabaseBuilt_CityScaleExteriorAndMothershipInterior");
        }
        catch (System.Exception ex)
        {
            FailWorldBuild(ex);
            completed?.Invoke(false);
            yield break;
        }

        completed?.Invoke(true);
    }

    private bool PrepareWorldBuild()
    {
        BuildFailureCode = string.Empty;
        try
        {
            ClearWorld();
            savedAmbient = RenderSettings.ambientLight;
            ambientCaptured = true;
            GameObject oldRoot = GameObject.Find(RootName);
            if (oldRoot != null) Destroy(oldRoot);
            GameObject city = GameObject.Find("BattleRoyaleCity");
            if (city != null) { legacyCity = city.transform; legacyCity.gameObject.SetActive(false); }

            var go = new GameObject(RootName);
            go.transform.SetParent(transform, false);
            root = go.transform;
            dark = MakeMaterial("S4_ObsidianAlloy", new Color(0.035f, 0.055f, 0.085f));
            metal = MakeMaterial("S4_BlueTitanium", new Color(0.16f, 0.24f, 0.34f));
            brightMetal = MakeMaterial("S4_SilverHull", new Color(0.52f, 0.65f, 0.76f));
            glass = MakeMaterial("S4_DarkGlass", new Color(0.025f, 0.12f, 0.18f), new Color(0.01f, 0.28f, 0.34f));
            green = MakeMaterial("S4_GreenUplink", new Color(0.12f, 1f, 0.35f), new Color(0.06f, 1f, 0.28f));
            cyan = MakeMaterial("S4_CyanDockLights", new Color(0.12f, 0.8f, 1f), new Color(0.04f, 0.5f, 1f));
            floor = MakeMaterial("S4_HangarFloor", new Color(0.085f, 0.11f, 0.15f));
            planetSurface = MakeMaterial("S4_PlanetOcean", new Color(0.025f, 0.13f, 0.25f), new Color(0.005f, 0.025f, 0.06f));
            planetLand = MakeMaterial("S4_PlanetContinents", new Color(0.12f, 0.31f, 0.17f), new Color(0.015f, 0.045f, 0.012f));
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("PERSIA_STAGE4: build initialization failed: " + ex);
            FailWorldBuild(ex);
            return false;
        }
    }

    // Iterator exception handling is isolated from yield statements so Unity can
    // report a failed phase instead of leaving the generic loading panel indefinitely.
    private IEnumerator RunBuildStep(IEnumerator step, System.Action<System.Exception> onError)
    {
        while (true)
        {
            bool moved = false;
            object current = null;
            System.Exception failure = null;
            try
            {
                moved = step.MoveNext();
                if (moved) current = step.Current;
            }
            catch (System.Exception ex)
            {
                failure = ex;
            }

            if (failure != null)
            {
                onError?.Invoke(failure);
                yield break;
            }
            if (!moved) yield break;
            yield return current;
        }
    }

    private void FailWorldBuild(System.Exception ex)
    {
        BuildFailureCode = ex != null ? ex.GetType().Name : "UnknownError";
        string details = ex != null ? ex.Message : "No exception details were available.";
        Debug.LogError("PERSIA_STAGE4: world build failed: " + ex);
        StartupCheckpoint.Set("StageFourWorldBuildFailed_" + BuildFailureCode);
        BuildStatus = "Stage 4 construction failed (" + BuildFailureCode + "): " + details;
        ClearWorld();
    }

    /// <summary>
    /// Explicit recovery path for a build coroutine that stops making progress.
    /// The menu flow owns the timeout so a failed Stage 4 load can never trap the
    /// player on the loading screen indefinitely.
    /// </summary>
    public void AbortBuild(string reason)
    {
        var timeout = new System.TimeoutException(string.IsNullOrEmpty(reason)
            ? "Stage 4 world construction timed out."
            : reason);
        FailWorldBuild(timeout);
    }

    public void ConfigurePlayer(PlayerController p, Camera cam)
    {
        controller = p;
        player = p != null ? p.transform : null;
        gameplayCamera = cam;
        if (controller != null)
        {
            controller.transform.position = new Vector3(0f, 0.15f, -9f);
            controller.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            controller.SetShield(100);
            if (controller.Health != null) controller.Health.SetMaxHealth(150);
            controller.SetMovementEnabled(false);
            if (controller.Weapon != null) controller.Weapon.enabled = false;
            if (controller.Aim != null) controller.Aim.enabled = false;
        }
        if (gameplayCamera != null)
        {
            savedCameraPosition = gameplayCamera.transform.position;
            savedCameraRotation = gameplayCamera.transform.rotation;
            savedFarClip = gameplayCamera.farClipPlane;
            savedBackground = gameplayCamera.backgroundColor;
            savedClearFlags = gameplayCamera.clearFlags;
            gameplayCamera.clearFlags = CameraClearFlags.SolidColor;
            gameplayCamera.backgroundColor = new Color(0.005f, 0.012f, 0.03f);
            gameplayCamera.farClipPlane = Mathf.Max(savedFarClip, 2600f);
        }
        if (built && controller != null && gameplayCamera != null && !introRunning)
            StartCoroutine(PlayArrivalCinematic());
    }

    private void Update()
    {
        if (!introRunning) return;
        // Match-service startup can re-enable the player while the cutscene is running;
        // keep controls gated until the green-beam arrival finishes.
        if (controller != null)
        {
            controller.SetMovementEnabled(false);
            if (controller.Weapon != null) controller.Weapon.enabled = false;
            if (controller.Aim != null) controller.Aim.enabled = false;
        }
    }

    private IEnumerator PlayArrivalCinematic()
    {
        introRunning = true;
        IntroMessage = "STAGE 4 • ORBITAL MEGABASE APPROACH";
        Transform pilot = player;
        Renderer[] renderers = pilot.GetComponentsInChildren<Renderer>(true);
        bool[] wasEnabled = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            wasEnabled[i] = renderers[i] != null && renderers[i].enabled;
            if (renderers[i] != null) renderers[i].enabled = false;
        }

        Vector3 stationCenter = new Vector3(0f, 0f, 120f);
        Vector3 farPos = stationCenter + new Vector3(0f, 840f, -1120f);
        Vector3 nearPos = new Vector3(0f, 90f, -145f);
        float t = 0f;
        while (t < 5.2f)
        {
            t += Time.unscaledDeltaTime;
            if (greenBeam != null)
            {
                // Grow the uplink from the planet's surface upward into the mothership.
                // A Unity cylinder's full height is twice its Y scale.
                float beamHalfHeight = Mathf.Lerp(0.1f, 140f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.8f)));
                greenBeam.position = new Vector3(0f, PlanetSurfaceY + beamHalfHeight, 120f);
                greenBeam.localScale = new Vector3(15f, beamHalfHeight, 15f);
            }
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 5.2f));
            gameplayCamera.transform.position = Vector3.Lerp(farPos, nearPos, a);
            Vector3 target = Vector3.Lerp(stationCenter, new Vector3(0f, 4f, 22f), a);
            gameplayCamera.transform.rotation = Quaternion.LookRotation(target - gameplayCamera.transform.position, Vector3.up);
            IntroMessage = a < 0.35f ? "GREEN UPLINK • ORBITAL BASE IN VIEW" :
                a < 0.72f ? "APPROACHING THE MOTHERSHIP DOCK" : "ENTERING THE MOTHERSHIP HANGAR";
            yield return null;
        }

        // Short green scan flash at the hangar entrance, then cut to the player
        // standing inside the mothership so the actual match begins indoors.
        IntroMessage = "TRANSFER COMPLETE • MOTHERSHIP INTERIOR";
        yield return new WaitForSecondsRealtime(0.65f);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = wasEnabled[i];

        Quaternion yaw = Quaternion.Euler(0f, pilot.eulerAngles.y, 0f);
        Vector3 cameraTarget = pilot.position + Vector3.up * 2.1f;
        gameplayCamera.transform.position = cameraTarget - yaw * Vector3.forward * 7.2f + Vector3.up * 3.2f;
        gameplayCamera.transform.rotation = Quaternion.LookRotation(cameraTarget - gameplayCamera.transform.position, Vector3.up);
        gameplayCamera.farClipPlane = Mathf.Max(savedFarClip, 900f);
        gameplayCamera.clearFlags = CameraClearFlags.SolidColor;
        gameplayCamera.backgroundColor = new Color(0.015f, 0.025f, 0.04f);
        if (controller != null)
        {
            controller.SetMovementEnabled(true);
            if (controller.Weapon != null) controller.Weapon.enabled = true;
            if (controller.Aim != null) controller.Aim.enabled = true;
        }
        yield return new WaitForSecondsRealtime(0.55f);
        introRunning = false;
        IntroMessage = "STAGE 4 • SECURE THE MOTHERSHIP";
        StartupCheckpoint.Set("StageFourArrivalCinematicComplete_PlayerInsideMothership");
    }

    private IEnumerator BuildExteriorMegabaseRoutine()
    {
        // A broad orbital platform suggests a city-scale base (not a small station).
        Box("S4_OrbitalFoundation", new Vector3(0f, -22f, 120f), new Vector3(980f, 36f, 980f), dark);
        Box("S4_CentralSpine", new Vector3(0f, 4f, 120f), new Vector3(260f, 28f, 520f), metal);
        Box("S4_MainTransitRing", new Vector3(0f, 5f, 120f), new Vector3(780f, 10f, 780f), dark);
        // District blocks, towers, illuminated lanes, and multiple launch/docking arms.
        for (int x = -4; x <= 4; x++)
        for (int z = -4; z <= 4; z++)
        {
            if (Mathf.Abs(x) < 2 && Mathf.Abs(z) < 2) continue;
            float px = x * 92f, pz = 120f + z * 92f;
            float h = 26f + ((x * 7 + z * 11 + 37) & 3) * 15f;
            Box("S4_CityDistrict_" + x + "_" + z, new Vector3(px, h * 0.5f - 2f, pz),
                new Vector3(48f + Mathf.Abs(z % 3) * 9f, h, 52f + Mathf.Abs(x % 3) * 8f), (x + z) % 2 == 0 ? metal : brightMetal);
            for (int w = -1; w <= 1; w++)
                Box("S4_DistrictLight_" + x + "_" + z + "_" + w,
                    new Vector3(px + w * 12f, h + 0.15f, pz - 25f),
                    new Vector3(3.2f, 0.7f, 0.5f), cyan);
            if (z == 4) yield return null; // keep Android rendering responsive during district generation
        }
        for (int arm = 0; arm < 8; arm++)
        {
            float angle = arm * 45f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 pos = new Vector3(dir.x * 350f, 5f, 120f + dir.z * 350f);
            Box("S4_DockingArm_" + arm, pos, new Vector3(74f, 12f, 210f), metal, Quaternion.LookRotation(dir));
            Box("S4_DockPad_" + arm, pos + dir * 130f + Vector3.up * 9f,
                new Vector3(112f, 5f, 88f), brightMetal, Quaternion.LookRotation(dir));
            for (int light = -3; light <= 3; light++)
                Box("S4_DockRunwayLight_" + arm + "_" + light,
                    pos + dir * (60f + (light + 3) * 20f) + Vector3.up * 15f + Vector3.right * light * 7f,
                    new Vector3(3f, 1f, 3f), green);
            yield return null;
        }
        // Ring pylons are tall enough to read in the far establishing shot.
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24f;
            Vector3 p = new Vector3(Mathf.Cos(a) * 430f, 48f, 120f + Mathf.Sin(a) * 430f);
            Box("S4_RingPylon_" + i, p, new Vector3(18f, 92f + (i % 3) * 22f, 18f), brightMetal);
            Box("S4_RingPylonBeacon_" + i, p + Vector3.up * 55f, new Vector3(5f, 3f, 5f), cyan);
            if (i % 3 == 2) yield return null;
        }
    }

    private IEnumerator BuildDockedShipsRoutine()
    {
        // Sixteen visibly distinct docked craft attached to the perimeter docks.
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI * 2f / 16f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 p = new Vector3(dir.x * 360f, 19f + (i % 3) * 8f, 120f + dir.z * 360f);
            BuildShip("S4_DockedShip_" + i, p, Quaternion.LookRotation(dir), 0.78f + (i % 4) * 0.16f);
            yield return null;
        }
    }

    private IEnumerator BuildMothershipAndInteriorRoutine()
    {
        // Central mothership hull and bridge crown, with an open, playable interior.
        Box("S4_MothershipKeel", new Vector3(0f, 12f, 120f), new Vector3(210f, 26f, 320f), dark);
        Box("S4_MothershipUpperHull", new Vector3(0f, 29f, 120f), new Vector3(178f, 16f, 260f), brightMetal);
        Box("S4_MothershipBridge", new Vector3(0f, 45f, 172f), new Vector3(92f, 18f, 72f), metal);
        Box("S4_MothershipBridgeGlass", new Vector3(0f, 48f, 176f), new Vector3(70f, 7f, 45f), glass);
        // The hangar floor is centered on the gameplay origin; walls frame rather than block it.
        Box("S4_InteriorHangarFloor", new Vector3(0f, -0.35f, 0f), new Vector3(94f, 0.7f, 94f), floor);
        Box("S4_HangarCeiling", new Vector3(0f, 15f, 0f), new Vector3(94f, 2f, 94f), dark);
        Box("S4_HangarWallLeft", new Vector3(-47f, 7f, 0f), new Vector3(2f, 14f, 94f), metal);
        Box("S4_HangarWallRight", new Vector3(47f, 7f, 0f), new Vector3(2f, 14f, 94f), metal);
        Box("S4_HangarRearWall", new Vector3(0f, 7f, -47f), new Vector3(94f, 14f, 2f), metal);
        // Wide forward blast door, with segmented pillars and an open central view toward the base.
        Box("S4_HangarDoorLeft", new Vector3(-36f, 7f, 47f), new Vector3(22f, 14f, 3f), brightMetal);
        Box("S4_HangarDoorRight", new Vector3(36f, 7f, 47f), new Vector3(22f, 14f, 3f), brightMetal);
        Box("S4_HangarDoorHeader", new Vector3(0f, 14f, 47f), new Vector3(50f, 2f, 3f), brightMetal);
        for (int i = -4; i <= 4; i++)
        {
            Box("S4_HangarFloorStripe_" + i, new Vector3(i * 8f, 0.04f, 3f),
                new Vector3(0.55f, 0.08f, 70f), i == 0 ? green : cyan);
        }
        yield return null;
        for (int i = 0; i < 6; i++)
        {
            float z = -32f + i * 13f;
            Box("S4_InteriorPillarL_" + i, new Vector3(-39f, 6.5f, z), new Vector3(3f, 13f, 3f), brightMetal);
            Box("S4_InteriorPillarR_" + i, new Vector3(39f, 6.5f, z), new Vector3(3f, 13f, 3f), brightMetal);
            Box("S4_InteriorPanelL_" + i, new Vector3(-37f, 7f, z), new Vector3(0.5f, 4f, 5f), green);
            Box("S4_InteriorPanelR_" + i, new Vector3(37f, 7f, z), new Vector3(0.5f, 4f, 5f), cyan);
            yield return null;
        }
        // Side passages give the hangar a sense of depth and scale.
        Box("S4_InteriorSideHallL", new Vector3(-68f, 0f, 0f), new Vector3(42f, 0.5f, 24f), floor);
        Box("S4_InteriorSideHallR", new Vector3(68f, 0f, 0f), new Vector3(42f, 0.5f, 24f), floor);
        Box("S4_InteriorSideWallL", new Vector3(-68f, 6f, 13f), new Vector3(42f, 12f, 2f), metal);
        Box("S4_InteriorSideWallR", new Vector3(68f, 6f, 13f), new Vector3(42f, 12f, 2f), metal);
        BuildShip("S4_InteriorServiceCraft", new Vector3(0f, 1.2f, 25f), Quaternion.identity, 0.36f);
        Box("S4_InteriorConsole", new Vector3(0f, 1.3f, -34f), new Vector3(14f, 2.6f, 4f), brightMetal);
        Box("S4_InteriorConsoleGlow", new Vector3(0f, 2.7f, -34f), new Vector3(10f, 0.18f, 2.2f), green);
    }

    private void BuildGreenUplink()
    {
        BuildTransferPlanet();

        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "S4_GreenUplinkPlanetToMothership";
        beam.transform.SetParent(root, false);
        beam.transform.position = new Vector3(0f, PlanetSurfaceY + 0.1f, 120f);
        beam.transform.localScale = new Vector3(15f, 0.1f, 15f);
        var beamRenderer = beam.GetComponent<Renderer>();
        if (beamRenderer != null) beamRenderer.sharedMaterial = green;
        Collider c = beam.GetComponent<Collider>(); if (c != null) Destroy(c);
        greenBeam = beam.transform;
        Box("S4_UplinkEmitter", new Vector3(0f, 18f, 120f), new Vector3(46f, 7f, 46f), green);
        Box("S4_UplinkBaseRing", new Vector3(0f, 7f, 120f), new Vector3(92f, 4f, 92f), metal);
    }

    private void BuildTransferPlanet()
    {
        // A curved blue/green planetary horizon sits below the city-scale orbital base.
        // It is visual-only: no planet collider can interfere with the playable hangar.
        var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        planet.name = "S4_TransferPlanet";
        planet.transform.SetParent(root, false);
        planet.transform.position = new Vector3(0f, -1000f, 120f);
        planet.transform.localScale = Vector3.one * 1800f;
        var oceanRenderer = planet.GetComponent<Renderer>();
        if (oceanRenderer != null) oceanRenderer.sharedMaterial = planetSurface;
        Collider planetCollider = planet.GetComponent<Collider>();
        if (planetCollider != null) Destroy(planetCollider);

        // Stylized continent shapes on the camera-facing hemisphere help the sphere
        // read as a planet during the establishing shot without costly terrain meshes.
        AddPlanetLand("S4_PlanetContinent_Northwest", new Vector3(-250f, -150f, -80f), new Vector3(260f, 18f, 170f), -18f);
        AddPlanetLand("S4_PlanetContinent_Northeast", new Vector3(180f, -155f, -160f), new Vector3(220f, 16f, 140f), 24f);
        AddPlanetLand("S4_PlanetContinent_West", new Vector3(-420f, -211f, -30f), new Vector3(190f, 16f, 130f), 38f);
        AddPlanetLand("S4_PlanetContinent_East", new Vector3(355f, -171f, -10f), new Vector3(240f, 18f, 155f), -31f);
    }

    private void AddPlanetLand(string name, Vector3 position, Vector3 scale, float yaw)
    {
        var land = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        land.name = name;
        land.transform.SetParent(root, false);
        land.transform.position = position;
        land.transform.localScale = scale;
        land.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var landRenderer = land.GetComponent<Renderer>();
        if (landRenderer != null) landRenderer.sharedMaterial = planetLand;
        Collider landCollider = land.GetComponent<Collider>();
        if (landCollider != null) Destroy(landCollider);
    }

    private void BuildLighting()
    {
        var lightGo = new GameObject("S4_MegabaseKeyLight");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.rotation = Quaternion.Euler(48f, -24f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.58f, 0.76f, 1f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.None;
        RenderSettings.ambientLight = new Color(0.22f, 0.27f, 0.35f);
    }

    private void BuildShip(string name, Vector3 position, Quaternion rotation, float scale)
    {
        var ship = new GameObject(name);
        ship.transform.SetParent(root, false);
        ship.transform.position = position;
        ship.transform.rotation = rotation;
        ship.transform.localScale = Vector3.one * scale;
        Part(ship.transform, "Hull", Vector3.zero, new Vector3(8f, 2.3f, 18f), brightMetal);
        Part(ship.transform, "Nose", new Vector3(0f, 0.2f, 11f), new Vector3(4.5f, 1.5f, 8f), metal);
        Part(ship.transform, "Cockpit", new Vector3(0f, 1.5f, 3f), new Vector3(3.5f, 1.7f, 5f), glass);
        Part(ship.transform, "LeftWing", new Vector3(-7f, -0.2f, -1f), new Vector3(8f, 0.7f, 8f), metal);
        Part(ship.transform, "RightWing", new Vector3(7f, -0.2f, -1f), new Vector3(8f, 0.7f, 8f), metal);
        Part(ship.transform, "EngineGlowL", new Vector3(-2f, 0f, -10f), new Vector3(1.3f, 1.3f, 1.3f), cyan);
        Part(ship.transform, "EngineGlowR", new Vector3(2f, 0f, -10f), new Vector3(1.3f, 1.3f, 1.3f), green);
    }

    private void Part(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = mat;
        var c = go.GetComponent<Collider>(); if (c != null) Destroy(c);
    }

    private void Box(string name, Vector3 pos, Vector3 size, Material mat, Quaternion rotation = default(Quaternion))
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        go.transform.localScale = size;
        if (rotation != default(Quaternion)) go.transform.rotation = rotation;
        var r = go.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = mat;
        // Architecture is solid only at the playable interior, keeping the huge distant base lightweight.
        bool interior = name.StartsWith("S4_Interior") || name.StartsWith("S4_Hangar") || name.StartsWith("S4_Mothership");
        if (!interior) { var c = go.GetComponent<Collider>(); if (c != null) Destroy(c); }
    }

    private Material MakeMaterial(string name, Color color, Color? emission = null)
    {
        // Use the project's Resources-backed shader first. Shader.Find fallbacks can
        // be stripped from Android players when no serialized material references them.
        Shader shader = RuntimeMaterialFactory.GetShader();
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
            throw new System.InvalidOperationException(
                "No compatible runtime shader found for Stage 4 materials.");

        var m = new Material(shader) { name = name, color = color };
        if (m.HasProperty("_Color"))
            m.SetColor("_Color", color);
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", color);
        if (m.HasProperty("_MainTex") && m.mainTexture == null)
            m.mainTexture = Texture2D.whiteTexture;

        // The Android flat shader has no emission properties; its explicit colors
        // still keep green/cyan markers visible without emitting unsupported keywords.
        if (emission.HasValue && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        return m;
    }

    private void ClearWorld()
    {
        if (root != null) { if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject); root = null; }
        var named = GameObject.Find(RootName);
        if (named != null) { if (Application.isPlaying) Destroy(named); else DestroyImmediate(named); }
        if (legacyCity == null)
        {
            var city = GameObject.Find("BattleRoyaleCity");
            if (city != null) legacyCity = city.transform;
        }
        if (legacyCity != null && legacyCity.gameObject != null) legacyCity.gameObject.SetActive(true);
        if (ambientCaptured) { RenderSettings.ambientLight = savedAmbient; ambientCaptured = false; }
        if (gameplayCamera != null)
        {
            gameplayCamera.farClipPlane = savedFarClip > 0f ? savedFarClip : gameplayCamera.farClipPlane;
            gameplayCamera.backgroundColor = savedBackground;
            gameplayCamera.clearFlags = savedClearFlags;
            gameplayCamera.transform.position = savedCameraPosition;
            gameplayCamera.transform.rotation = savedCameraRotation;
        }
        if (controller != null && !controller.IsDefeated)
        {
            controller.SetMovementEnabled(true);
            if (controller.Weapon != null) controller.Weapon.enabled = true;
            if (controller.Aim != null) controller.Aim.enabled = true;
        }
        if (dark != null) Destroy(dark); if (metal != null) Destroy(metal);
        if (brightMetal != null) Destroy(brightMetal); if (glass != null) Destroy(glass);
        if (green != null) Destroy(green); if (cyan != null) Destroy(cyan); if (floor != null) Destroy(floor);
        if (planetSurface != null) Destroy(planetSurface); if (planetLand != null) Destroy(planetLand);
        built = false; introRunning = false; controller = null; player = null; gameplayCamera = null;
        greenBeam = null;
    }
}
