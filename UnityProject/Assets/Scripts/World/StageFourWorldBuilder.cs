using System.Collections;
using System.Collections.Generic;
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
    private Transform greenBeamCore;
    private Transform impactPulse;
    private Transform interiorTransferPulse;
    private readonly List<EnemyChase> interiorHostiles = new List<EnemyChase>();
    private readonly List<Transform> escapeShuttles = new List<Transform>();
    private Material dark, metal, brightMetal, glass, green, cyan, floor, planetSurface, planetLand;
    private Material alienArmor, alienDark, alienEye;
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
    public int DockedEscapeShuttleCount => escapeShuttles.Count;
    public Transform GetDockedEscapeShuttle(int index) =>
        index >= 0 && index < escapeShuttles.Count ? escapeShuttles[index] : null;
    public int TotalHostiles => interiorHostiles.Count;
    public int RemainingHostiles
    {
        get
        {
            int remaining = 0;
            for (int i = 0; i < interiorHostiles.Count; i++)
            {
                EnemyChase hostile = interiorHostiles[i];
                TargetHealth health = hostile != null ? hostile.GetComponent<TargetHealth>() : null;
                if (hostile != null && health != null && health.CurrentHealth > 0)
                    remaining++;
            }
            return remaining;
        }
    }
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
            alienArmor = MakeMaterial("S4_AlienArmor", new Color(0.14f, 0.27f, 0.32f));
            alienDark = MakeMaterial("S4_AlienChitin", new Color(0.035f, 0.075f, 0.09f));
            alienEye = MakeMaterial("S4_AlienBioluminescence", new Color(0.18f, 1f, 0.32f));
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
        if (built && controller != null && gameplayCamera != null)
        {
            SpawnInteriorAliens();
            if (!introRunning)
                StartCoroutine(PlayArrivalCinematic());
        }
    }

    private void Update()
    {
        if (introRunning)
        {
            if (controller != null)
            {
                controller.SetMovementEnabled(false);
                if (controller.Weapon != null) controller.Weapon.enabled = false;
                if (controller.Aim != null) controller.Aim.enabled = false;
            }
            return;
        }

        // Defend against a late match-start callback re-gating Stage 4 movement after
        // the cutscene. Do not interfere with defeat handling or a new cinematic.
        if (built && controller != null && !controller.IsDefeated &&
            (!controller.enabled || !controller.IsMovementEnabled) && Time.time > 1f)
        {
            controller.enabled = true;
            controller.SetMovementEnabled(true);
            StartupCheckpoint.Set("StageFourMovementWatchdogRearmed");
        }
    }

    private IEnumerator PlayArrivalCinematic()
    {
        introRunning = true;
        IntroMessage = "STAGE 4 • ORBITAL UPLINK DETECTED";
        Transform pilot = player;
        Renderer[] renderers = pilot != null ? pilot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        bool[] wasEnabled = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            wasEnabled[i] = renderers[i] != null && renderers[i].enabled;
            if (renderers[i] != null) renderers[i].enabled = false;
        }

        Vector3[] cameraPath =
        {
            new Vector3(360f, 600f, 130f), // high starboard-side establishing shot
            new Vector3(275f, 240f, 60f),  // dive along the right side of the mothership
            new Vector3(190f, 75f, -60f),  // arc around the starboard aft quarter
            new Vector3(65f, 25f, -67f),   // line up with the rear hangar entrance
            new Vector3(0f, 12f, -42f),   // align with the centered rear doorway
            new Vector3(0f, 5.7f, -18.5f)  // settle behind the player
        };
        Vector3[] lookPath =
        {
            new Vector3(0f, 35f, 42f),
            new Vector3(0f, 32f, 16f),
            new Vector3(0f, 23f, -10f),
            new Vector3(0f, 11f, -12f),
            new Vector3(0f, 4f, -8f),
            new Vector3(0f, 2.3f, -6.5f)
        };

        const float duration = 6.0f;
        const float beamTopY = 320f;
        const float beamImpactY = 44f;
        float maxBeamHalfHeight = (beamTopY - beamImpactY) * 0.5f;
        float t = 0f;
        bool interiorPulseShown = false;
        Renderer beamRenderer = greenBeam != null ? greenBeam.GetComponent<Renderer>() : null;
        Renderer beamCoreRenderer = greenBeamCore != null ? greenBeamCore.GetComponent<Renderer>() : null;
        Renderer impactRenderer = impactPulse != null ? impactPulse.GetComponent<Renderer>() : null;
        Renderer transferRenderer = interiorTransferPulse != null
            ? interiorTransferPulse.GetComponent<Renderer>() : null;
        if (beamRenderer != null) beamRenderer.enabled = true;
        if (beamCoreRenderer != null) beamCoreRenderer.enabled = true;
        if (impactRenderer != null) impactRenderer.enabled = false;
        if (transferRenderer != null) transferRenderer.enabled = false;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            Vector3 cameraPosition = EvaluatePath(cameraPath, normalized);
            Vector3 cameraTarget = EvaluatePath(lookPath, normalized);
            gameplayCamera.transform.position = cameraPosition;
            Vector3 direction = cameraTarget - cameraPosition;
            if (direction.sqrMagnitude > 0.001f)
                gameplayCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.72f));
            float collapse = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.92f) / 0.46f));
            float beamHalfHeight = Mathf.Lerp(0.12f, maxBeamHalfHeight, grow) * (1f - collapse);
            if (greenBeam != null)
            {
                greenBeam.position = new Vector3(0f, beamTopY - beamHalfHeight, 48f);
                greenBeam.localScale = new Vector3(13f, Mathf.Max(0.12f, beamHalfHeight), 13f);
            }
            if (greenBeamCore != null)
            {
                greenBeamCore.position = new Vector3(0f, beamTopY - beamHalfHeight, 48f);
                greenBeamCore.localScale = new Vector3(4.2f, Mathf.Max(0.12f, beamHalfHeight), 4.2f);
            }

            // Hide both beam renderers completely once the energy retracts into space.
            bool externalBeamVisible = t < 1.42f;
            if (beamRenderer != null) beamRenderer.enabled = externalBeamVisible;
            if (beamCoreRenderer != null) beamCoreRenderer.enabled = externalBeamVisible;

            if (impactPulse != null)
            {
                float impactIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.45f) / 0.20f));
                float impactOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1.18f) / 0.42f));
                float pulse = (11f + Mathf.Sin(t * 18f) * 1.5f) * impactIn * impactOut;
                impactPulse.position = new Vector3(0f, beamImpactY, 48f);
                impactPulse.localScale = Vector3.one * Mathf.Max(0.05f, pulse);
                if (impactRenderer != null) impactRenderer.enabled = t >= 0.45f && t < 1.65f;
            }

            if (interiorTransferPulse != null && t >= 1.05f)
            {
                if (!interiorPulseShown)
                {
                    interiorPulseShown = true;
                    if (transferRenderer != null) transferRenderer.enabled = true;
                    StartupCheckpoint.Set("StageFourUplinkEnteredMothership");
                }
                float feed = Mathf.Clamp01((t - 1.05f) / 1.55f);
                interiorTransferPulse.position = new Vector3(0f, 1.45f, Mathf.Lerp(82f, -12f, feed));
                interiorTransferPulse.localScale = Vector3.one * (1.2f + Mathf.Sin(t * 15f) * 0.12f);
                if (transferRenderer != null) transferRenderer.enabled = t < 2.85f;
            }

            IntroMessage = t < 0.95f ? "GREEN UPLINK DESCENDING FAST" :
                t < 1.55f ? "UPLINK IMPACT • MOTHERSHIP HULL" :
                t < 2.85f ? "ENERGY TRANSFER ENTERING THE SHIP" :
                t < 4.65f ? "CAMERA SWEEPING INTO THE HANGAR" :
                "INSIDE THE MOTHERSHIP • HOSTILES DETECTED";
            yield return null;
        }

        IntroMessage = "TRANSFER COMPLETE • MOTHERSHIP INTERIOR";
        yield return new WaitForSecondsRealtime(0.35f);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = wasEnabled[i];

        if (pilot != null && gameplayCamera != null)
        {
            Quaternion yaw = Quaternion.Euler(0f, pilot.eulerAngles.y, 0f);
            Vector3 cameraTarget = pilot.position + Vector3.up * 2.1f;
            gameplayCamera.transform.position = cameraTarget - yaw * Vector3.forward * 7.2f + Vector3.up * 3.2f;
            gameplayCamera.transform.rotation = Quaternion.LookRotation(cameraTarget - gameplayCamera.transform.position, Vector3.up);
            gameplayCamera.farClipPlane = Mathf.Max(savedFarClip, 900f);
            gameplayCamera.clearFlags = CameraClearFlags.SolidColor;
            gameplayCamera.backgroundColor = new Color(0.015f, 0.025f, 0.04f);
        }

        // Unlock after dropping the cinematic gate; Update() disables these while
        // introRunning is true, so unlocking before the final wait is undone each frame.
        introRunning = false;
        if (controller != null && !controller.IsDefeated)
        {
            controller.SetMoveInput(Vector2.zero);
            controller.ClearAimDirection();
            controller.SetMovementEnabled(true);
            if (controller.Weapon != null) controller.Weapon.enabled = true;
            if (controller.Aim != null) controller.Aim.enabled = true;
        }
        for (int i = 0; i < interiorHostiles.Count; i++)
            if (interiorHostiles[i] != null) interiorHostiles[i].enabled = true;

        IntroMessage = "STAGE 4 • SECURE THE MOTHERSHIP";
        StartupCheckpoint.Set("StageFourArrivalCinematicComplete_PlayerInsideMothership");
        StartupCheckpoint.Set("StageFourControlsAndInteriorHostilesEnabled");
    }

    private static Vector3 EvaluatePath(Vector3[] points, float t)
    {
        if (points == null || points.Length == 0) return Vector3.zero;
        if (points.Length == 1) return points[0];
        float scaled = Mathf.Clamp01(t) * (points.Length - 1);
        int index = Mathf.Min(points.Length - 2, Mathf.FloorToInt(scaled));
        float segmentT = scaled - index;
        segmentT = segmentT * segmentT * (3f - 2f * segmentT);
        return Vector3.Lerp(points[index], points[index + 1], segmentT);
    }

    private IEnumerator BuildExteriorMegabaseRoutine()
    {
        // Orbital city platform stays below the playable deck so it never occludes combat inside.
        Box("S4_OrbitalFoundation", new Vector3(0f, -22f, 0f), new Vector3(980f, 36f, 980f), dark);
        Box("S4_CentralSpine", new Vector3(0f, -12f, 0f), new Vector3(260f, 8f, 520f), metal);
        Box("S4_MainTransitRing", new Vector3(0f, -9f, 0f), new Vector3(780f, 4f, 780f), dark);

        for (int x = -4; x <= 4; x++)
        for (int z = -4; z <= 4; z++)
        {
            if (Mathf.Abs(x) < 2 && Mathf.Abs(z) < 2) continue;
            float px = x * 92f, pz = z * 92f;
            float h = 26f + ((x * 7 + z * 11 + 37) & 3) * 15f;
            Box("S4_CityDistrict_" + x + "_" + z, new Vector3(px, h * 0.5f - 2f, pz),
                new Vector3(48f + Mathf.Abs(z % 3) * 9f, h, 52f + Mathf.Abs(x % 3) * 8f),
                (x + z) % 2 == 0 ? metal : brightMetal);
            for (int w = -1; w <= 1; w++)
                Box("S4_DistrictLight_" + x + "_" + z + "_" + w,
                    new Vector3(px + w * 12f, h + 0.15f, pz - 25f),
                    new Vector3(3.2f, 0.7f, 0.5f), cyan);
            if (z == 4) yield return null;
        }

        // Eight reinforced docking arms radiate from the central mothership.
        for (int arm = 0; arm < 8; arm++)
        {
            float angle = arm * 45f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 pos = dir * 300f + Vector3.up * 5f;
            Box("S4_DockingArm_" + arm, pos, new Vector3(66f, 12f, 300f), metal, Quaternion.LookRotation(dir));
            Vector3 padPos = dir * 395f + Vector3.up * 10f;
            Box("S4_DockPad_" + arm, padPos, new Vector3(102f, 5f, 78f), brightMetal, Quaternion.LookRotation(dir));
            Vector3 collarPos = dir * 205f + Vector3.up * 11f;
            Box("S4_DockingCollar_" + arm, collarPos, new Vector3(46f, 4f, 24f), cyan, Quaternion.LookRotation(dir));
            for (int light = -3; light <= 3; light++)
                Box("S4_DockRunwayLight_" + arm + "_" + light,
                    pos + dir * ((light + 4) * 18f - 54f) + Vector3.up * 12f + Vector3.right * light * 7f,
                    new Vector3(2.8f, 0.7f, 2.8f), green);
            yield return null;
        }

        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24f;
            Vector3 p = new Vector3(Mathf.Cos(a) * 430f, 48f, Mathf.Sin(a) * 430f);
            Box("S4_RingPylon_" + i, p, new Vector3(18f, 92f + (i % 3) * 22f, 18f), brightMetal);
            Box("S4_RingPylonBeacon_" + i, p + Vector3.up * 55f, new Vector3(5f, 3f, 5f), cyan);
            if (i % 3 == 2) yield return null;
        }
    }

    private IEnumerator BuildDockedShipsRoutine()
    {
        // Exactly eight escape shuttles dock directly to the port and starboard rails.
        escapeShuttles.Clear();
        int shuttleIndex = 0;
        for (int sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            int side = sideIndex == 0 ? -1 : 1;
            for (int berth = 0; berth < 4; berth++)
            {
                float z = -94f + berth * 62.5f;
                float y = 16f + ((berth + sideIndex) % 3) * 2.4f;
                Vector3 outward = new Vector3(side, 0f, 0f);
                Vector3 shuttlePosition = new Vector3(side * 133f, y, z);
                CreateVisualCylinder("S4_EscapeShuttleDockTube_" + shuttleIndex,
                    new Vector3(side * 113f, y, z), outward, 30f, 2.7f,
                    shuttleIndex % 2 == 0 ? brightMetal : metal);
                Box("S4_EscapeShuttleDockCollar_" + shuttleIndex,
                    new Vector3(side * 102f, y, z), new Vector3(9f, 5f, 13f),
                    cyan, Quaternion.Euler(0f, side * 90f, 0f));

                GameObject shuttle = BuildShip("S4_MothershipEscapeShuttle_" + shuttleIndex,
                    shuttlePosition, Quaternion.LookRotation(outward, Vector3.up),
                    0.58f + ((berth + sideIndex) % 3) * 0.045f);
                if (shuttle != null) escapeShuttles.Add(shuttle.transform);
                shuttleIndex++;
                yield return null;
            }
        }
        StartupCheckpoint.Set("StageFourEightDockedEscapeShuttlesReady");
    }

    private IEnumerator BuildMothershipAndInteriorRoutine()
    {
        BuildMothershipCoreAssembly();

        // The main shell is widest at its central body and narrows at the bow and stern.
        Box("S4_MothershipKeel", new Vector3(0f, -3.9f, 0f), new Vector3(24f, 7f, 278f), dark);
        Box("S4_MothershipMidHullPort", new Vector3(-34f, 20f, -2f), new Vector3(50f, 9f, 104f), brightMetal);
        Box("S4_MothershipMidHullStarboard", new Vector3(34f, 20f, -2f), new Vector3(50f, 9f, 104f), brightMetal);
        Box("S4_MothershipBowHullPort", new Vector3(-22f, 19.5f, 89f), new Vector3(26f, 7f, 48f), brightMetal);
        Box("S4_MothershipBowHullStarboard", new Vector3(22f, 19.5f, 89f), new Vector3(26f, 7f, 48f), brightMetal);
        Box("S4_MothershipAftHullPort", new Vector3(-20f, 18.5f, -91f), new Vector3(24f, 6f, 48f), metal);
        Box("S4_MothershipAftHullStarboard", new Vector3(20f, 18.5f, -91f), new Vector3(24f, 6f, 48f), metal);
        Box("S4_MothershipDorsalBayLipPort", new Vector3(-12f, 18f, 58f), new Vector3(3f, 3f, 106f), metal);
        Box("S4_MothershipDorsalBayLipStarboard", new Vector3(12f, 18f, 58f), new Vector3(3f, 3f, 106f), metal);
        CreateVisualPrimitive(PrimitiveType.Sphere, "S4_MothershipPortKeelPod",
            new Vector3(-64f, 11f, 0f), new Vector3(14f, 9f, 158f), dark);
        CreateVisualPrimitive(PrimitiveType.Sphere, "S4_MothershipStarboardKeelPod",
            new Vector3(64f, 11f, 0f), new Vector3(14f, 9f, 158f), dark);
        CreateVisualPrimitive(PrimitiveType.Sphere, "S4_MothershipProwArmor",
            new Vector3(0f, 11f, 126f), new Vector3(34f, 9f, 27f), metal);
        CreateVisualPrimitive(PrimitiveType.Sphere, "S4_MothershipProwTip",
            new Vector3(0f, 10.5f, 149f), new Vector3(15f, 5.5f, 24f), brightMetal);
        CreateVisualPrimitive(PrimitiveType.Sphere, "S4_MothershipAftArmor",
            new Vector3(0f, 10f, -125f), new Vector3(30f, 8f, 25f), metal);
        CreateVisualPrimitive(PrimitiveType.Sphere, "S4_MothershipSternTip",
            new Vector3(0f, 9.8f, -148f), new Vector3(14f, 5.5f, 24f), brightMetal);
        Box("S4_MothershipBridge", new Vector3(0f, 37f, 49f), new Vector3(66f, 16f, 44f), metal);
        Box("S4_MothershipBridgeGlass", new Vector3(0f, 38f, 73f), new Vector3(52f, 9f, 14f), glass);
        Box("S4_MothershipBridgePortFin", new Vector3(-40f, 30f, 53f), new Vector3(14f, 7f, 42f), brightMetal);
        Box("S4_MothershipBridgeStarboardFin", new Vector3(40f, 30f, 53f), new Vector3(14f, 7f, 42f), brightMetal);

        // Keep the rear engines aligned inside the tapered stern instead of forming a wide block.
        for (int i = -2; i <= 2; i++)
        {
            float x = i * 15f;
            Box("S4_MothershipAftEngine_" + i, new Vector3(x, 5f, -146f), new Vector3(10f, 9f, 22f), metal);
            Box("S4_MothershipAftEngineGlow_" + i, new Vector3(x, 5f, -159f),
                new Vector3(6f, 6f, 3f), i % 2 == 0 ? green : cyan);
        }
        for (int i = -4; i <= 4; i++)
        {
            float z = i * 30f;
            Box("S4_MothershipSideBeaconL_" + i, new Vector3(-65f, 13f, z), new Vector3(3f, 2f, 8f), cyan);
            Box("S4_MothershipSideBeaconR_" + i, new Vector3(65f, 13f, z), new Vector3(3f, 2f, 8f), green);
            if (i % 2 == 0) yield return null;
        }

        Box("S4_InteriorHangarFloor", new Vector3(0f, -0.35f, 0f), new Vector3(94f, 0.7f, 94f), floor);
        // Keep an open 28m dorsal channel above the main combat lane. This lets
        // the intro camera descend through the hull into the hangar without passing
        // through an opaque ceiling slab; side roof panels still enclose the room.
        Box("S4_HangarCeilingPort", new Vector3(-30.5f, 15f, 0f), new Vector3(33f, 2f, 94f), dark);
        Box("S4_HangarCeilingStarboard", new Vector3(30.5f, 15f, 0f), new Vector3(33f, 2f, 94f), dark);

        // Open side portals from the hangar plus a rear hatch to keep branches connected.
        Box("S4_HangarWallLeftFront", new Vector3(-47f, 7f, 34f), new Vector3(2f, 14f, 26f), metal);
        Box("S4_HangarWallLeftRear", new Vector3(-47f, 7f, -34f), new Vector3(2f, 14f, 26f), metal);
        Box("S4_HangarWallRightFront", new Vector3(47f, 7f, 34f), new Vector3(2f, 14f, 26f), metal);
        Box("S4_HangarWallRightRear", new Vector3(47f, 7f, -34f), new Vector3(2f, 14f, 26f), metal);
        Box("S4_HangarRearWallLeft", new Vector3(-31f, 7f, -47f), new Vector3(32f, 14f, 2f), metal);
        Box("S4_HangarRearWallRight", new Vector3(31f, 7f, -47f), new Vector3(32f, 14f, 2f), metal);
        Box("S4_HangarRearLintel", new Vector3(0f, 13.4f, -47f), new Vector3(30f, 1.2f, 2f), brightMetal);

        Box("S4_HangarDoorLeft", new Vector3(-36f, 7f, 47f), new Vector3(22f, 14f, 3f), brightMetal);
        Box("S4_HangarDoorRight", new Vector3(36f, 7f, 47f), new Vector3(22f, 14f, 3f), brightMetal);
        Box("S4_HangarDoorHeader", new Vector3(0f, 14f, 47f), new Vector3(50f, 2f, 3f), brightMetal);

        Box("S4_InteriorForwardCorridorFloor", new Vector3(0f, -0.35f, 62.5f), new Vector3(24f, 0.7f, 31f), floor);
        Box("S4_InteriorForwardCorridorLeft", new Vector3(-12f, 6.5f, 62.5f), new Vector3(1.5f, 13f, 31f), metal);
        Box("S4_InteriorForwardCorridorRight", new Vector3(12f, 6.5f, 62.5f), new Vector3(1.5f, 13f, 31f), metal);
        // Leave the centerline open overhead for the camera's continuous entry path.
        Box("S4_InteriorForwardCorridorRoofPort", new Vector3(-8f, 15f, 62.5f), new Vector3(8f, 2f, 31f), dark);
        Box("S4_InteriorForwardCorridorRoofStarboard", new Vector3(8f, 15f, 62.5f), new Vector3(8f, 2f, 31f), dark);
        Box("S4_InteriorCommandRoomFloor", new Vector3(0f, -0.35f, 90f), new Vector3(52f, 0.7f, 26f), floor);
        Box("S4_InteriorCommandRoomCeilingPort", new Vector3(-20f, 15f, 90f), new Vector3(12f, 2f, 26f), dark);
        Box("S4_InteriorCommandRoomCeilingStarboard", new Vector3(20f, 15f, 90f), new Vector3(12f, 2f, 26f), dark);
        Box("S4_InteriorCommandFrontLeft", new Vector3(-20f, 6.5f, 77f), new Vector3(16f, 13f, 1.5f), metal);
        Box("S4_InteriorCommandFrontRight", new Vector3(20f, 6.5f, 77f), new Vector3(16f, 13f, 1.5f), metal);
        Box("S4_InteriorCommandRearWall", new Vector3(0f, 6.5f, 103f), new Vector3(52f, 13f, 1.5f), metal);
        Box("S4_InteriorCommandWallL", new Vector3(-26f, 6.5f, 90f), new Vector3(1.5f, 13f, 26f), metal);
        Box("S4_InteriorCommandWallR", new Vector3(26f, 6.5f, 90f), new Vector3(1.5f, 13f, 26f), metal);
        Box("S4_InteriorCommandConsole", new Vector3(0f, 1.2f, 96f), new Vector3(16f, 2.4f, 5f), brightMetal);
        Box("S4_InteriorCommandConsoleGlow", new Vector3(0f, 2.45f, 96f), new Vector3(12f, 0.12f, 2.4f), green);

        Box("S4_InteriorRearCorridorFloor", new Vector3(0f, -0.35f, -62.5f), new Vector3(24f, 0.7f, 31f), floor);
        Box("S4_InteriorRearCorridorLeft", new Vector3(-12f, 6.5f, -62.5f), new Vector3(1.5f, 13f, 31f), metal);
        Box("S4_InteriorRearCorridorRight", new Vector3(12f, 6.5f, -62.5f), new Vector3(1.5f, 13f, 31f), metal);
        Box("S4_InteriorRearCorridorCeiling", new Vector3(0f, 15f, -62.5f), new Vector3(24f, 2f, 31f), dark);
        Box("S4_InteriorEngineRoomFloor", new Vector3(0f, -0.35f, -89f), new Vector3(56f, 0.7f, 24f), floor);
        Box("S4_InteriorEngineRoomCeiling", new Vector3(0f, 15f, -89f), new Vector3(56f, 2f, 24f), dark);
        Box("S4_InteriorEngineDoorLeft", new Vector3(-20f, 6.5f, -77f), new Vector3(16f, 13f, 1.5f), metal);
        Box("S4_InteriorEngineDoorRight", new Vector3(20f, 6.5f, -77f), new Vector3(16f, 13f, 1.5f), metal);
        Box("S4_InteriorEngineRearWall", new Vector3(0f, 6.5f, -101f), new Vector3(56f, 13f, 1.5f), metal);
        Box("S4_InteriorEngineWallL", new Vector3(-28f, 6.5f, -89f), new Vector3(1.5f, 13f, 24f), metal);
        Box("S4_InteriorEngineWallR", new Vector3(28f, 6.5f, -89f), new Vector3(1.5f, 13f, 24f), metal);
        for (int i = -2; i <= 2; i++)
        {
            float x = i * 8f;
            Box("S4_InteriorEngineCoil_" + i, new Vector3(x, 3f, -88f), new Vector3(2.2f, 5.8f, 13f), brightMetal);
            Box("S4_InteriorEngineCoilGlow_" + i, new Vector3(x, 6.05f, -88f), new Vector3(1.2f, 0.14f, 9f), i % 2 == 0 ? green : cyan);
            yield return null;
        }

        BuildSideCompartment(-1);
        BuildSideCompartment(1);
        CreateVisualBox("S4_InteriorUplinkConduit", new Vector3(0f, 0.035f, 0f), new Vector3(0.85f, 0.07f, 198f), green);

        for (int i = -4; i <= 4; i++)
        {
            Box("S4_HangarFloorStripe_" + i, new Vector3(i * 8f, 0.04f, 3f),
                new Vector3(0.55f, 0.08f, 70f), i == 0 ? green : cyan);
        }
        for (int i = 0; i < 6; i++)
        {
            float z = -32f + i * 13f;
            Box("S4_InteriorPillarL_" + i, new Vector3(-39f, 6.5f, z), new Vector3(3f, 13f, 3f), brightMetal);
            Box("S4_InteriorPillarR_" + i, new Vector3(39f, 6.5f, z), new Vector3(3f, 13f, 3f), brightMetal);
            Box("S4_InteriorPanelL_" + i, new Vector3(-37f, 7f, z), new Vector3(0.5f, 4f, 5f), green);
            Box("S4_InteriorPanelR_" + i, new Vector3(37f, 7f, z), new Vector3(0.5f, 4f, 5f), cyan);
            yield return null;
        }
        BuildShip("S4_InteriorServiceCraft", new Vector3(0f, 1.2f, 25f), Quaternion.identity, 0.36f);
        Box("S4_InteriorConsole", new Vector3(0f, 1.3f, -34f), new Vector3(14f, 2.6f, 4f), brightMetal);
        Box("S4_InteriorConsoleGlow", new Vector3(0f, 2.7f, -34f), new Vector3(10f, 0.18f, 2.2f), green);
    }

    private void BuildMothershipCoreAssembly()
    {
        // Five overlapping egg-shaped hull modules create a connected central spine.
        CreateVisualPrimitive(PrimitiveType.Sphere, "S4_MothershipEgg_Aft",
            new Vector3(0f, 33f, -99f), new Vector3(30f, 11f, 44f), metal);
        CreateVisualPrimitive(PrimitiveType.Sphere, "S4_MothershipEgg_AftCore",
            new Vector3(0f, 35f, -57f), new Vector3(43f, 15f, 53f), brightMetal);
        CreateVisualPrimitive(PrimitiveType.Sphere, "S4_MothershipEgg_Central",
            new Vector3(0f, 36f, -8f), new Vector3(52f, 18f, 58f), metal);
        CreateVisualPrimitive(PrimitiveType.Sphere, "S4_MothershipEgg_Forward",
            new Vector3(0f, 35f, 43f), new Vector3(43f, 15f, 51f), brightMetal);
        CreateVisualPrimitive(PrimitiveType.Sphere, "S4_MothershipEgg_Nose",
            new Vector3(0f, 32f, 87f), new Vector3(26f, 10f, 36f), metal);

        // Long and short cylinders physically link the modules.
        CreateVisualCylinder("S4_MothershipCentralSpine", new Vector3(0f, 35f, -6f),
            Vector3.forward, 222f, 5.5f, brightMetal);
        CreateVisualCylinder("S4_MothershipAxialTube_Aft", new Vector3(0f, 35f, -116f),
            Vector3.forward, 24f, 3.2f, cyan);
        CreateVisualCylinder("S4_MothershipAxialTube_Nose", new Vector3(0f, 34f, 108f),
            Vector3.forward, 20f, 2.8f, green);
        CreateVisualCylinder("S4_MothershipLargeLink_A", new Vector3(0f, 35f, -66f),
            Vector3.forward, 27f, 7.5f, metal);
        CreateVisualCylinder("S4_MothershipLargeLink_B", new Vector3(0f, 36f, 12f),
            Vector3.forward, 36f, 8.5f, brightMetal);
        CreateVisualCylinder("S4_MothershipSmallLink_C", new Vector3(0f, 35f, 61f),
            Vector3.forward, 24f, 3.0f, cyan);

        // Structural rings are made of tube segments, leaving hollow centers.
        BuildMothershipRing("S4_MothershipRing_Aft", new Vector3(0f, 62f, -70f), 25f, 2.8f, 24, cyan);
        BuildMothershipRing("S4_MothershipRing_CoreA", new Vector3(0f, 62f, -22f), 37f, 3.2f, 28, brightMetal);
        BuildMothershipRing("S4_MothershipRing_CoreB", new Vector3(0f, 62f, 34f), 33f, 3.0f, 28, cyan);
        BuildMothershipRing("S4_MothershipRing_Nose", new Vector3(0f, 58f, 80f), 21f, 2.5f, 24, green);

        // Cubes and cuboids around the round core are tied in with short cross-tubes.
        Box("S4_MothershipTechCube_PortA", new Vector3(-60f, 31f, -52f),
            new Vector3(20f, 14f, 24f), metal, Quaternion.Euler(0f, -12f, 0f));
        Box("S4_MothershipTechCube_StarboardA", new Vector3(60f, 31f, -52f),
            new Vector3(20f, 14f, 24f), metal, Quaternion.Euler(0f, 12f, 0f));
        Box("S4_MothershipTechCuboid_PortB", new Vector3(-63f, 32f, 26f),
            new Vector3(28f, 12f, 36f), brightMetal, Quaternion.Euler(0f, -10f, 0f));
        Box("S4_MothershipTechCuboid_StarboardB", new Vector3(63f, 32f, 26f),
            new Vector3(28f, 12f, 36f), brightMetal, Quaternion.Euler(0f, 10f, 0f));

        CreateVisualCylinder("S4_MothershipCrossTube_PortA", new Vector3(-45f, 31f, -52f),
            Vector3.left, 34f, 4.5f, cyan);
        CreateVisualCylinder("S4_MothershipCrossTube_StarboardA", new Vector3(45f, 31f, -52f),
            Vector3.right, 34f, 4.5f, cyan);
        CreateVisualCylinder("S4_MothershipCrossTube_PortB", new Vector3(-48f, 32f, 26f),
            Vector3.left, 34f, 4.0f, metal);
        CreateVisualCylinder("S4_MothershipCrossTube_StarboardB", new Vector3(48f, 32f, 26f),
            Vector3.right, 34f, 4.0f, metal);

        CreateVisualCylinder("S4_MothershipRingSpoke_Port", new Vector3(-21f, 62f, -22f),
            Vector3.right, 42f, 1.8f, cyan);
        CreateVisualCylinder("S4_MothershipRingSpoke_Starboard", new Vector3(21f, 62f, -22f),
            Vector3.left, 42f, 1.8f, cyan);
    }

    private void BuildMothershipRing(string name, Vector3 center, float radius, float tubeRadius, int segments, Material material)
    {
        segments = Mathf.Max(12, segments);
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 p0 = center + new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0f);
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f);
            Vector3 tangent = p1 - p0;
            GameObject segment = CreateVisualPrimitive(PrimitiveType.Cylinder, name + "_" + i, material);
            segment.transform.position = (p0 + p1) * 0.5f;
            segment.transform.rotation = Quaternion.FromToRotation(Vector3.up, tangent.normalized);
            segment.transform.localScale = new Vector3(tubeRadius * 2f, tangent.magnitude * 0.5f, tubeRadius * 2f);
        }
    }

    private void BuildSideCompartment(int side)
    {
        string label = side < 0 ? "Port" : "Starboard";
        float sign = side;
        float x = sign * 74f;
        Box("S4_Interior" + label + "RoomFloor", new Vector3(x, -0.35f, 0f), new Vector3(42f, 0.7f, 52f), floor);
        Box("S4_Interior" + label + "RoomCeiling", new Vector3(x, 15f, 0f), new Vector3(42f, 2f, 52f), dark);
        Box("S4_Interior" + label + "OuterWall", new Vector3(sign * 95f, 6.5f, 0f), new Vector3(1.5f, 13f, 52f), metal);
        Box("S4_Interior" + label + "FrontWall", new Vector3(x, 6.5f, 26f), new Vector3(42f, 13f, 1.5f), metal);
        Box("S4_Interior" + label + "RearWall", new Vector3(x, 6.5f, -26f), new Vector3(42f, 13f, 1.5f), metal);
        Box("S4_Interior" + label + "InnerWallFront", new Vector3(sign * 53f, 6.5f, 17f), new Vector3(1.5f, 13f, 18f), metal);
        Box("S4_Interior" + label + "InnerWallRear", new Vector3(sign * 53f, 6.5f, -17f), new Vector3(1.5f, 13f, 18f), metal);

        // Cargo lockers are kept against the outer wall; the center remains a clear combat lane.
        for (int i = -1; i <= 1; i++)
        {
            float z = i * 13f;
            Box("S4_Interior" + label + "Locker_" + i, new Vector3(sign * 86f, 1.1f, z), new Vector3(8f, 2.2f, 7f), brightMetal);
            CreateVisualBox("S4_Interior" + label + "Panel_" + i, new Vector3(sign * 54.5f, 8.1f, z),
                new Vector3(0.22f, 3.8f, 6.8f), side < 0 ? green : cyan);
        }
    }

    private void BuildGreenUplink()
    {
        BuildTransferPlanet();
        var beam = CreateVisualPrimitive(PrimitiveType.Cylinder, "S4_GreenUplinkFromSpace", green);
        beam.transform.position = new Vector3(0f, 319.8f, 48f);
        beam.transform.localScale = new Vector3(13f, 0.12f, 13f);
        greenBeam = beam.transform;

        var core = CreateVisualPrimitive(PrimitiveType.Cylinder, "S4_GreenUplinkCore", cyan);
        core.transform.position = new Vector3(0f, 319.8f, 48f);
        core.transform.localScale = new Vector3(4.2f, 0.12f, 4.2f);
        greenBeamCore = core.transform;

        var pulse = CreateVisualPrimitive(PrimitiveType.Sphere, "S4_UplinkImpactPulse", green);
        pulse.transform.position = new Vector3(0f, 44f, 48f);
        pulse.transform.localScale = Vector3.one * 0.2f;
        impactPulse = pulse.transform;

        var transfer = CreateVisualPrimitive(PrimitiveType.Sphere, "S4_InteriorTransferPulse", cyan);
        transfer.transform.position = new Vector3(0f, 1.45f, 82f);
        transfer.transform.localScale = Vector3.one * 1.2f;
        Renderer transferRenderer = transfer.GetComponent<Renderer>();
        if (transferRenderer != null) transferRenderer.enabled = false;
        interiorTransferPulse = transfer.transform;

        Box("S4_UplinkImpactReceiver", new Vector3(0f, 26f, 48f), new Vector3(48f, 3f, 34f), green);
        Box("S4_UplinkBridgeRing", new Vector3(0f, 48f, 48f), new Vector3(100f, 3f, 92f), metal);
    }

    private void BuildTransferPlanet()
    {
        // A curved blue/green planetary horizon sits below the city-scale orbital base.
        // It is visual-only: no planet collider can interfere with the playable hangar.
        var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        planet.name = "S4_TransferPlanet";
        planet.transform.SetParent(root, false);
        planet.transform.position = new Vector3(0f, -1000f, 0f);
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

    private GameObject BuildShip(string name, Vector3 position, Quaternion rotation, float scale)
    {
        var ship = new GameObject(name);
        ship.transform.SetParent(root, false);
        ship.transform.position = position;
        ship.transform.rotation = rotation;
        ship.transform.localScale = Vector3.one * scale;
        PartPrimitive(ship.transform, PrimitiveType.Sphere, "HullEgg", Vector3.zero,
            new Vector3(8.5f, 2.5f, 19f), brightMetal);
        PartPrimitive(ship.transform, PrimitiveType.Sphere, "TaperedNose",
            new Vector3(0f, 0.18f, 12f), new Vector3(4.4f, 1.4f, 10f), metal);
        PartPrimitive(ship.transform, PrimitiveType.Sphere, "NoseTip",
            new Vector3(0f, 0.12f, 17.8f), new Vector3(2.1f, 0.8f, 4.4f), brightMetal);
        PartPrimitive(ship.transform, PrimitiveType.Sphere, "CockpitCanopy",
            new Vector3(0f, 1.35f, 3.5f), new Vector3(3.6f, 1.6f, 4.8f), glass);
        Part(ship.transform, "SweptWingPort", new Vector3(-6.1f, -0.18f, -1.4f),
            new Vector3(7.6f, 0.55f, 8.8f), metal);
        Part(ship.transform, "SweptWingStarboard", new Vector3(6.1f, -0.18f, -1.4f),
            new Vector3(7.6f, 0.55f, 8.8f), metal);
        PartPrimitive(ship.transform, PrimitiveType.Cylinder, "EngineTubePort",
            new Vector3(-2.25f, -0.1f, -12f), new Vector3(1.4f, 5.5f, 1.4f),
            brightMetal, Quaternion.Euler(90f, 0f, 0f));
        PartPrimitive(ship.transform, PrimitiveType.Cylinder, "EngineTubeStarboard",
            new Vector3(2.25f, -0.1f, -12f), new Vector3(1.4f, 5.5f, 1.4f),
            brightMetal, Quaternion.Euler(90f, 0f, 0f));
        PartPrimitive(ship.transform, PrimitiveType.Sphere, "EngineGlowPort",
            new Vector3(-2.25f, -0.1f, -17.2f), new Vector3(1.5f, 1.1f, 1.5f), cyan);
        PartPrimitive(ship.transform, PrimitiveType.Sphere, "EngineGlowStarboard",
            new Vector3(2.25f, -0.1f, -17.2f), new Vector3(1.5f, 1.1f, 1.5f), green);
        return ship;
    }

    private void PartPrimitive(Transform parent, PrimitiveType primitiveType, string name,
        Vector3 localPosition, Vector3 localScale, Material material, Quaternion localRotation = default(Quaternion))
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        if (localRotation != default(Quaternion)) part.transform.localRotation = localRotation;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }

    private void CreateVisualCylinder(string name, Vector3 center, Vector3 axis, float length, float radius, Material material)
    {
        GameObject cylinder = CreateVisualPrimitive(PrimitiveType.Cylinder, name, material);
        cylinder.transform.position = center;
        Vector3 direction = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;
        cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        cylinder.transform.localScale = new Vector3(radius * 2f, Mathf.Max(0.05f, length * 0.5f), radius * 2f);
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

    private GameObject CreateVisualPrimitive(PrimitiveType primitiveType, string name, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(primitiveType);
        go.name = name;
        go.transform.SetParent(root, true);
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        return go;
    }

    private GameObject CreateVisualPrimitive(
        PrimitiveType primitiveType, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject go = CreateVisualPrimitive(primitiveType, name, material);
        go.transform.position = position;
        go.transform.localScale = size;
        return go;
    }

    private GameObject CreateVisualBox(string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.position = position;
        go.transform.localScale = size;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        return go;
    }

    private void SpawnInteriorAliens()
    {
        if (controller == null || root == null || interiorHostiles.Count > 0)
            return;

        Vector3[] positions =
        {
            new Vector3(-6f, 0f, 27f),
            new Vector3(7f, 0f, 38f),
            new Vector3(-69f, 0f, 5f),
            new Vector3(-78f, 0f, -15f),
            new Vector3(69f, 0f, 5f),
            new Vector3(78f, 0f, -15f),
            new Vector3(0f, 0f, -62f),
            new Vector3(21f, 0f, -88f),
            new Vector3(9f, 0f, 87f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            int archetype = i % 3 + 1;
            GameObject alien = new GameObject("S4_AlienTrooper_" + (i + 1).ToString("00"));
            alien.transform.SetParent(root, false);
            alien.transform.position = positions[i];
            Vector3 toPlayer = controller.transform.position - alien.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
                alien.transform.rotation = Quaternion.LookRotation(toPlayer.normalized);

            CapsuleCollider capsule = alien.AddComponent<CapsuleCollider>();
            capsule.radius = 0.48f;
            capsule.height = 1.95f;
            capsule.center = new Vector3(0f, 0.97f, 0f);

            TargetHealth health = alien.AddComponent<TargetHealth>();
            health.SetMaxHealth(archetype == 2 ? 135 : (archetype == 3 ? 120 : 95));

            EnemyChase chase = alien.AddComponent<EnemyChase>();
            chase.Configure(controller.transform, archetype);
            chase.enabled = false;
            interiorHostiles.Add(chase);
            BuildAlienTrooperDetails(alien.transform, archetype);
        }
        StartupCheckpoint.Set("StageFourInteriorAliensSpawned_" + interiorHostiles.Count);
    }

    private void BuildAlienTrooperDetails(Transform alien, int archetype)
    {
        Material armor = archetype == 2 ? brightMetal : alienArmor;
        CreateAlienDetail(alien, "S4_AlienHead", new Vector3(0f, 1.48f, 0.04f),
            new Vector3(0.62f, 0.53f, 0.52f), alienDark);
        CreateAlienDetail(alien, "S4_AlienChestPlate", new Vector3(0f, 1.02f, 0.33f),
            new Vector3(0.68f, 0.56f, 0.18f), armor);
        CreateAlienDetail(alien, "S4_AlienShoulderL", new Vector3(-0.43f, 1.13f, 0.08f),
            new Vector3(0.32f, 0.38f, 0.42f), armor);
        CreateAlienDetail(alien, "S4_AlienShoulderR", new Vector3(0.43f, 1.13f, 0.08f),
            new Vector3(0.32f, 0.38f, 0.42f), armor);
        CreateAlienDetail(alien, "S4_AlienEyeL", new Vector3(-0.16f, 1.53f, 0.47f),
            new Vector3(0.12f, 0.09f, 0.07f), alienEye);
        CreateAlienDetail(alien, "S4_AlienEyeR", new Vector3(0.16f, 1.53f, 0.47f),
            new Vector3(0.12f, 0.09f, 0.07f), alienEye);
        CreateAlienDetail(alien, "S4_AlienAntennaL", new Vector3(-0.24f, 1.91f, 0.08f),
            new Vector3(0.07f, 0.28f, 0.07f), alienEye);
        CreateAlienDetail(alien, "S4_AlienAntennaR", new Vector3(0.24f, 1.91f, 0.08f),
            new Vector3(0.07f, 0.28f, 0.07f), alienEye);
    }

    private void CreateAlienDetail(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
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
        // Only architecture is solid. Decorative stripes and glow panels must never
        // touch the player's movement sphere; these colliders can block the whole lane.
        bool decorative = name.StartsWith("S4_HangarFloorStripe_") ||
            name.Contains("ConsoleGlow") || name.Contains("Panel_") ||
            name.Contains("EngineCoilGlow") || name.Contains("SideBeacon") ||
            name.Contains("AftEngineGlow") || name.Contains("DistrictLight") ||
            name.Contains("RunwayLight") || name.Contains("PylonBeacon");
        bool interior = (name.StartsWith("S4_Interior") || name.StartsWith("S4_Hangar")) && !decorative;
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
        if (alienArmor != null) Destroy(alienArmor); if (alienDark != null) Destroy(alienDark);
        if (alienEye != null) Destroy(alienEye);
        interiorHostiles.Clear();
        escapeShuttles.Clear();
        built = false; introRunning = false; controller = null; player = null; gameplayCamera = null;
        greenBeam = null; greenBeamCore = null; impactPulse = null; interiorTransferPulse = null;
    }
}
