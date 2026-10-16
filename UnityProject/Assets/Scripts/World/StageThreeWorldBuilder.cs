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
    private Transform beamCoreColumn;
    private Transform uplinkOrb;
    private Transform entryTransportShip;
    private Material groundMaterial;
    private Material silverMaterial;
    private Material darkMetalMaterial;
    private Material windowMaterial;
    private Material cyanGlowMaterial;
    private Material greenGlowMaterial;
    private Material warningMaterial;
    private Material uplinkOrbMaterial;
    private readonly Material[] alienBodyMaterials = new Material[3];
    private readonly List<HovercraftPatrol> hovercrafts = new List<HovercraftPatrol>();
    private readonly List<GroundAlien> groundAliens = new List<GroundAlien>();
    private readonly List<TargetHealth> hostileHealth = new List<TargetHealth>();
    private bool liftSequenceStarted;
    private bool isComplete;
    private bool worldBuilt;
    private bool groundAliensSpawned;
    private bool introStarted;
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private Color savedCameraBackground;
    private CameraClearFlags savedCameraClearFlags;
    private float savedCameraFarClip;
    private float savedCameraFieldOfView;
    private Color savedAmbientLight;
    private bool ambientCaptured;
    private Renderer[] savedPlayerRenderers;
    private bool[] savedPlayerRendererStates;

    private sealed class HovercraftPatrol
    {
        public Transform Root;
        public Vector3 StartPoint;
        public Vector3 EndPoint;
        public float Progress;
        public float TravelDirection = 1f;
        public float Speed;
        public float Height;
        public float NextShotTime;
        public float NextDamageTime;
        public int Archetype;
        public TargetHealth Health;
        public Vector3 Heading;
        public bool ExplosionStarted;
        public bool Hidden;
        public float ReappearAt;
    }

    private sealed class GroundAlien
    {
        public Transform Root;
        public TargetHealth Health;
        public EnemyChase Chase;
        public bool Guard;
        public float NextShotTime;
        public int Archetype;
        public bool UpperPatrol;
        public Vector3 PatrolCenter;
        public float PatrolRadius;
        public float PatrolPhase;
        public float PatrolSpeed;
    }

    public bool IsComplete => isComplete;
    public string IntroMessage { get; private set; } = "TRANSPORT SHIP INBOUND";
    public bool IsCinematic { get; private set; }
    public bool IsIntroCinematic { get; private set; }
    public bool IsEndingCinematic => IsCinematic && !IsIntroCinematic;
    public bool IsBuilt => worldBuilt;
    public int TotalHostiles => hostileHealth.Count;
    public int RemainingHostiles
    {
        get
        {
            int remaining = 0;
            for (int i = 0; i < hostileHealth.Count; i++)
            {
                TargetHealth health = hostileHealth[i];
                if (health != null && health.CurrentHealth > 0)
                    remaining++;
            }
            return remaining;
        }
    }
    public bool HostilesCleared => RemainingHostiles == 0;

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
        savedAmbientLight = RenderSettings.ambientLight;
        ambientCaptured = true;
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

        // Readable warm sand contrasts with blue-silver architecture.
        groundMaterial = CreateMaterial("S3_SandyAlienGround", new Color(0.57f, 0.43f, 0.27f));
        silverMaterial = CreateMaterial("S3_BlueSilverAlienMetal", new Color(0.48f, 0.66f, 0.82f));
        darkMetalMaterial = CreateMaterial("S3_BlueTitanium", new Color(0.17f, 0.25f, 0.35f));
        windowMaterial = CreateMaterial("S3_DeepBlueGlass", new Color(0.055f, 0.19f, 0.28f), new Color(0.025f, 0.30f, 0.43f));
        cyanGlowMaterial = CreateMaterial("S3_CyanEnergy", new Color(0.12f, 0.88f, 1f), new Color(0.08f, 0.86f, 1f));
        greenGlowMaterial = CreateMaterial("S3_UplinkEnergy", new Color(0.34f, 1f, 0.73f), new Color(0.18f, 1f, 0.67f));
        warningMaterial = CreateMaterial("S3_HazardWarning", new Color(1f, 0.24f, 0.10f), new Color(1f, 0.10f, 0.035f));
        // Deep blue-silver: substantially darker than the surrounding silver walls
        // while keeping the orb visually in the same architectural color family.
        uplinkOrbMaterial = CreateMaterial("S3_UplinkOrbDeepBlueSilver", new Color(0.13f, 0.20f, 0.28f));
        alienBodyMaterials[0] = CreateMaterial("S3_AlienChitin_Teal", new Color(0.075f, 0.30f, 0.34f));
        alienBodyMaterials[1] = CreateMaterial("S3_AlienChitin_Violet", new Color(0.25f, 0.15f, 0.36f));
        alienBodyMaterials[2] = CreateMaterial("S3_AlienChitin_Olive", new Color(0.24f, 0.31f, 0.12f));

        try
        {
            BuildLighting();
            BuildGroundAndBoundary();
            BuildAlienCity();
            BuildFortressAndNineRoutes();
            BuildHovercraftPatrols();
            BuildLiftAndBeam();
        }
        catch (System.Exception exception)
        {
            Debug.LogError("PERSIA_STAGE3: world build failed: " + exception);
            ClearWorldAndRestorePrevious();
            return false;
        }

        worldBuilt = true;
        isComplete = false;
        IsCinematic = false;
        liftSequenceStarted = false;
        Debug.Log("PERSIA_STAGE3: sandy alien world built; 9 routes, 60 ground aliens (including 20 tall area-patrol guards) and 5 looping destructible craft ready.");
        StartupCheckpoint.Set("StageThreeWorldBuilt_9Routes");
        return true;
    }

    public void ConfigurePlayer(PlayerController player, Camera camera)
    {
        localPlayer = player;
        gameplayCamera = camera;
        if (localPlayer != null)
        {
            // Stage 3 starts with a clear, visible survival baseline.
            if (localPlayer.Health != null)
                localPlayer.Health.SetMaxHealth(150);
            localPlayer.SetShield(100);
        }
        if (gameplayCamera != null)
        {
            savedCameraPosition = gameplayCamera.transform.position;
            savedCameraRotation = gameplayCamera.transform.rotation;
            savedCameraBackground = gameplayCamera.backgroundColor;
            savedCameraClearFlags = gameplayCamera.clearFlags;
            savedCameraFarClip = gameplayCamera.farClipPlane;
            savedCameraFieldOfView = gameplayCamera.fieldOfView;
            gameplayCamera.clearFlags = CameraClearFlags.SolidColor;
            gameplayCamera.backgroundColor = new Color(0.035f, 0.045f, 0.065f, 1f);
            gameplayCamera.farClipPlane = Mathf.Max(gameplayCamera.farClipPlane, 320f);
        }

        if (localPlayer != null && !introStarted)
        {
            introStarted = true;
            localPlayer.SetMovementEnabled(false);
            if (localPlayer.Weapon != null) localPlayer.Weapon.enabled = false;
            if (localPlayer.Aim != null) localPlayer.Aim.enabled = false;
            BuildStageThreeGroundAliens();
            StartCoroutine(RunStageThreeIntro());
        }
    }

    private void Update()
    {
        if (!worldBuilt || isComplete)
            return;

        UpdateHovercrafts();
        if (IsIntroCinematic)
            return;

        UpdateUpperGuardPatrols();
        UpdateHovercraftShooting();
        UpdateStationaryGuardShooting();
        ApplyHovercraftThreat();

        // The center lift remains locked until all ground aliens and flying craft
        // are dead; entering the shaft early never starts the ascent.
        if (!liftSequenceStarted && HostilesCleared &&
            localPlayer != null && !localPlayer.IsDefeated)
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
        key.color = new Color(0.83f, 0.88f, 1f);
        key.intensity = 1.25f;
        key.shadows = LightShadows.None;
        RenderSettings.ambientLight = new Color(0.34f, 0.33f, 0.31f);
    }

    private void BuildGroundAndBoundary()
    {
        CreateBox("S3_SandyAlienGround", new Vector3(0f, -0.42f, 0f),
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
                    new Vector3(8.5f, 0.045f, 6.8f), CreateMaterial("S3_SandPlate", new Color(0.44f, 0.32f, 0.20f)), false,
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

        // The approach lane reaches the southern edge for the wide opening reveal.
        CreateBox("S3_ApproachLane", new Vector3(0f, 0.025f, -59f),
            new Vector3(16f, 0.06f, 80f), darkMetalMaterial, false);
        for (int i = 0; i < 17; i++)
        {
            float z = -100f + i * 4.7f;
            CreateBox("S3_ApproachGuideLeft", new Vector3(-5.7f, 0.08f, z),
                new Vector3(0.13f, 0.07f, 1.65f), cyanGlowMaterial, false);
            CreateBox("S3_ApproachGuideRight", new Vector3(5.7f, 0.08f, z),
                new Vector3(0.13f, 0.07f, 1.65f), cyanGlowMaterial, false);
        }
    }

    private void BuildAlienCity()
    {
        // A varied grid of tall silver towers; preserve a clear southern approach to the citadel.
        float[] coordinates = { -82f, -54f, -26f, 26f, 54f, 82f };
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

                float windowY = height * 0.48f;
                CreateBox("S3_TowerWindowSpine", new Vector3(x, windowY, z - depth * 0.5f - 0.045f),
                    new Vector3(width * 0.50f, Mathf.Min(height * 0.58f, 12f), 0.08f), windowMaterial, false,
                    Quaternion.Euler(0f, yaw, 0f));
                CreateBox("S3_TowerLightBand", new Vector3(x, height * 0.70f, z - depth * 0.5f - 0.075f),
                    new Vector3(width * 0.72f, 0.11f, 0.10f), cyanGlowMaterial, false,
                    Quaternion.Euler(0f, yaw, 0f));
                CreateBox("S3_TowerLowerWindow", new Vector3(x, height * 0.23f, z - depth * 0.5f - 0.045f),
                    new Vector3(width * 0.34f, Mathf.Min(height * 0.20f, 3.4f), 0.08f), windowMaterial, false,
                    Quaternion.Euler(0f, yaw, 0f));

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
            Quaternion gateRotation = Quaternion.LookRotation(radial, Vector3.up);

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
                Quaternion.Euler(0f, angle, 0f));

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 wallPosition = radial * 14.6f + tangent * side * 2.25f;
                CreateBox("S3_RouteWall_" + route, wallPosition + Vector3.up * 2.1f,
                    new Vector3(0.42f, 4.2f, 18.2f), silverMaterial, true,
                    Quaternion.Euler(0f, angle, 0f));
                for (int marker = 0; marker < 4; marker++)
                {
                    Vector3 mark = radial * (7.6f + marker * 4.2f) +
                        tangent * side * 2.03f + Vector3.up * 2.6f;
                    CreateBox("S3_RouteGuide_" + route, mark,
                        new Vector3(0.08f, 0.10f, 1.25f), cyanGlowMaterial, false,
                        Quaternion.Euler(0f, angle, 0f));
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
                Quaternion.LookRotation(radial, Vector3.up));
            CreateBox("S3_CitadelWallTrim_" + segment,
                new Vector3(center.x, 13.8f, center.z),
                new Vector3(13.0f, 0.35f, 2.45f), cyanGlowMaterial, false,
                Quaternion.LookRotation(radial, Vector3.up));
        }

        // The huge top platform is built in four pieces, leaving an open shaft above the lift.
        CreateBox("S3_TopPlatformWest", new Vector3(-13.5f, 24.65f, 0f),
            new Vector3(18f, 1.1f, 47f), silverMaterial, true);
        CreateBox("S3_TopPlatformEast", new Vector3(13.5f, 24.65f, 0f),
            new Vector3(18f, 1.1f, 47f), silverMaterial, true);
        CreateBox("S3_TopPlatformNorth", new Vector3(0f, 24.65f, 13.5f),
            new Vector3(9f, 1.1f, 20f), silverMaterial, true);
        CreateBox("S3_TopPlatformSouth", new Vector3(0f, 24.65f, -13.5f),
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
                new Vector3(0.20f, 12.6f, 0.20f), silverMaterial, false);
            CreateCylinder("S3_CylinderInnerGlow", radial * 3.82f + Vector3.up * 12.6f,
                new Vector3(0.07f, 12.6f, 0.07f), greenGlowMaterial, false);
        }

        // This is a lift pad, not a blocking pillar: ground stays walkable at its centre.
        // The ending cinematic moves the platform and the player explicitly.
        liftPlatform = CreateCylinder("S3_CentralLiftPlatform", new Vector3(0f, 0.28f, 0f),
            new Vector3(5.4f, 0.55f, 5.4f), silverMaterial, false).transform;
        // Keep the circular marking on the lift platform subdued instead of neon bright.
        CreateCylinder("S3_LiftPlatformGlow", new Vector3(0f, 0.49f, 0f),
            new Vector3(4.5f, 0.08f, 4.5f), darkMetalMaterial, false);

        CreateCylinder("S3_TopUplinkReceiver", new Vector3(0f, 26.1f, 0f),
            new Vector3(7.8f, 0.22f, 7.8f), greenGlowMaterial, false);

        // A large energy sphere sits above the deck. The final beam begins inside it
        // and erupts vertically beyond the top of the screen.
        uplinkOrb = CreateSphere("S3_UplinkOrb", new Vector3(0f, 29.5f, 0f),
            new Vector3(5.5f, 5.5f, 5.5f), uplinkOrbMaterial, false).transform;
        CreateCylinder("S3_UplinkOrbRing", new Vector3(0f, 29.5f, 0f),
            new Vector3(6.4f, 0.16f, 6.4f), silverMaterial, false);
        CreateSphere("S3_UplinkOrbCore", new Vector3(0f, 29.5f, 0f),
            new Vector3(2.35f, 2.35f, 2.35f), uplinkOrbMaterial, false);

        GameObject beam = CreateCylinder("S3_UplinkBeam", new Vector3(0f, 29.5f, 0f),
            new Vector3(1.55f, 0.1f, 1.55f), greenGlowMaterial, false);
        beamColumn = beam.transform;
        beamColumn.gameObject.SetActive(false);

        GameObject beamCore = CreateCylinder("S3_UplinkBeamCore", new Vector3(0f, 29.5f, 0f),
            new Vector3(0.42f, 0.1f, 0.42f), cyanGlowMaterial, false);
        beamCoreColumn = beamCore.transform;
        beamCoreColumn.gameObject.SetActive(false);
    }

    private void BuildHovercraftPatrols()
    {
        // Six ships use three distinct silhouettes. Each travels back and forth
        // along one fixed lane rather than orbiting or steering toward the player.
        // Six parallel street corridors never intersect. The old pair of north/south
        // lanes crossed all four east/west lanes, allowing ships to meet head-on.
        // Each row is offset inside an existing road gap and remains at firing height.
        float[] laneZ = { -72f, -44f, 44f, 64f, 72f };
        Vector3[] starts = new Vector3[laneZ.Length];
        Vector3[] ends = new Vector3[laneZ.Length];
        for (int i = 0; i < laneZ.Length; i++)
        {
            // Patrol endpoints lie beyond the map boundary. Ships disappear there,
            // then respawn just outside the opposite edge and fly back across the world.
            starts[i] = new Vector3(-118f, 0f, laneZ[i]);
            ends[i] = new Vector3(118f, 0f, laneZ[i]);
        }

        for (int i = 0; i < starts.Length; i++)
        {
            int model = i % 3;
            GameObject craft = new GameObject("S3_HostileHovercraft_" + model + "_" + i);
            craft.transform.SetParent(worldRoot, false);
            float height = 1.8f + (i % 3) * 0.18f;
            craft.transform.position = starts[i] + Vector3.up * height;
            BuildHovercraftModel(craft.transform, model);

            BoxCollider hitbox = craft.AddComponent<BoxCollider>();
            // Player bullets travel horizontally at muzzle height. The original
            // thin collider sat too high above that trajectory, so shots visibly
            // passed under the craft even though auto-aim had selected them.
            // Extend the hitbox downward while keeping it centered on the hull.
            hitbox.center = new Vector3(0f, -0.35f, 0f);
            hitbox.size = model == 2
                ? new Vector3(5.3f, 2.6f, 5.3f)
                : (model == 1 ? new Vector3(6.2f, 2.6f, 4.1f) : new Vector3(3.4f, 2.6f, 4.1f));

            TargetHealth health = craft.AddComponent<TargetHealth>();
            // The normal Stage 3 weapon projectile deals 22 damage: every
            // spacecraft therefore takes exactly four registered hits (88 HP).
            health.SetMaxHealth(88);
            EnemyChase chase = craft.AddComponent<EnemyChase>();
            chase.enabled = false;
            hostileHealth.Add(health);

            HovercraftPatrol patrol = new HovercraftPatrol
            {
                Root = craft.transform,
                StartPoint = starts[i],
                EndPoint = ends[i],
                Progress = (i % 2 == 0) ? 0f : 1f,
                TravelDirection = (i % 2 == 0) ? 1f : -1f,
                Speed = 8.0f + model * 1.35f,
                Height = height,
                NextShotTime = Time.time + 1.7f + i * 0.37f,
                NextDamageTime = Time.time + 1.0f,
                Archetype = model,
                Health = health,
                Heading = (ends[i] - starts[i]).normalized * ((i % 2 == 0) ? 1f : -1f)
            };
            hovercrafts.Add(patrol);
            craft.transform.rotation = Quaternion.LookRotation(patrol.Heading, Vector3.up);
        }
    }

    private void BuildHovercraftModel(Transform craft, int model)
    {
        if (model == 0)
        {
            // Scout: slim body, short wings and a bright sensor nose.
            CreateBoxChild(craft, "ScoutArmoredBody", Vector3.zero, new Vector3(1.8f, 0.62f, 3.0f), darkMetalMaterial);
            CreateBoxChild(craft, "ScoutBlueHull", new Vector3(0f, 0.25f, 0.15f), new Vector3(1.25f, 0.22f, 2.45f), silverMaterial);
            CreateBoxChild(craft, "ScoutLeftWing", new Vector3(-1.35f, -0.04f, -0.2f), new Vector3(1.15f, 0.16f, 1.5f), silverMaterial);
            CreateBoxChild(craft, "ScoutRightWing", new Vector3(1.35f, -0.04f, -0.2f), new Vector3(1.15f, 0.16f, 1.5f), silverMaterial);
            CreateSphereChild(craft, "ScoutSensor", new Vector3(0f, 0.27f, 1.18f), new Vector3(0.42f, 0.30f, 0.42f), cyanGlowMaterial);
        }
        else if (model == 1)
        {
            // Interceptor: broader wings and two obvious gun pods.
            CreateBoxChild(craft, "InterceptorBody", Vector3.zero, new Vector3(1.55f, 0.72f, 3.4f), darkMetalMaterial);
            CreateBoxChild(craft, "InterceptorCenterSpine", new Vector3(0f, 0.36f, 0.0f), new Vector3(0.88f, 0.26f, 2.7f), silverMaterial);
            CreateBoxChild(craft, "InterceptorLeftWing", new Vector3(-2.05f, -0.07f, -0.35f), new Vector3(2.1f, 0.20f, 1.8f), silverMaterial);
            CreateBoxChild(craft, "InterceptorRightWing", new Vector3(2.05f, -0.07f, -0.35f), new Vector3(2.1f, 0.20f, 1.8f), silverMaterial);
            CreateBoxChild(craft, "InterceptorLeftCannon", new Vector3(-0.82f, -0.02f, 1.65f), new Vector3(0.24f, 0.22f, 0.95f), warningMaterial);
            CreateBoxChild(craft, "InterceptorRightCannon", new Vector3(0.82f, -0.02f, 1.65f), new Vector3(0.24f, 0.22f, 0.95f), warningMaterial);
        }
        else
        {
            // Heavy saucer: circular chassis, raised cockpit and engine pods.
            CreateCylinderChild(craft, "HeavySaucerDisc", new Vector3(0f, -0.06f, 0f), new Vector3(5.0f, 0.68f, 5.0f), silverMaterial);
            CreateCylinderChild(craft, "HeavySaucerDarkCore", new Vector3(0f, 0.34f, 0f), new Vector3(2.5f, 0.62f, 2.5f), darkMetalMaterial);
            CreateSphereChild(craft, "HeavySaucerCockpit", new Vector3(0f, 0.55f, 0.18f), new Vector3(1.55f, 0.95f, 1.55f), cyanGlowMaterial);
            for (int side = -1; side <= 1; side += 2)
                CreateCylinderChild(craft, "HeavySaucerEngine", new Vector3(side * 1.8f, -0.24f, -1.0f),
                    new Vector3(0.52f, 0.36f, 0.52f), greenGlowMaterial);
        }

        CreateBoxChild(craft, "CraftForwardLight", new Vector3(0f, 0.15f, 1.78f),
            new Vector3(model == 2 ? 0.9f : 0.52f, 0.12f, 0.16f), cyanGlowMaterial);
        for (int side = -1; side <= 1; side += 2)
            CreateCylinderChild(craft, "CraftThruster", new Vector3(side * (model == 2 ? 1.9f : 1.0f), -0.38f, -1.05f),
                new Vector3(0.42f, 0.24f, 0.42f), greenGlowMaterial);
    }

    private void UpdateHovercrafts()
    {
        for (int i = 0; i < hovercrafts.Count; i++)
        {
            HovercraftPatrol patrol = hovercrafts[i];
            if (patrol.Root == null || patrol.Health == null)
                continue;
            if (patrol.Hidden)
            {
                if (Time.time >= patrol.ReappearAt && patrol.Health.CurrentHealth > 0)
                {
                    patrol.Progress = patrol.TravelDirection > 0f ? 0f : 1f;
                    patrol.Root.gameObject.SetActive(true);
                    patrol.Hidden = false;
                }
                else
                    continue;
            }
            if (patrol.Health.CurrentHealth <= 0)
            {
                if (!patrol.ExplosionStarted)
                {
                    patrol.ExplosionStarted = true;
                    SpawnHovercraftExplosion(patrol.Root.position);

                    // Keep the destroyed saucer as a grounded wreck instead of
                    // deleting it immediately. Disable collision so the wreck
                    // cannot block the player or receive more shots.
                    Collider[] wreckColliders = patrol.Root.GetComponentsInChildren<Collider>(true);
                    for (int c = 0; c < wreckColliders.Length; c++)
                        if (wreckColliders[c] != null)
                            wreckColliders[c].enabled = false;

                    Vector3 wreckPosition = patrol.Root.position;
                    wreckPosition.y = 0.22f;
                    patrol.Root.position = wreckPosition;
                    patrol.Root.rotation = patrol.Root.rotation * Quaternion.Euler(72f, 0f, 18f);
                    patrol.Root.name = "S3_DestroyedHovercraftWreck";
                    // Keep the wreck visible on the ground for three seconds, then clear it.
                    Destroy(patrol.Root.gameObject, 3f);
                }
                continue;
            }

            float length = Vector3.Distance(patrol.StartPoint, patrol.EndPoint);
            if (length > 0.01f)
            {
                patrol.Progress += patrol.TravelDirection * patrol.Speed * Time.deltaTime / length;
                if (patrol.Progress >= 1f || patrol.Progress <= 0f)
                {
                    // Do not turn around at the map edge: leave the playable map,
                    // vanish briefly, then re-enter from the opposite edge.
                    patrol.Progress = Mathf.Clamp01(patrol.Progress);
                    patrol.Hidden = true;
                    patrol.ReappearAt = Time.time + 1.15f;
                    patrol.Root.gameObject.SetActive(false);
                    continue;
                }
            }

            Vector3 point = Vector3.Lerp(patrol.StartPoint, patrol.EndPoint, patrol.Progress);
            point.y = patrol.Height + Mathf.Sin(Time.time * 1.8f + patrol.Progress * 6.28f) * 0.09f;
            patrol.Root.position = point;
            patrol.Heading = (patrol.EndPoint - patrol.StartPoint).normalized * patrol.TravelDirection;
            patrol.Root.rotation = Quaternion.LookRotation(patrol.Heading, Vector3.up);
        }
    }

    private void SpawnHovercraftExplosion(Vector3 position)
    {
        GameObject burst = new GameObject("S3_HovercraftExplosion");
        burst.transform.SetParent(worldRoot, true);
        burst.transform.position = position;
        ParticleSystem particles = burst.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.duration = 0.42f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.25f, 0.95f, 1f, 1f), new Color(0.35f, 1f, 0.68f, 0.95f));
        main.maxParticles = 36;
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.38f;
        var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.material = RuntimeMaterialFactory.Create(
            "S3_HovercraftExplosionMaterial", new Color(0.25f, 0.95f, 1f));
        particles.Play();
        Destroy(burst, 1.2f);
        StartupCheckpoint.Set("StageThreeHovercraftExploded");
    }

    private void UpdateHovercraftShooting()
    {
        for (int i = 0; i < hovercrafts.Count; i++)
        {
            HovercraftPatrol craft = hovercrafts[i];
            if (craft.Root == null || craft.Hidden || craft.Health == null || craft.Health.CurrentHealth <= 0 ||
                Time.time < craft.NextShotTime)
                continue;

            // Hostile transports lead their shots toward the pilot instead of
            // firing harmlessly along the patrol lane.
            Vector3 origin = craft.Root.position + craft.Heading * 1.7f + Vector3.up * 0.12f;
            Vector3 direction = localPlayer.transform.position + Vector3.up * 0.88f - origin;
            float distance = direction.magnitude;
            // Craft fire is a mid-range threat; it must not snipe the player
            // from the opposite side of the map.
            if (distance < 0.01f || distance > 28f)
            {
                craft.NextShotTime = Time.time + 0.9f;
                continue;
            }
            CreateHostileProjectile(origin, direction.normalized, craft.Root, 6, 30f, warningMaterial);
            craft.NextShotTime = Time.time + 3.0f + craft.Archetype * 0.35f;
            StartupCheckpoint.Set("StageThreeHovercraftFiredAtPilot");
        }
    }

    private void ApplyHovercraftThreat()
    {
        if (localPlayer == null || localPlayer.IsDefeated || liftSequenceStarted)
            return;

        Vector3 playerPosition = localPlayer.transform.position;
        for (int i = 0; i < hovercrafts.Count; i++)
        {
            HovercraftPatrol craft = hovercrafts[i];
            if (craft.Root == null || craft.Hidden || craft.Health == null || craft.Health.CurrentHealth <= 0)
                continue;

            Vector2 playerFlat = new Vector2(playerPosition.x, playerPosition.z);
            Vector2 craftFlat = new Vector2(craft.Root.position.x, craft.Root.position.z);
            if (Vector2.Distance(playerFlat, craftFlat) > 2.7f ||
                Mathf.Abs(playerPosition.y - craft.Root.position.y) > 2.5f ||
                Time.time < craft.NextDamageTime)
                continue;

            // Collision damage is a close-range scrape, not a one-touch kill.
            localPlayer.ReceiveDamage(7);
            craft.NextDamageTime = Time.time + 1.8f;
            StartupCheckpoint.Set("StageThreeHovercraftThreatHit");
        }
    }

    private void BuildStageThreeGroundAliens()
    {
        if (groundAliensSpawned || localPlayer == null)
            return;
        groundAliensSpawned = true;

        List<Vector3> occupied = new List<Vector3>();
        // Twelve regular sentries stand at perimeter posts around the fortress.
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 360f / 12f;
            Vector3 position = DirectionAt(angle) * (34.5f + (i % 2) * 4.5f);
            position.y = 0f;
            occupied.Add(position);
            SpawnGroundAlien(i, position, true);
        }

        // The rest are spread through open streets and outer ground, avoiding building footprints.
        int spawned = 12;
        const int groundRosterBeforeTallGuards = 40;
        for (int attempt = 0; attempt < 800 && spawned < groundRosterBeforeTallGuards; attempt++)
        {
            float angle = attempt * 137.50776f;
            float radius = 43f + (attempt % 6) * 9.2f;
            Vector3 position = DirectionAt(angle) * radius;
            position.y = 0f;

            if (Mathf.Abs(position.x) < 9.5f && position.z < -22f)
                continue;
            if (IsNearAlienTower(position, 9f))
                continue;

            bool tooClose = false;
            for (int i = 0; i < occupied.Count; i++)
            {
                if (Vector3.Distance(occupied[i], position) < 7.2f)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose)
                continue;

            occupied.Add(position);
            SpawnGroundAlien(spawned, position, false);
            spawned++;
        }

        // Deterministic fallback completes the first 40 aliens before the 20 tall guards.
        for (int attempt = 0; spawned < groundRosterBeforeTallGuards && attempt < 120; attempt++)
        {
            float angle = attempt * 31.7f;
            float radius = 74f + (attempt % 3) * 5f;
            Vector3 position = DirectionAt(angle) * radius;
            position.y = 0f;
            occupied.Add(position);
            SpawnGroundAlien(spawned, position, false);
            spawned++;
        }

        // Twenty tall alien guards bring the ground roster to exactly 60.
        // Each has its own 20m patrol area and pursues the player only while nearby.
        int upperPatrols = 0;
        for (float z = 42f; z <= 102f && upperPatrols < 20; z += 8f)
        {
            for (float x = -92f; x <= 92f && upperPatrols < 20; x += 8f)
            {
                Vector3 position = new Vector3(x, 0f, z);
                if (new Vector2(position.x, position.z).magnitude < 36f ||
                    IsPatrolLaneBlockedByAlienTower(position, 10f))
                    continue;

                bool tooClose = false;
                for (int i = 0; i < occupied.Count; i++)
                {
                    if (Vector3.Distance(occupied[i], position) < 10.5f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                    continue;

                occupied.Add(position);
                int guardIndex = 40 + upperPatrols;
                SpawnGroundAlien(guardIndex, position, true);
                GroundAlien guard = groundAliens[groundAliens.Count - 1];
                guard.UpperPatrol = true;
                guard.PatrolCenter = position;
                guard.PatrolRadius = 10f;
                guard.PatrolPhase = upperPatrols * 0.83f;
                guard.PatrolSpeed = 0.62f + (upperPatrols % 4) * 0.08f;
                guard.NextShotTime = Time.time + 1.0f + (upperPatrols % 6) * 0.18f;
                guard.Health.SetMaxHealth(guard.Archetype == 3 ? 250 : (guard.Archetype == 2 ? 210 : 180));
                CreateBoxChild(guard.Root, "S3_EliteHeavyRifle", new Vector3(0.48f, 0.10f, 0.48f),
                    new Vector3(0.16f, 0.15f, 0.90f), darkMetalMaterial);
                CreateBoxChild(guard.Root, "S3_EliteHeavyRifleBarrel", new Vector3(0.48f, 0.12f, 0.94f),
                    new Vector3(0.10f, 0.10f, 0.46f), greenGlowMaterial);
                upperPatrols++;
            }
        }

        // Safe fallback for unusual map layouts: place any missing elite guards
        // along the open northern approach, still separated from the other roster.
        for (int attempt = 0; upperPatrols < 20 && attempt < 60; attempt++)
        {
            Vector3 position = new Vector3(-88f + (attempt % 18) * 9.5f, 0f, 96f - (attempt / 18) * 8f);
            if (IsPatrolLaneBlockedByAlienTower(position, 10f))
                continue;
            bool fallbackTooClose = false;
            for (int i = 0; i < occupied.Count; i++)
            {
                if (Vector3.Distance(occupied[i], position) < 10.5f)
                {
                    fallbackTooClose = true;
                    break;
                }
            }
            if (fallbackTooClose)
                continue;
            occupied.Add(position);
            int guardIndex = 40 + upperPatrols;
            SpawnGroundAlien(guardIndex, position, true);
            GroundAlien guard = groundAliens[groundAliens.Count - 1];
            guard.UpperPatrol = true;
            guard.PatrolCenter = position;
            guard.PatrolRadius = 10f;
            guard.PatrolPhase = upperPatrols * 0.83f;
            guard.PatrolSpeed = 0.62f + (upperPatrols % 4) * 0.08f;
            guard.NextShotTime = Time.time + 1.0f + (upperPatrols % 6) * 0.18f;
            guard.Health.SetMaxHealth(guard.Archetype == 3 ? 250 : (guard.Archetype == 2 ? 210 : 180));
            CreateBoxChild(guard.Root, "S3_EliteHeavyRifle", new Vector3(0.48f, 0.10f, 0.48f),
                new Vector3(0.16f, 0.15f, 0.90f), darkMetalMaterial);
            CreateBoxChild(guard.Root, "S3_EliteHeavyRifleBarrel", new Vector3(0.48f, 0.12f, 0.94f),
                new Vector3(0.10f, 0.10f, 0.46f), greenGlowMaterial);
            upperPatrols++;
        }

        Debug.Log("PERSIA_STAGE3: spawned " + groundAliens.Count +
            " ground aliens total (40 regular aliens + 20 tall area-patrol guards).");
        StartupCheckpoint.Set("StageThree_60GroundAliensSpawned");
    }

    private bool IsNearAlienTower(Vector3 position, float padding)
    {
        float[] coordinates = { -82f, -54f, -26f, 26f, 54f, 82f };
        for (int ix = 0; ix < coordinates.Length; ix++)
        {
            for (int iz = 0; iz < coordinates.Length; iz++)
            {
                float x = coordinates[ix];
                float z = coordinates[iz];
                if (new Vector2(x, z).magnitude < 37f)
                    continue;
                float safeDistance = 5.8f + padding * 0.15f;
                if (Mathf.Abs(position.x - x) < safeDistance &&
                    Mathf.Abs(position.z - z) < safeDistance)
                    return true;
            }
        }
        return false;
    }

    // Guards patrol ten metres in either direction along X. A spawn can look clear
    // while its full patrol lane crosses a tower, so validate the entire swept lane.
    private bool IsPatrolLaneBlockedByAlienTower(Vector3 patrolCenter, float patrolRadius)
    {
        float[] coordinates = { -82f, -54f, -26f, 26f, 54f, 82f };
        const float towerHalfFootprint = 5.6f;
        const float guardRadius = 0.55f;
        for (int ix = 0; ix < coordinates.Length; ix++)
        {
            for (int iz = 0; iz < coordinates.Length; iz++)
            {
                float x = coordinates[ix];
                float z = coordinates[iz];
                if (new Vector2(x, z).magnitude < 37f)
                    continue;
                bool overlapsTowerDepth = Mathf.Abs(patrolCenter.z - z) <
                    towerHalfFootprint + guardRadius;
                bool patrolSweepsAcrossTower = Mathf.Abs(patrolCenter.x - x) <
                    towerHalfFootprint + guardRadius + patrolRadius;
                if (overlapsTowerDepth && patrolSweepsAcrossTower)
                    return true;
            }
        }
        return false;
    }

    private void SpawnGroundAlien(int index, Vector3 position, bool guard)
    {
        int archetype = index % 3 + 1;
        GameObject alien;
#if UNITY_ANDROID
        alien = new GameObject((guard ? "S3_AlienGuard_" : "S3_AlienRover_") + index.ToString("00"));
        alien.transform.position = position;
        alien.transform.localScale = GetAlienModelScale(archetype);
        CapsuleCollider capsule = alien.AddComponent<CapsuleCollider>();
        capsule.radius = 0.55f;
        capsule.height = 1.8f;
        capsule.center = new Vector3(0f, 0.9f, 0f);

        GameObject meshSource = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        MeshFilter sourceFilter = meshSource.GetComponent<MeshFilter>();
        MeshFilter alienFilter = alien.AddComponent<MeshFilter>();
        alienFilter.sharedMesh = sourceFilter.sharedMesh;
        MeshRenderer alienRenderer = alien.AddComponent<MeshRenderer>();
        alienRenderer.sharedMaterial = alienBodyMaterials[archetype - 1];
        alienRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        alienRenderer.receiveShadows = false;
        Collider temporaryCollider = meshSource.GetComponent<Collider>();
        if (temporaryCollider != null) Destroy(temporaryCollider);
        Renderer temporaryRenderer = meshSource.GetComponent<Renderer>();
        if (temporaryRenderer != null) temporaryRenderer.enabled = false;
        Destroy(meshSource);
#else
        alien = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        alien.name = (guard ? "S3_AlienGuard_" : "S3_AlienRover_") + index.ToString("00");
        alien.transform.position = position;
        alien.transform.localScale = GetAlienModelScale(archetype);
        Renderer alienRenderer = alien.GetComponent<Renderer>();
        if (alienRenderer != null)
        {
            alienRenderer.sharedMaterial = alienBodyMaterials[archetype - 1];
        }
#endif
        alien.transform.SetParent(worldRoot, true);
        bool eliteGuard = guard && index >= 40;
        if (eliteGuard)
            alien.transform.localScale = new Vector3(1.12f, 1.62f, 1.12f);
        BuildAlienCreatureDetails(alien.transform, archetype, guard, eliteGuard);

        TargetHealth health = alien.AddComponent<TargetHealth>();
        health.SetMaxHealth(archetype == 3 ? 150 : (archetype == 2 ? 115 : 90));
        // Stage 3 already shows a live total-hostile counter. Avoid spawning 80
        // independent IMGUI health bars/raycast loops on mobile; aim still reads TargetHealth.
        EnemyChase chase = alien.AddComponent<EnemyChase>();
        if (guard)
        {
            chase.SetTarget(localPlayer.transform);
            chase.enabled = false;
        }
        else
        {
            chase.Configure(localPlayer.transform, archetype);
            // Enemy movement and aiming begin only after the wide intro completes.
            chase.enabled = false;
        }

        GroundAlien data = new GroundAlien
        {
            Root = alien.transform,
            Health = health,
            Chase = chase,
            Guard = guard,
            Archetype = archetype,
            NextShotTime = Time.time + 1.4f + (index % 5) * 0.35f
        };
        groundAliens.Add(data);
        hostileHealth.Add(health);
    }

    private static Vector3 GetAlienModelScale(int archetype)
    {
        // Three clearly readable alien silhouettes: slender scout, broad brute,
        // and compact armored hunter. Elite guards receive their own tall override.
        switch (archetype)
        {
            case 1: return new Vector3(0.78f, 1.10f, 0.80f);
            case 2: return new Vector3(1.12f, 0.88f, 1.08f);
            default: return new Vector3(0.94f, 1.02f, 0.78f);
        }
    }

    private void BuildAlienCreatureDetails(Transform alien, int archetype, bool guard, bool eliteGuard)
    {
        Material chitin = alienBodyMaterials[Mathf.Clamp(archetype - 1, 0, alienBodyMaterials.Length - 1)];
        Material eyes = archetype == 1 ? cyanGlowMaterial
            : (archetype == 2 ? greenGlowMaterial : warningMaterial);

        // Tall guards have an elongated alien skull and broader, more insectoid armor.
        float tallScale = eliteGuard ? 1.22f : 1f;
        CreateSphereChild(alien, "S3_AlienCranium", new Vector3(0f, 0.76f * tallScale, 0.16f),
            new Vector3(0.78f, 0.62f, 0.62f), chitin);
        CreateSphereChild(alien, "S3_AlienJaw", new Vector3(0f, 0.36f, 0.38f),
            new Vector3(0.42f, 0.19f, 0.30f), darkMetalMaterial);
        CreateBoxChild(alien, "S3_AlienChestArmor", new Vector3(0f, 0.05f, 0.35f),
            new Vector3(0.46f, 0.34f, 0.18f), darkMetalMaterial);
        CreateSphereChild(alien, "S3_AlienBackEnergyNode", new Vector3(0f, 0.46f, -0.38f),
            new Vector3(0.24f, 0.30f, 0.20f), greenGlowMaterial);
        // A segmented insect abdomen and six jointed legs make the silhouette
        // unmistakably extraterrestrial instead of a human wearing armor.
        CreateSphereChild(alien, "S3_AlienSegmentedAbdomen", new Vector3(0f, -0.12f, -0.46f),
            new Vector3(0.48f, 0.38f, 0.62f), chitin);
        CreateSphereChild(alien, "S3_AlienForeheadCrest", new Vector3(0f, 1.02f * tallScale, 0.23f),
            new Vector3(0.28f, 0.22f, 0.30f), silverMaterial);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int leg = 0; leg < 3; leg++)
            {
                float legZ = 0.24f - leg * 0.31f;
                CreateCylinderChild(alien, "S3_AlienInsectLeg",
                    new Vector3(side * (0.43f + (leg == 1 ? 0.10f : 0f)), -0.36f, legZ),
                    new Vector3(0.10f, 0.34f, 0.10f), chitin);
                CreateSphereChild(alien, "S3_AlienLegJoint",
                    new Vector3(side * 0.53f, -0.43f, legZ),
                    new Vector3(0.13f, 0.13f, 0.13f), eyes);
            }
            CreateSphereChild(alien, "S3_AlienShoulderPlate",
                new Vector3(side * 0.39f, 0.16f, 0.02f),
                new Vector3(0.30f, 0.23f, 0.34f), silverMaterial);
            CreateBoxChild(alien, "S3_AlienMandible",
                new Vector3(side * 0.16f, 0.30f, 0.55f),
                new Vector3(0.10f, 0.20f, 0.12f), silverMaterial);
            CreateCylinderChild(alien, "S3_AlienAntenna",
                new Vector3(side * 0.24f, 1.10f, 0.13f),
                new Vector3(0.07f, 0.29f, 0.07f), darkMetalMaterial);
            CreateSphereChild(alien, "S3_AlienEye",
                new Vector3(side * 0.19f, 0.69f, 0.47f),
                new Vector3(0.13f, 0.13f, 0.08f), eyes);
        }
        CreateSphereChild(alien, "S3_AlienThirdEye", new Vector3(0f, 0.82f, 0.48f),
            new Vector3(0.12f, 0.12f, 0.08f), eyes);

        if (guard)
        {
            CreateBoxChild(alien, "S3_AlienSentryBackpack", new Vector3(0f, 0.43f, -0.34f),
                new Vector3(0.38f, 0.50f, 0.20f), silverMaterial);
            if (!eliteGuard)
            {
                CreateBoxChild(alien, "S3_AlienEnergyRifle", new Vector3(0.47f, 0.06f, 0.45f),
                    new Vector3(0.10f, 0.10f, 0.44f), darkMetalMaterial);
                CreateSphereChild(alien, "S3_AlienRifleEmitter", new Vector3(0.47f, 0.07f, 0.70f),
                    new Vector3(0.07f, 0.07f, 0.07f), eyes);
            }
        }
        else
        {
            CreateBoxChild(alien, "S3_AlienRoverSpine", new Vector3(0f, 0.28f, -0.36f),
                new Vector3(0.26f, 0.28f, 0.20f), silverMaterial);
        }
    }

    private void UpdateUpperGuardPatrols()
    {
        for (int i = 0; i < groundAliens.Count; i++)
        {
            GroundAlien alien = groundAliens[i];
            if (!alien.UpperPatrol || alien.Root == null || alien.Health == null ||
                alien.Health.CurrentHealth <= 0)
                continue;

            float phase = Time.time * alien.PatrolSpeed + alien.PatrolPhase;
            Vector3 patrolPoint = alien.PatrolCenter +
                Vector3.right * (Mathf.Sin(phase) * alien.PatrolRadius);
            Vector3 position = patrolPoint;
            Vector3 toPlayer = localPlayer != null
                ? localPlayer.transform.position - alien.Root.position
                : Vector3.zero;
            toPlayer.y = 0f;

            // Tall guards hunt only inside their own assigned patrol circle.
            // If the player comes within 16m, pursue them but never leave that zone.
            if (toPlayer.sqrMagnitude <= 16f * 16f)
            {
                Vector3 pursued = Vector3.MoveTowards(alien.Root.position,
                    new Vector3(localPlayer.transform.position.x, 0f, localPlayer.transform.position.z),
                    alien.PatrolSpeed * 2.2f * Time.deltaTime);
                Vector3 offset = pursued - alien.PatrolCenter;
                offset.y = 0f;
                if (offset.magnitude > alien.PatrolRadius)
                    pursued = alien.PatrolCenter + offset.normalized * alien.PatrolRadius;
                position = pursued;
            }

            position.y = 0f;
            alien.Root.position = position;

            Vector3 facing = position - alien.PatrolCenter;
            if (toPlayer.sqrMagnitude > 0.01f)
                facing = toPlayer;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f)
                alien.Root.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
        }
    }

    private void UpdateStationaryGuardShooting()
    {
        if (localPlayer == null || localPlayer.IsDefeated)
            return;

        for (int i = 0; i < groundAliens.Count; i++)
        {
            GroundAlien alien = groundAliens[i];
            if (!alien.Guard || alien.Root == null || alien.Health == null ||
                alien.Health.CurrentHealth <= 0 || Time.time < alien.NextShotTime)
                continue;

            Vector3 origin = alien.Root.position + Vector3.up * 1.0f +
                alien.Root.forward * 0.22f;
            // Aim at the player's upper body in 3D. Flattening this vector made
            // bolts skim beneath/over the player on sloped ground and ramps.
            Vector3 direction = localPlayer.transform.position + Vector3.up * 0.88f - origin;
            if (direction.sqrMagnitude < 0.01f)
                continue;
            float distance = direction.magnitude;
            // Keep alien fire within a readable combat distance. The old 62-82m
            // ranges let multiple squads focus-fire the player from across the map.
            float maxRange = alien.UpperPatrol ? 24f : 18f;
            if (distance > maxRange)
            {
                alien.NextShotTime = Time.time + 0.7f;
                continue;
            }

            if (alien.UpperPatrol)
            {
                // Elite guards still hit harder, but slower and only at close-mid range.
                CreateHostileProjectile(origin, direction.normalized, alien.Root, 6, 27f, greenGlowMaterial);
                alien.NextShotTime = Time.time + 3.0f + (alien.Archetype - 1) * 0.35f;
            }
            else
            {
                // Standard sentries are a low-pressure threat rather than a long-range sniper.
                CreateHostileProjectile(origin, direction.normalized, alien.Root, 4, 20f, warningMaterial);
                alien.NextShotTime = Time.time + 3.4f + (alien.Archetype - 1) * 0.45f;
            }
        }
    }

    private void CreateHostileProjectile(Vector3 origin, Vector3 direction, Transform owner,
        int damage, float range, Material projectileMaterial)
    {
        if (direction.sqrMagnitude < 0.001f)
            return;
        direction.Normalize();
        GameObject projectile;
#if UNITY_ANDROID
        projectile = AndroidSafeRuntimeFactory.CreateProjectile(
            "S3_HostileBolt", origin, Quaternion.LookRotation(direction, Vector3.up),
            new Vector3(0.075f, 0.075f, 0.075f), new Color(1f, 0.20f, 0.10f));
        if (projectile == null)
            return;
#else
        projectile = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        projectile.name = "S3_HostileBolt";
        projectile.transform.position = origin;
        projectile.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        projectile.transform.localScale = new Vector3(0.075f, 0.26f, 0.075f);
        SphereCollider sphere = projectile.GetComponent<SphereCollider>();
        if (sphere != null) Destroy(sphere);
        CapsuleCollider capsule = projectile.GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.isTrigger = true;
            capsule.radius = 0.5f;
            capsule.height = 2f;
            capsule.direction = 1;
        }
        Rigidbody body = projectile.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = projectileMaterial;
#endif
        EnemyProjectile enemyShot = projectile.AddComponent<EnemyProjectile>();
        enemyShot.Configure(direction, damage, owner, range);
    }

    private Transform PrepareStageThreeTransportShip()
    {
        // Prefer the exact ship object used by the Stage 2 ending. This preserves
        // the same hull, engine layout, cabin, ramp, and materials across stages.
        Transform ship = StageTwoVictorySequence.TakeRetainedShipForStageThree();
        if (ship == null)
        {
            // Direct Stage 3 test entry has no Stage 2 cutscene instance to reuse.
            // Build a geometry-for-geometry match of StageTwoVictorySequence.BuildShip,
            // not the older simplified look-alike.
            ship = new GameObject("StageThreeEntryTransportShip").transform;
            Material stageTwoHull = CreateMaterial("S3_TransportHull_MatchStageTwo",
                new Color(0.62f, 0.72f, 0.80f));
            Material stageTwoDark = CreateMaterial("S3_TransportDark_MatchStageTwo",
                new Color(0.075f, 0.14f, 0.22f));
            Color engineColor = new Color(0.32f, 1f, 0.47f);
            Material stageTwoEngine = CreateMaterial("S3_TransportEngineGlow_MatchStageTwo",
                engineColor, engineColor);

            // Twin rounded hulls and twin keels leave the open central passage visible.
            CreateSphereChild(ship, "PortHull", new Vector3(-6.5f, 0f, 0f),
                new Vector3(8.8f, 1.45f, 9.0f), stageTwoHull);
            CreateSphereChild(ship, "StarboardHull", new Vector3(6.5f, 0f, 0f),
                new Vector3(8.8f, 1.45f, 9.0f), stageTwoHull);
            CreateBoxChild(ship, "PortKeel", new Vector3(-3.8f, -0.58f, -0.15f),
                new Vector3(5.6f, 0.36f, 6.1f), stageTwoDark);
            CreateBoxChild(ship, "StarboardKeel", new Vector3(3.8f, -0.58f, -0.15f),
                new Vector3(5.6f, 0.36f, 6.1f), stageTwoDark);

            // Match the same open-stern passenger cabin and rear ramp from Stage 2.
            CreateBoxChild(ship, "PassengerCabinFloor", new Vector3(0f, -0.72f, -1.85f),
                new Vector3(3.35f, 0.14f, 4.65f), stageTwoDark);
            CreateBoxChild(ship, "PassengerCabinPortWall", new Vector3(-1.72f, 0.12f, -1.85f),
                new Vector3(0.18f, 1.72f, 4.65f), stageTwoDark);
            CreateBoxChild(ship, "PassengerCabinStarboardWall", new Vector3(1.72f, 0.12f, -1.85f),
                new Vector3(0.18f, 1.72f, 4.65f), stageTwoDark);
            CreateBoxChild(ship, "PassengerCabinForwardBulkhead", new Vector3(0f, 0.12f, 0.43f),
                new Vector3(3.35f, 1.72f, 0.18f), stageTwoDark);
            CreateBoxChild(ship, "RearEntryRamp", new Vector3(0f, -0.72f, -5.20f),
                new Vector3(3.8f, 0.14f, 2.65f), stageTwoDark);

            CreateBoxChild(ship, "PortDeck", new Vector3(-4.35f, 0.48f, 0.15f),
                new Vector3(3.1f, 0.34f, 4.6f), stageTwoHull);
            CreateBoxChild(ship, "StarboardDeck", new Vector3(4.35f, 0.48f, 0.15f),
                new Vector3(3.1f, 0.34f, 4.6f), stageTwoHull);
            CreateSphereChild(ship, "CommandDome", new Vector3(0f, 0.92f, 1.0f),
                new Vector3(5.4f, 1.15f, 3.5f), stageTwoHull);
            CreateBoxChild(ship, "CentralSpine", new Vector3(0f, 0.90f, 2.0f),
                new Vector3(7.2f, 0.52f, 1.2f), stageTwoDark);
            CreateBoxChild(ship, "PortWing", new Vector3(-7.2f, -0.12f, 0.4f),
                new Vector3(5.3f, 0.52f, 2.4f), stageTwoDark);
            CreateBoxChild(ship, "StarboardWing", new Vector3(7.2f, -0.12f, 0.4f),
                new Vector3(5.3f, 0.52f, 2.4f), stageTwoDark);
            CreateSphereChild(ship, "FrontNose", new Vector3(0f, -0.02f, 4.0f),
                new Vector3(5.4f, 0.9f, 2.5f), stageTwoHull);

            // Six engine glows and fourteen deck lights match the Stage 2 silhouette.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int engine = 0; engine < 3; engine++)
                {
                    float z = -3.0f + engine * 2.2f;
                    CreateSphereChild(ship, "EngineGlow",
                        new Vector3(side * (5.0f + (engine == 1 ? 1.2f : 0f)), -0.20f, z),
                        new Vector3(1.0f, 0.34f, 1.0f), stageTwoEngine);
                }

                for (int window = -3; window <= 3; window++)
                {
                    float z = window * 1.05f;
                    CreateBoxChild(ship, "DeckLight",
                        new Vector3(side * 4.35f, 0.76f, z),
                        new Vector3(0.16f, 0.12f, 0.42f), stageTwoEngine);
                }
            }
        }

        ship.name = "StageThreeEntryTransportShip";
        ship.SetParent(worldRoot, true);
        ship.localScale = Vector3.one;
        ship.gameObject.SetActive(true);
        return ship;
    }

    private IEnumerator RunStageThreeIntro()
    {
        IsCinematic = true;
        IsIntroCinematic = true;
        IntroMessage = "TRANSPORT SHIP INBOUND — SCANNING THE PLANET";
        StartupCheckpoint.Set("StageThreeIntroStarted_TransportArrival");

        if (localPlayer != null)
        {
            localPlayer.SetMovementEnabled(false);
            if (localPlayer.Weapon != null) localPlayer.Weapon.enabled = false;
            if (localPlayer.Aim != null) localPlayer.Aim.enabled = false;
        }

        if (gameplayCamera == null || localPlayer == null)
        {
            IsIntroCinematic = false;
            IsCinematic = false;
            yield break;
        }

        Transform pilot = localPlayer.transform;
        Transform originalPlayerParent = pilot.parent;
        Vector3 originalPlayerLocalScale = pilot.localScale;
        Renderer[] pilotRenderers = pilot.GetComponentsInChildren<Renderer>(true);
        bool[] pilotRendererWasEnabled = new bool[pilotRenderers.Length];
        for (int i = 0; i < pilotRenderers.Length; i++)
        {
            pilotRendererWasEnabled[i] = pilotRenderers[i] != null && pilotRenderers[i].enabled;
            if (pilotRenderers[i] != null)
                pilotRenderers[i].enabled = false;
        }

        Vector3 selectedLandingPoint = pilot.position;
        selectedLandingPoint.y = 0f;
        selectedLandingPoint.x = Mathf.Clamp(selectedLandingPoint.x, -92f, 92f);
        selectedLandingPoint.z = Mathf.Clamp(selectedLandingPoint.z, -98f, 98f);

        entryTransportShip = PrepareStageThreeTransportShip();
        Quaternion shipRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        Vector3 shipLandingPosition = selectedLandingPoint + Vector3.forward * 9f;
        shipLandingPosition.y = 0.92f;
        // Bring the ship down from a nearby, readable height instead of starting
        // with the player-sized city-wide aerial shot.
        Vector3 shipStartPosition = shipLandingPosition + Vector3.up * 44f + Vector3.back * 6f;
        entryTransportShip.rotation = shipRotation;
        entryTransportShip.position = shipStartPosition;

        // Hide the pilot inside the actual ship while it descends. The same
        // ship instance from Stage 2 is animated, not a replacement model.
        pilot.SetParent(entryTransportShip, true);
        pilot.localPosition = new Vector3(0f, -0.40f, -1.8f);
        pilot.localRotation = Quaternion.identity;

        Vector3 landingCameraPosition = shipLandingPosition + new Vector3(0f, 14f, -26f);
        Vector3 landingCameraTarget = shipLandingPosition + Vector3.up * 1.0f;
        gameplayCamera.farClipPlane = Mathf.Max(savedCameraFarClip, 320f);
        // A tighter lens and grounded camera position keep the ship readable on a phone.
        gameplayCamera.fieldOfView = Mathf.Clamp(savedCameraFieldOfView, 52f, 60f);
        gameplayCamera.transform.position = landingCameraPosition;
        gameplayCamera.transform.rotation = Quaternion.LookRotation(
            shipStartPosition - gameplayCamera.transform.position, Vector3.up);

        // Start directly at the close shot: the camera does not dive from a
        // map-wide overview. It pans to follow the transport down to the surface.
        IntroMessage = "TRANSPORT DESCENDING — LANDING ZONE CONFIRMED";
        float descentElapsed = 0f;
        const float descentDuration = 4.2f;
        while (descentElapsed < descentDuration)
        {
            descentElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(descentElapsed / descentDuration));
            entryTransportShip.position = Vector3.Lerp(shipStartPosition, shipLandingPosition, t);
            Vector3 cameraTarget = Vector3.Lerp(shipStartPosition, landingCameraTarget, t);
            gameplayCamera.transform.rotation = Quaternion.LookRotation(
                cameraTarget - gameplayCamera.transform.position, Vector3.up);
            yield return null;
        }

        entryTransportShip.position = shipLandingPosition;
        gameplayCamera.transform.position = landingCameraPosition;
        gameplayCamera.transform.rotation = Quaternion.LookRotation(
            landingCameraTarget - landingCameraPosition, Vector3.up);
        StartupCheckpoint.Set("StageThreeTransportShipLanded");
        IntroMessage = "LANDING COMPLETE — PILOT DEPLOYING";
        float landingPause = 0f;
        while (landingPause < 0.45f)
        {
            landingPause += Time.unscaledDeltaTime;
            yield return null;
        }

        // The rear ramp is at the negative-Z end of the ship. Reveal the same pilot
        // there and animate the walk clear of the ramp before arming combat controls.
        Vector3 exitStart = entryTransportShip.TransformPoint(new Vector3(0f, 0f, -5.7f));
        Vector3 exitFinish = entryTransportShip.TransformPoint(new Vector3(0f, 0f, -11.3f));
        exitStart.y = 0f;
        exitFinish.y = 0f;
        pilot.SetParent(originalPlayerParent, true);
        pilot.localScale = originalPlayerLocalScale;
        pilot.position = exitStart;
        pilot.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        localPlayer.SetGroundedPosition(exitStart);
        for (int i = 0; i < pilotRenderers.Length; i++)
        {
            if (pilotRenderers[i] != null)
                pilotRenderers[i].enabled = pilotRendererWasEnabled[i];
        }

        IntroMessage = "PILOT ON FOOT — PREPARING FOR COMBAT";
        Quaternion tacticalOrbit = Quaternion.Euler(37f, 32f, 0f);
        Quaternion yawOnly = Quaternion.Euler(0f, 32f, 0f);
        Vector3 cameraStartPosition = gameplayCamera.transform.position;
        Quaternion cameraStartRotation = gameplayCamera.transform.rotation;
        float exitElapsed = 0f;
        const float exitDuration = 1.85f;
        while (exitElapsed < exitDuration)
        {
            exitElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(exitElapsed / exitDuration));
            Vector3 currentPlayerPosition = Vector3.Lerp(exitStart, exitFinish, t);
            pilot.position = currentPlayerPosition;
            Vector3 desiredCameraPosition = currentPlayerPosition + tacticalOrbit * Vector3.back * 16.63f;
            Vector3 desiredLookTarget = currentPlayerPosition + yawOnly * Vector3.forward * 4.5f +
                Vector3.up * 0.90f;
            gameplayCamera.transform.position = Vector3.Lerp(cameraStartPosition, desiredCameraPosition, t);
            Vector3 blendedLookTarget = Vector3.Lerp(landingCameraTarget, desiredLookTarget, t);
            Quaternion desiredRotation = Quaternion.LookRotation(
                blendedLookTarget - gameplayCamera.transform.position, Vector3.up);
            gameplayCamera.transform.rotation = Quaternion.Slerp(cameraStartRotation, desiredRotation, t);
            yield return null;
        }

        pilot.position = exitFinish;
        pilot.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        localPlayer.SetGroundedPosition(exitFinish);
        Vector3 finalCameraPosition = exitFinish + tacticalOrbit * Vector3.back * 16.63f;
        Vector3 finalLookTarget = exitFinish + yawOnly * Vector3.forward * 4.5f + Vector3.up * 0.90f;
        gameplayCamera.transform.position = finalCameraPosition;
        gameplayCamera.transform.rotation = Quaternion.LookRotation(
            finalLookTarget - finalCameraPosition, Vector3.up);
        gameplayCamera.fieldOfView = savedCameraFieldOfView;

        IsIntroCinematic = false;
        IsCinematic = false;
        IntroMessage = "MISSION LIVE — ELIMINATE ALL ALIENS";
        if (localPlayer != null)
        {
            localPlayer.enabled = true;
            localPlayer.SetMovementEnabled(true);
            if (localPlayer.Weapon != null) localPlayer.Weapon.enabled = true;
            if (localPlayer.Aim != null) localPlayer.Aim.enabled = true;
        }
        StartupCheckpoint.Set("StageThreeTransportDisembarkComplete_CombatReady");
    }

    private IEnumerator RunCentralLiftEnding()
    {
        // This coroutine can only begin once RemainingHostiles reaches zero.
        IsCinematic = true;
        IsIntroCinematic = false;
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

        Vector3 playerOffset = Vector3.up * 0.55f;
        Vector3 platformStart = new Vector3(0f, 0.28f, 0f);
        Vector3 platformTop = new Vector3(0f, 24.65f, 0f);
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
            FollowPlayerDuringCinematic();
            yield return null;
        }

        liftPlatform.position = platformTop;
        localPlayer.transform.position = new Vector3(0f, 25.2f, 0f);
        FollowPlayerDuringCinematic();
        StartupCheckpoint.Set("StageThreePlayerReachedUpperPlatform");

        float orbRevealElapsed = 0f;
        while (orbRevealElapsed < 0.65f)
        {
            orbRevealElapsed += Time.unscaledDeltaTime;
            FollowPlayerDuringCinematic();
            yield return null;
        }

        // The avatar disappears into the orb; only its fast, rising signal remains.
        HidePlayerForUplink();
        if (beamColumn != null) beamColumn.gameObject.SetActive(true);
        if (beamCoreColumn != null) beamCoreColumn.gameObject.SetActive(true);

        float beamElapsed = 0f;
        const float beamDuration = 1.45f;
        const float beamBaseY = 29.45f;
        const float beamMaximumHeight = 84f;
        while (beamElapsed < beamDuration)
        {
            beamElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(beamElapsed / beamDuration);
            float beamHeight = Mathf.Lerp(0.3f, beamMaximumHeight, t * t);
            Vector3 beamPosition = new Vector3(0f, beamBaseY + beamHeight * 0.5f, 0f);
            if (beamColumn != null)
            {
                beamColumn.position = beamPosition;
                beamColumn.localScale = new Vector3(1.55f, beamHeight * 0.5f, 1.55f);
            }
            if (beamCoreColumn != null)
            {
                beamCoreColumn.position = beamPosition;
                beamCoreColumn.localScale = new Vector3(0.40f, beamHeight * 0.5f, 0.40f);
            }
            FollowLaunchBeamDuringCinematic(beamBaseY + beamHeight);
            yield return null;
        }

        float afterglowElapsed = 0f;
        while (afterglowElapsed < 0.45f)
        {
            afterglowElapsed += Time.unscaledDeltaTime;
            FollowLaunchBeamDuringCinematic(beamBaseY + beamMaximumHeight);
            yield return null;
        }

        isComplete = true;
        IsCinematic = false;
        StartupCheckpoint.Set("StageThreeEndingComplete");
        GameSession.Instance?.EndMission(true);
    }

    private void HidePlayerForUplink()
    {
        if (localPlayer == null)
            return;
        savedPlayerRenderers = localPlayer.GetComponentsInChildren<Renderer>(true);
        savedPlayerRendererStates = new bool[savedPlayerRenderers.Length];
        for (int i = 0; i < savedPlayerRenderers.Length; i++)
        {
            savedPlayerRendererStates[i] = savedPlayerRenderers[i] != null && savedPlayerRenderers[i].enabled;
            if (savedPlayerRenderers[i] != null)
                savedPlayerRenderers[i].enabled = false;
        }
    }

    private void RestorePlayerVisibility()
    {
        if (savedPlayerRenderers != null && savedPlayerRendererStates != null)
        {
            for (int i = 0; i < savedPlayerRenderers.Length && i < savedPlayerRendererStates.Length; i++)
                if (savedPlayerRenderers[i] != null)
                    savedPlayerRenderers[i].enabled = savedPlayerRendererStates[i];
        }
        savedPlayerRenderers = null;
        savedPlayerRendererStates = null;
    }

    private void FollowLaunchBeamDuringCinematic(float beamTopY)
    {
        if (gameplayCamera == null)
            return;

        float t = Mathf.InverseLerp(30f, 112f, beamTopY);
        Vector3 cameraTarget = new Vector3(0f, 31f + 4f * t, -16f + 2.5f * t);
        Vector3 lookTarget = new Vector3(0f, Mathf.Max(33f, beamTopY * 0.88f), 0f);
        gameplayCamera.transform.position = Vector3.Lerp(gameplayCamera.transform.position,
            cameraTarget, 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
        Vector3 direction = lookTarget - gameplayCamera.transform.position;
        if (direction.sqrMagnitude > 0.001f)
            gameplayCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void FollowPlayerDuringCinematic()
    {
        if (gameplayCamera == null || localPlayer == null) return;
        Quaternion orbit = Quaternion.Euler(37f, 32f, 0f);
        Quaternion yawOnly = Quaternion.Euler(0f, 32f, 0f);
        Vector3 cameraTarget = localPlayer.transform.position + orbit * Vector3.back * 16.63f;
        Vector3 lookTarget = localPlayer.transform.position + yawOnly * Vector3.forward * 4.5f +
            Vector3.up * 0.90f;
        gameplayCamera.transform.position = Vector3.Lerp(
            gameplayCamera.transform.position, cameraTarget,
            1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
        Vector3 lookDirection = lookTarget - gameplayCamera.transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
            gameplayCamera.transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
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

        RestorePlayerVisibility();
        if (localPlayer != null && !localPlayer.IsDefeated)
        {
            localPlayer.enabled = true;
            localPlayer.SetMovementEnabled(true);
            if (localPlayer.Weapon != null) localPlayer.Weapon.enabled = true;
            if (localPlayer.Aim != null) localPlayer.Aim.enabled = true;
        }
        if (gameplayCamera != null)
        {
            gameplayCamera.backgroundColor = savedCameraBackground;
            gameplayCamera.clearFlags = savedCameraClearFlags;
            gameplayCamera.farClipPlane = savedCameraFarClip;
            gameplayCamera.fieldOfView = savedCameraFieldOfView;
            gameplayCamera.transform.position = savedCameraPosition;
            gameplayCamera.transform.rotation = savedCameraRotation;
        }
        if (ambientCaptured)
        {
            RenderSettings.ambientLight = savedAmbientLight;
            ambientCaptured = false;
        }

        hovercrafts.Clear();
        groundAliens.Clear();
        entryTransportShip = null;
        hostileHealth.Clear();
        worldBuilt = false;
        isComplete = false;
        IsCinematic = false;
        IsIntroCinematic = false;
        liftSequenceStarted = false;
        groundAliensSpawned = false;
        introStarted = false;
        beamColumn = null;
        beamCoreColumn = null;
        uplinkOrb = null;
        localPlayer = null;
        gameplayCamera = null;
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
        Material material, bool collider, Quaternion? rotation = null)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(worldRoot, false);
        box.transform.position = position;
        box.transform.localScale = scale;
        if (rotation.HasValue)
            box.transform.rotation = rotation.Value;
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

    private GameObject CreateSphere(string objectName, Vector3 position, Vector3 scale,
        Material material, bool collider)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = objectName;
        sphere.transform.SetParent(worldRoot, false);
        sphere.transform.position = position;
        sphere.transform.localScale = scale;
        SetRenderer(sphere, material);
        if (!collider)
            RemoveCollider(sphere);
        return sphere;
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

    private static void CreateSphereChild(Transform parent, string objectName, Vector3 position,
        Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
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
