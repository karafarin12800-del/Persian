using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PersiaWar.Unity2D5D;

/// <summary>
/// Builds the isolated Stage 3 alien-fortress world at runtime. It deliberately owns
/// every object it creates so test entry/exit cannot rewrite the Stage 1 or Stage 2 layouts.
/// </summary>
public sealed class StageThreeWorldBuilder : MonoBehaviour
{
    private const string WorldRootName = "StageThreeAlienWorld";
    private const float WorldHalfSize = 108f;
    private const float FortressRadius = 25f;
    private const int RouteCount = 9;

    private Transform worldRoot;
    private Transform legacyCityRoot;
    private PlayerController localPlayer;
    private Camera gameplayCamera;
    private Transform liftPlatform;
    private Transform beamColumn;
    private Material groundMaterial;
    private Material silverMaterial;
    private Material darkMetalMaterial;
    private Material windowMaterial;
    private Material cyanGlowMaterial;
    private Material greenGlowMaterial;
    private Material warningMaterial;
    private readonly List<HovercraftPatrol> hovercrafts = new List<HovercraftPatrol>();
    private bool liftSequenceStarted;
    private bool isComplete;
    private bool worldBuilt;
    private float nextHazardDamageTime;
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private Color savedCameraBackground;

    private sealed class HovercraftPatrol
    {
        public Transform Root;
        public float Radius;
        public float AngularSpeed;
        public float Angle;
        public float Height;
        public float DamageCooldown;
        public float NextDamageTime;
    }

    public bool IsComplete => isComplete;
    public bool IsCinematic { get; private set; }
    public bool IsBuilt => worldBuilt;

    public static void ClearExistingWorld()
    {
        StageThreeWorldBuilder[] builders = FindObjectsByType<StageThreeWorldBuilder>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < builders.Length; i++)
        {
            if (builders[i] != null)
                builders[i].ClearWorldAndRestorePrevious();
        }
    }

    public bool BuildWorld()
    {
        ClearWorldAndRestorePrevious();
        GameObject oldWorld = FindSceneObject(WorldRootName);
        if (oldWorld != null)
            Destroy(oldWorld);

        GameObject city = FindSceneObject("BattleRoyaleCity");
        if (city != null)
        {
            legacyCityRoot = city.transform;
            legacyCityRoot.gameObject.SetActive(false);
        }

        GameObject rootObject = new GameObject(WorldRootName);
        rootObject.transform.SetParent(transform, false);
        worldRoot = rootObject.transform;

        groundMaterial = CreateMaterial("S3_ObsidianGround", new Color(0.055f, 0.068f, 0.085f));
        silverMaterial = CreateMaterial("S3_BrushedAlienSilver", new Color(0.53f, 0.60f, 0.67f));
        darkMetalMaterial = CreateMaterial("S3_DarkTitanium", new Color(0.115f, 0.145f, 0.18f));
        windowMaterial = CreateMaterial("S3_DeepGlass", new Color(0.035f, 0.12f, 0.17f), new Color(0.015f, 0.20f, 0.27f));
        cyanGlowMaterial = CreateMaterial("S3_CyanEnergy", new Color(0.12f, 0.85f, 1f), new Color(0.05f, 0.75f, 1f));
        greenGlowMaterial = CreateMaterial("S3_UplinkEnergy", new Color(0.34f, 1f, 0.73f), new Color(0.10f, 0.9f, 0.55f));
        warningMaterial = CreateMaterial("S3_HazardWarning", new Color(1f, 0.23f, 0.12f), new Color(1f, 0.08f, 0.025f));

        BuildLighting();
        BuildGroundAndBoundary();
        BuildAlienCity();
        BuildFortressAndNineRoutes();
        BuildHovercraftPatrols();
        BuildLiftAndBeam();

        worldBuilt = true;
        isComplete = false;
        IsCinematic = false;
        liftSequenceStarted = false;
        Debug.Log("PERSIA_STAGE3: alien world built; 9 routes, central lift, upper platform and patrol craft ready.");
        StartupCheckpoint.Set("StageThreeWorldBuilt_9Routes");
        return true;
    }

    public void ConfigurePlayer(PlayerController player, Camera camera)
    {
        localPlayer = player;
        gameplayCamera = camera;
        if (gameplayCamera != null)
        {
            savedCameraPosition = gameplayCamera.transform.position;
            savedCameraRotation = gameplayCamera.transform.rotation;
            savedCameraBackground = gameplayCamera.backgroundColor;
            gameplayCamera.backgroundColor = new Color(0.008f, 0.014f, 0.027f, 1f);
            gameplayCamera.farClipPlane = Mathf.Max(gameplayCamera.farClipPlane, 280f);
        }
    }

    private void Update()
    {
        if (!worldBuilt || isComplete)
            return;

        UpdateHovercrafts();
        ApplyHovercraftThreat();

        if (!liftSequenceStarted && localPlayer != null && !localPlayer.IsDefeated)
        {
            Vector3 flatPlayer = localPlayer.transform.position;
            flatPlayer.y = 0f;
            if (flatPlayer.magnitude <= 3.35f && localPlayer.transform.position.y < 2.5f)
            {
                liftSequenceStarted = true;
                StartCoroutine(RunCentralLiftEnding());
            }
        }
    }

    private void BuildLighting()
    {
        GameObject lightObject = new GameObject("S3_ColdAlienKeyLight");
        lightObject.transform.SetParent(worldRoot, false);
        lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        Light key = lightObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(0.60f, 0.77f, 1f);
        key.intensity = 0.88f;
        key.shadows = LightShadows.None;
        RenderSettings.ambientLight = new Color(0.16f, 0.20f, 0.28f);
    }

    private void BuildGroundAndBoundary()
    {
        CreateBox("S3_ObsidianGround", new Vector3(0f, -0.42f, 0f),
            new Vector3(WorldHalfSize * 2f, 0.8f, WorldHalfSize * 2f), groundMaterial, true);

        // Large angular basalt plates break up the ground without cluttering the routes.
        for (int x = -4; x <= 4; x++)
        {
            for (int z = -4; z <= 4; z++)
            {
                if ((x * 3 + z * 5) % 4 == 0)
                    continue;
                float px = x * 22f + ((x + z) % 3) * 2.4f;
                float pz = z * 22f + ((x - z) % 3) * 2.1f;
                if (new Vector2(px, pz).magnitude < 31f)
                    continue;
                CreateBox("S3_GroundPlate", new Vector3(px, 0.015f, pz),
                    new Vector3(8.5f, 0.045f, 6.8f), darkMetalMaterial, false,
                    Quaternion.Euler(0f, (x * 17 + z * 11) % 35, 0f));
            }
        }

        float edge = WorldHalfSize - 2f;
        CreateBox("S3_BoundaryNorth", new Vector3(0f, 3f, edge),
            new Vector3(WorldHalfSize * 2f, 6f, 2f), darkMetalMaterial, true);
        CreateBox("S3_BoundarySouth", new Vector3(0f, 3f, -edge),
            new Vector3(WorldHalfSize * 2f, 6f, 2f), darkMetalMaterial, true);
        CreateBox("S3_BoundaryEast", new Vector3(edge, 3f, 0f),
            new Vector3(2f, 6f, WorldHalfSize * 2f), darkMetalMaterial, true);
        CreateBox("S3_BoundaryWest", new Vector3(-edge, 3f, 0f),
            new Vector3(2f, 6f, WorldHalfSize * 2f), darkMetalMaterial, true);

        // Arrival lane points the player directly toward the nine-door citadel.
        CreateBox("S3_ApproachLane", new Vector3(0f, 0.025f, -55f),
            new Vector3(13f, 0.06f, 65f), darkMetalMaterial, false);
        for (int i = 0; i < 13; i++)
        {
            float z = -84f + i * 4.5f;
            CreateBox("S3_ApproachGuideLeft", new Vector3(-5.7f, 0.08f, z),
                new Vector3(0.13f, 0.07f, 1.65f), cyanGlowMaterial, false);
            CreateBox("S3_ApproachGuideRight", new Vector3(5.7f, 0.08f, z),
                new Vector3(0.13f, 0.07f, 1.65f), cyanGlowMaterial, false);
        }
    }

    private void BuildAlienCity()
    {
        // A varied grid of tall silver towers; preserve a clear southern approach to the citadel.
        float[] coordinates = { -82f, -62f, -42f, -22f, 22f, 42f, 62f, 82f };
        for (int ix = 0; ix < coordinates.Length; ix++)
        {
            for (int iz = 0; iz < coordinates.Length; iz++)
            {
                float x = coordinates[ix];
                float z = coordinates[iz];
                if (new Vector2(x, z).magnitude < 37f)
                    continue;
                if (Mathf.Abs(x) < 13f && z < -28f)
                    continue;

                int seed = ix * 17 + iz * 31;
                float height = 15f + Mathf.Abs((seed * 7) % 19);
                float width = 7.2f + Mathf.Abs((seed * 3) % 5) * 0.55f;
                float depth = 7.5f + Mathf.Abs((seed * 5) % 4) * 0.65f;
                float yaw = (seed % 4) * 90f;
                Vector3 center = new Vector3(x, height * 0.5f, z);
                CreateBox("S3_SilverTower", center, new Vector3(width, height, depth),
                    silverMaterial, true, Quaternion.Euler(0f, yaw, 0f));
                CreateBox("S3_TowerFoot", new Vector3(x, 0.55f, z),
                    new Vector3(width + 1.5f, 1.1f, depth + 1.5f), darkMetalMaterial, true,
                    Quaternion.Euler(0f, yaw, 0f));

                int floorCount = Mathf.Clamp(Mathf.FloorToInt(height / 4f), 3, 8);
                for (int floor = 1; floor <= floorCount; floor++)
                {
                    float y = Mathf.Min(height - 1.2f, floor * height / (floorCount + 1f));
                    CreateBox("S3_TowerWindowBand", new Vector3(x, y, z - depth * 0.5f - 0.035f),
                        new Vector3(width * 0.62f, 0.36f, 0.07f), windowMaterial, false,
                        Quaternion.Euler(0f, yaw, 0f));
                    CreateBox("S3_TowerLightBand", new Vector3(x, y + 0.43f, z - depth * 0.5f - 0.06f),
                        new Vector3(width * 0.72f, 0.08f, 0.08f), cyanGlowMaterial, false,
                        Quaternion.Euler(0f, yaw, 0f));
                }

                CreateBox("S3_TowerCrown", new Vector3(x, height + 0.35f, z),
                    new Vector3(width * 0.62f, 0.7f, depth * 0.62f), darkMetalMaterial, false,
                    Quaternion.Euler(0f, yaw, 0f));
            }
        }
    }

    private void BuildFortressAndNineRoutes()
    {
        // Nine gates are equally spaced around a massive ring. The gaps between the
        // ring segments align exactly with the nine inward-running corridors.
        for (int route = 0; route < RouteCount; route++)
        {
            float angle = route * 360f / RouteCount;
            Vector3 radial = DirectionAt(angle);
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
            Vector3 gateCenter = radial * FortressRadius;
            Quaternion gateRotation = Quaternion.LookRotation(tangent, Vector3.up);

            CreateBox("S3_GateLeftPillar", gateCenter - tangent * 3.05f + Vector3.up * 6.2f,
                new Vector3(1.3f, 12.4f, 2.2f), silverMaterial, true, gateRotation);
            CreateBox("S3_GateRightPillar", gateCenter + tangent * 3.05f + Vector3.up * 6.2f,
                new Vector3(1.3f, 12.4f, 2.2f), silverMaterial, true, gateRotation);
            CreateBox("S3_GateLintel", gateCenter + Vector3.up * 12.1f,
                new Vector3(7.6f, 1.4f, 2.2f), darkMetalMaterial, true, gateRotation);
            CreateBox("S3_GateEnergyMark", gateCenter + radial * 1.2f + Vector3.up * 8.2f,
                new Vector3(3.1f, 0.18f, 0.18f), greenGlowMaterial, false, gateRotation);

            // Route floors and side walls: nine separate, traversable corridors converge on the lift.
            Vector3 floorCenter = radial * 14.6f;
            CreateBox("S3_RouteFloor_" + route, new Vector3(floorCenter.x, 0.045f, floorCenter.z),
                new Vector3(4.4f, 0.10f, 18.5f), darkMetalMaterial, false,
                Quaternion.Euler(0f, -angle, 0f));

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 wallPosition = radial * 14.6f + tangent * side * 2.25f;
                CreateBox("S3_RouteWall_" + route, wallPosition + Vector3.up * 2.1f,
                    new Vector3(0.42f, 4.2f, 18.2f), silverMaterial, true,
                    Quaternion.Euler(0f, -angle, 0f));
                for (int marker = 0; marker < 4; marker++)
                {
                    Vector3 mark = radial * (7.6f + marker * 4.2f) +
                        tangent * side * 2.03f + Vector3.up * 2.6f;
                    CreateBox("S3_RouteGuide_" + route, mark,
                        new Vector3(0.08f, 0.10f, 1.25f), cyanGlowMaterial, false,
                        Quaternion.Euler(0f, -angle, 0f));
                }
            }

            // Small route number plate, physically part of the gate frame.
            CreateBox("S3_RouteNumberPlate_" + route,
                gateCenter + radial * 1.55f + Vector3.up * 10.1f,
                new Vector3(1.1f, 0.28f, 0.12f), cyanGlowMaterial, false, gateRotation);
        }

        // Solid ring walls fill the gaps between doorways but never close a route.
        for (int segment = 0; segment < RouteCount; segment++)
        {
            float middleAngle = (segment + 0.5f) * 360f / RouteCount;
            Vector3 radial = DirectionAt(middleAngle);
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
            Vector3 center = radial * FortressRadius;
            CreateBox("S3_CitadelRingWall_" + segment,
                new Vector3(center.x, 6.8f, center.z),
                new Vector3(13.0f, 13.6f, 2.2f), silverMaterial, true,
                Quaternion.LookRotation(tangent, Vector3.up));
            CreateBox("S3_CitadelWallTrim_" + segment,
                new Vector3(center.x, 13.8f, center.z),
                new Vector3(13.0f, 0.35f, 2.45f), cyanGlowMaterial, false,
                Quaternion.LookRotation(tangent, Vector3.up));
        }

        // The huge top platform is built in four pieces, leaving an open shaft above the lift.
        CreateBox("S3_TopPlatformWest", new Vector3(-13.5f, 25.2f, 0f),
            new Vector3(18f, 1.1f, 47f), silverMaterial, true);
        CreateBox("S3_TopPlatformEast", new Vector3(13.5f, 25.2f, 0f),
            new Vector3(18f, 1.1f, 47f), silverMaterial, true);
        CreateBox("S3_TopPlatformNorth", new Vector3(0f, 25.2f, 13.5f),
            new Vector3(9f, 1.1f, 20f), silverMaterial, true);
        CreateBox("S3_TopPlatformSouth", new Vector3(0f, 25.2f, -13.5f),
            new Vector3(9f, 1.1f, 20f), silverMaterial, true);

        // A high-tech crown rises above the deck to make the destination visible from afar.
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f;
            Vector3 radial = DirectionAt(angle);
            CreateCylinder("S3_CrownPylon", radial * 21.2f + Vector3.up * 29.1f,
                new Vector3(0.52f, 7.6f, 0.52f), silverMaterial, false);
            CreateBox("S3_CrownLight", radial * 21.2f + Vector3.up * 32.4f,
                new Vector3(0.72f, 0.18f, 0.72f), cyanGlowMaterial, false);
        }
    }

    private void BuildLiftAndBeam()
    {
        // The cylinder is deliberately open-sided: nine paths feed into it, and the
        // player can walk into the centre before the platform carries them to the roof.
        CreateCylinder("S3_LiftBaseRing", new Vector3(0f, 0.12f, 0f),
            new Vector3(8.4f, 0.24f, 8.4f), darkMetalMaterial, false);
        CreateCylinder("S3_LiftInnerRing", new Vector3(0f, 0.27f, 0f),
            new Vector3(6.2f, 0.12f, 6.2f), cyanGlowMaterial, false);

        for (int i = 0; i < 16; i++)
        {
            float angle = i * 22.5f;
            Vector3 radial = DirectionAt(angle);
            CreateCylinder("S3_CylinderEnergyColumn", radial * 4.0f + Vector3.up * 12.6f,
                new Vector3(0.20f, 25.2f, 0.20f), silverMaterial, false);
            CreateCylinder("S3_CylinderInnerGlow", radial * 3.82f + Vector3.up * 12.6f,
                new Vector3(0.07f, 25.2f, 0.07f), greenGlowMaterial, false);
        }

        liftPlatform = CreateCylinder("S3_CentralLiftPlatform", new Vector3(0f, 0.28f, 0f),
            new Vector3(5.4f, 0.36f, 5.4f), silverMaterial, true).transform;
        CreateCylinder("S3_LiftPlatformGlow", new Vector3(0f, 0.49f, 0f),
            new Vector3(4.5f, 0.08f, 4.5f), greenGlowMaterial, false);

        GameObject beam = CreateCylinder("S3_UplinkBeam", new Vector3(0f, 0.1f, 0f),
            new Vector3(1.35f, 0.1f, 1.35f), greenGlowMaterial, false);
        beamColumn = beam.transform;
        beamColumn.gameObject.SetActive(false);

        CreateCylinder("S3_TopUplinkReceiver", new Vector3(0f, 26.1f, 0f),
            new Vector3(7.8f, 0.22f, 7.8f), greenGlowMaterial, false);
    }

    private void BuildHovercraftPatrols()
    {
        float[] radii = { 37f, 45f, 54f, 63f };
        for (int i = 0; i < radii.Length; i++)
        {
            GameObject craft = new GameObject("S3_HostileHovercraft_" + i);
            craft.transform.SetParent(worldRoot, false);
            CreateBoxChild(craft.transform, "CraftArmoredBody", new Vector3(0f, 0f, 0f),
                new Vector3(4.2f, 0.78f, 2.7f), darkMetalMaterial);
            CreateBoxChild(craft.transform, "CraftSilverTop", new Vector3(0f, 0.48f, 0f),
                new Vector3(2.5f, 0.38f, 1.55f), silverMaterial);
            CreateBoxChild(craft.transform, "CraftFrontBlade", new Vector3(0f, 0.02f, 1.55f),
                new Vector3(2.5f, 0.16f, 0.24f), silverMaterial);
            CreateBoxChild(craft.transform, "CraftRedWarning", new Vector3(0f, 0.64f, 0.18f),
                new Vector3(0.78f, 0.13f, 0.18f), warningMaterial);
            for (int side = -1; side <= 1; side += 2)
            {
                CreateCylinderChild(craft.transform, "CraftThruster", new Vector3(side * 1.6f, -0.32f, -0.55f),
                    new Vector3(0.45f, 0.24f, 0.45f), cyanGlowMaterial);
                CreateCylinderChild(craft.transform, "CraftDownwash", new Vector3(side * 1.6f, -0.62f, -0.55f),
                    new Vector3(0.18f, 0.5f, 0.18f), greenGlowMaterial);
            }

            HovercraftPatrol patrol = new HovercraftPatrol
            {
                Root = craft.transform,
                Radius = radii[i],
                AngularSpeed = (i % 2 == 0 ? 1f : -1f) * (0.15f + i * 0.018f),
                Angle = i * 83f,
                Height = 1.8f + (i % 2) * 0.28f,
                DamageCooldown = 1.25f
            };
            hovercrafts.Add(patrol);
            UpdateHovercraftPosition(patrol);
        }
    }

    private void UpdateHovercrafts()
    {
        for (int i = 0; i < hovercrafts.Count; i++)
        {
            HovercraftPatrol patrol = hovercrafts[i];
            if (patrol.Root == null) continue;
            patrol.Angle = Mathf.Repeat(patrol.Angle + patrol.AngularSpeed * Time.deltaTime * Mathf.Rad2Deg, 360f);
            UpdateHovercraftPosition(patrol);
        }
    }

    private static void UpdateHovercraftPosition(HovercraftPatrol patrol)
    {
        Vector3 position = DirectionAt(patrol.Angle) * patrol.Radius;
        position.y = patrol.Height + Mathf.Sin(Time.time * 1.6f + patrol.Angle) * 0.22f;
        patrol.Root.position = position;
        patrol.Root.rotation = Quaternion.Euler(0f, patrol.Angle + (patrol.AngularSpeed > 0f ? 90f : -90f), 0f);
    }

    private void ApplyHovercraftThreat()
    {
        if (localPlayer == null || localPlayer.IsDefeated || liftSequenceStarted)
            return;

        Vector3 playerPosition = localPlayer.transform.position;
        for (int i = 0; i < hovercrafts.Count; i++)
        {
            HovercraftPatrol craft = hovercrafts[i];
            if (craft.Root == null) continue;
            Vector2 playerFlat = new Vector2(playerPosition.x, playerPosition.z);
            Vector2 craftFlat = new Vector2(craft.Root.position.x, craft.Root.position.z);
            if (Vector2.Distance(playerFlat, craftFlat) > 2.7f ||
                Mathf.Abs(playerPosition.y - craft.Root.position.y) > 2.5f)
                continue;

            if (Time.time < craft.NextDamageTime)
                continue;

            if (localPlayer.Health != null)
                localPlayer.Health.ApplyDamage(12, craft.Root);
            craft.NextDamageTime = Time.time + craft.DamageCooldown;
            StartupCheckpoint.Set("StageThreeHovercraftThreatHit");
        }
    }

    private IEnumerator RunCentralLiftEnding()
    {
        IsCinematic = true;
        StartupCheckpoint.Set("StageThreeCentralLiftStarted");
        if (localPlayer != null)
        {
            localPlayer.SetMovementEnabled(false);
            localPlayer.enabled = false;
            if (localPlayer.Weapon != null) localPlayer.Weapon.enabled = false;
            if (localPlayer.Aim != null) localPlayer.Aim.enabled = false;
        }

        if (liftPlatform == null || localPlayer == null)
            yield break;

        Vector3 playerOffset = localPlayer.transform.position - liftPlatform.position;
        playerOffset.y = 0.95f;
        Vector3 platformStart = new Vector3(0f, 0.28f, 0f);
        Vector3 platformTop = new Vector3(0f, 24.1f, 0f);
        float elapsed = 0f;
        const float elevatorDuration = 4.0f;

        while (elapsed < elevatorDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / elevatorDuration));
            Vector3 platformPosition = Vector3.Lerp(platformStart, platformTop, t);
            liftPlatform.position = platformPosition;
            localPlayer.transform.position = platformPosition + playerOffset;
            localPlayer.transform.rotation = Quaternion.identity;
            yield return null;
        }

        liftPlatform.position = platformTop;
        localPlayer.transform.position = platformTop + playerOffset;
        localPlayer.transform.position = new Vector3(localPlayer.transform.position.x, 25.2f, localPlayer.transform.position.z);
        StartupCheckpoint.Set("StageThreePlayerReachedUpperPlatform");

        // The uplink starts at the base and climbs the shaft. When it reaches the hero,
        // the beam carries them beyond the roofline and the stage ends.
        if (beamColumn != null)
        {
            beamColumn.gameObject.SetActive(true);
            float beamElapsed = 0f;
            const float beamDuration = 2.4f;
            while (beamElapsed < beamDuration)
            {
                beamElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(beamElapsed / beamDuration);
                float beamHeight = Mathf.Lerp(0.25f, 25.5f, t);
                beamColumn.position = new Vector3(0f, beamHeight * 0.5f, 0f);
                beamColumn.localScale = new Vector3(1.35f, beamHeight * 0.5f, 1.35f);
                yield return null;
            }
        }

        Vector3 liftStart = localPlayer.transform.position;
        float carryElapsed = 0f;
        const float carryDuration = 2.0f;
        while (carryElapsed < carryDuration)
        {
            carryElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(carryElapsed / carryDuration));
            localPlayer.transform.position = liftStart + Vector3.up * (30f * t);
            if (gameplayCamera != null)
            {
                Vector3 cameraTarget = localPlayer.transform.position + new Vector3(0f, 5.5f, -11f);
                gameplayCamera.transform.position = Vector3.Lerp(
                    gameplayCamera.transform.position, cameraTarget,
                    1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
                gameplayCamera.transform.rotation = Quaternion.LookRotation(
                    localPlayer.transform.position + Vector3.up * 1.1f - gameplayCamera.transform.position,
                    Vector3.up);
            }
            yield return null;
        }

        isComplete = true;
        IsCinematic = false;
        StartupCheckpoint.Set("StageThreeEndingComplete");
        GameSession.Instance?.EndMission(true);
    }

    private void ClearWorldAndRestorePrevious()
    {
        if (worldRoot != null)
        {
            if (Application.isPlaying)
                Destroy(worldRoot.gameObject);
            else
                DestroyImmediate(worldRoot.gameObject);
            worldRoot = null;
        }

        GameObject namedWorld = FindSceneObject(WorldRootName);
        if (namedWorld != null && namedWorld.transform.parent == transform)
        {
            if (Application.isPlaying) Destroy(namedWorld);
            else DestroyImmediate(namedWorld);
        }

        if (legacyCityRoot == null)
        {
            GameObject city = FindSceneObject("BattleRoyaleCity");
            if (city != null) legacyCityRoot = city.transform;
        }
        if (legacyCityRoot != null && legacyCityRoot.gameObject != null)
            legacyCityRoot.gameObject.SetActive(true);

        if (gameplayCamera != null)
        {
            gameplayCamera.backgroundColor = savedCameraBackground;
            gameplayCamera.transform.position = savedCameraPosition;
            gameplayCamera.transform.rotation = savedCameraRotation;
        }

        hovercrafts.Clear();
        worldBuilt = false;
        isComplete = false;
        IsCinematic = false;
        liftSequenceStarted = false;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        Transform[] all = FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate != null && candidate.gameObject.name == objectName && candidate.gameObject.scene.IsValid())
                return candidate.gameObject;
        }
        return null;
    }

    private GameObject CreateBox(string objectName, Vector3 position, Vector3 scale,
        Material material, bool collider, Quaternion rotation = default(Quaternion))
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(worldRoot, false);
        box.transform.position = position;
        box.transform.localScale = scale;
        if (rotation != default(Quaternion))
            box.transform.rotation = rotation;
        SetRenderer(box, material);
        if (!collider)
            RemoveCollider(box);
        return box;
    }

    private GameObject CreateCylinder(string objectName, Vector3 position, Vector3 scale,
        Material material, bool collider)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = objectName;
        cylinder.transform.SetParent(worldRoot, false);
        cylinder.transform.position = position;
        cylinder.transform.localScale = scale;
        SetRenderer(cylinder, material);
        if (!collider)
            RemoveCollider(cylinder);
        return cylinder;
    }

    private static void CreateBoxChild(Transform parent, string objectName, Vector3 position,
        Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = objectName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        SetRenderer(part, material);
        RemoveCollider(part);
    }

    private static void CreateCylinderChild(Transform parent, string objectName, Vector3 position,
        Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        part.name = objectName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        SetRenderer(part, material);
        RemoveCollider(part);
    }

    private static Material CreateMaterial(string name, Color color, Color? emission = null)
    {
        Material material = RuntimeMaterialFactory.Create(name, color);
        if (material != null && emission.HasValue)
        {
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }
        }
        return material;
    }

    private static void SetRenderer(GameObject target, Material material)
    {
        Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
        if (renderer == null) return;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void RemoveCollider(GameObject target)
    {
        Collider collider = target != null ? target.GetComponent<Collider>() : null;
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
    }

    private static Vector3 DirectionAt(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
    }
}
