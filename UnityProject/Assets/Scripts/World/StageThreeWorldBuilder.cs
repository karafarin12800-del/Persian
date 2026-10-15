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
    private Material groundMaterial;
    private Material silverMaterial;
    private Material darkMetalMaterial;
    private Material windowMaterial;
    private Material cyanGlowMaterial;
    private Material greenGlowMaterial;
    private Material warningMaterial;
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
        Debug.Log("PERSIA_STAGE3: sandy alien world built; 9 routes, 60 ground aliens and 6 destructible linear-lane craft ready.");
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
        CreateCylinder("S3_LiftPlatformGlow", new Vector3(0f, 0.49f, 0f),
            new Vector3(4.5f, 0.08f, 4.5f), greenGlowMaterial, false);

        CreateCylinder("S3_TopUplinkReceiver", new Vector3(0f, 26.1f, 0f),
            new Vector3(7.8f, 0.22f, 7.8f), greenGlowMaterial, false);

        // A large energy sphere sits above the deck. The final beam begins inside it
        // and erupts vertically beyond the top of the screen.
        uplinkOrb = CreateSphere("S3_UplinkOrb", new Vector3(0f, 29.5f, 0f),
            new Vector3(5.5f, 5.5f, 5.5f), greenGlowMaterial, false).transform;
        CreateCylinder("S3_UplinkOrbRing", new Vector3(0f, 29.5f, 0f),
            new Vector3(6.4f, 0.16f, 6.4f), silverMaterial, false);
        CreateSphere("S3_UplinkOrbCore", new Vector3(0f, 29.5f, 0f),
            new Vector3(2.35f, 2.35f, 2.35f), cyanGlowMaterial, false);

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
        Vector3[] starts =
        {
            new Vector3(-91f, 0f, -68f),
            new Vector3(-91f, 0f, -40f),
            new Vector3(-91f, 0f,  40f),
            new Vector3(-91f, 0f,  68f),
            new Vector3(-40f, 0f, -91f),
            new Vector3( 40f, 0f, -91f)
        };
        Vector3[] ends =
        {
            new Vector3(91f, 0f, -68f),
            new Vector3(91f, 0f, -40f),
            new Vector3(91f, 0f,  40f),
            new Vector3(91f, 0f,  68f),
            new Vector3(-40f, 0f, 91f),
            new Vector3( 40f, 0f, 91f)
        };

        for (int i = 0; i < starts.Length; i++)
        {
            int model = i % 3;
            GameObject craft = new GameObject("S3_HostileHovercraft_" + model + "_" + i);
            craft.transform.SetParent(worldRoot, false);
            float height = 1.8f + (i % 3) * 0.18f;
            craft.transform.position = starts[i] + Vector3.up * height;
            BuildHovercraftModel(craft.transform, model);

            BoxCollider hitbox = craft.AddComponent<BoxCollider>();
            hitbox.size = model == 2
                ? new Vector3(5.3f, 1.2f, 5.3f)
                : (model == 1 ? new Vector3(6.2f, 1.0f, 4.1f) : new Vector3(3.4f, 1.0f, 4.1f));

            TargetHealth health = craft.AddComponent<TargetHealth>();
            // 6 / 8 / 10 standard rifle hits (22 damage) for scout/interceptor/heavy craft.
            health.SetMaxHealth(model == 0 ? 132 : (model == 1 ? 176 : 220));
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
            if (patrol.Root == null || patrol.Health == null || patrol.Health.CurrentHealth <= 0)
                continue;

            float length = Vector3.Distance(patrol.StartPoint, patrol.EndPoint);
            if (length > 0.01f)
            {
                patrol.Progress += patrol.TravelDirection * patrol.Speed * Time.deltaTime / length;
                if (patrol.Progress >= 1f)
                {
                    patrol.Progress = 1f;
                    patrol.TravelDirection = -1f;
                }
                else if (patrol.Progress <= 0f)
                {
                    patrol.Progress = 0f;
                    patrol.TravelDirection = 1f;
                }
            }

            Vector3 point = Vector3.Lerp(patrol.StartPoint, patrol.EndPoint, patrol.Progress);
            point.y = patrol.Height + Mathf.Sin(Time.time * 1.8f + patrol.Progress * 6.28f) * 0.09f;
            patrol.Root.position = point;
            patrol.Heading = (patrol.EndPoint - patrol.StartPoint).normalized * patrol.TravelDirection;
            patrol.Root.rotation = Quaternion.LookRotation(patrol.Heading, Vector3.up);
        }
    }

    private void UpdateHovercraftShooting()
    {
        for (int i = 0; i < hovercrafts.Count; i++)
        {
            HovercraftPatrol craft = hovercrafts[i];
            if (craft.Root == null || craft.Health == null || craft.Health.CurrentHealth <= 0 ||
                Time.time < craft.NextShotTime)
                continue;

            // A bolt is fired straight forward in the ship's direction of travel.
            Vector3 origin = craft.Root.position + craft.Heading * 2.1f + Vector3.up * 0.12f;
            CreateHostileProjectile(origin, craft.Heading, craft.Root, 10, 72f, warningMaterial);
            craft.NextShotTime = Time.time + 2.25f + craft.Archetype * 0.25f;
            StartupCheckpoint.Set("StageThreeHovercraftFired");
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
            if (craft.Root == null || craft.Health == null || craft.Health.CurrentHealth <= 0)
                continue;

            Vector2 playerFlat = new Vector2(playerPosition.x, playerPosition.z);
            Vector2 craftFlat = new Vector2(craft.Root.position.x, craft.Root.position.z);
            if (Vector2.Distance(playerFlat, craftFlat) > 2.7f ||
                Mathf.Abs(playerPosition.y - craft.Root.position.y) > 2.5f ||
                Time.time < craft.NextDamageTime)
                continue;

            if (localPlayer.Health != null)
                localPlayer.Health.ApplyDamage(12, craft.Root);
            craft.NextDamageTime = Time.time + 1.4f;
            StartupCheckpoint.Set("StageThreeHovercraftThreatHit");
        }
    }

    private void BuildStageThreeGroundAliens()
    {
        if (groundAliensSpawned || localPlayer == null)
            return;
        groundAliensSpawned = true;

        List<Vector3> occupied = new List<Vector3>();
        // Eighteen fixed sentries stand at perimeter posts around the fortress.
        for (int i = 0; i < 18; i++)
        {
            float angle = i * 360f / 18f;
            Vector3 position = DirectionAt(angle) * (34.5f + (i % 2) * 4.5f);
            position.y = 0f;
            occupied.Add(position);
            SpawnGroundAlien(i, position, true);
        }

        // The rest are spread through open streets and outer ground, avoiding building footprints.
        int spawned = 18;
        for (int attempt = 0; attempt < 800 && spawned < 60; attempt++)
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

        // Deterministic fallback prevents a partial 60-rover roster if terrain exclusions change.
        for (int attempt = 0; spawned < 60 && attempt < 120; attempt++)
        {
            float angle = attempt * 31.7f;
            float radius = 74f + (attempt % 3) * 5f;
            Vector3 position = DirectionAt(angle) * radius;
            position.y = 0f;
            occupied.Add(position);
            SpawnGroundAlien(spawned, position, false);
            spawned++;
        }

        // Add twenty elite guards in the northern half of the map. Each patrols
        // a short, fixed 20m lane (10m either side) and uses a stronger weapon.
        int upperPatrols = 0;
        for (float z = 42f; z <= 102f && upperPatrols < 20; z += 8f)
        {
            for (float x = -92f; x <= 92f && upperPatrols < 20; x += 8f)
            {
                Vector3 position = new Vector3(x, 0f, z);
                if (new Vector2(position.x, position.z).magnitude < 36f ||
                    IsNearAlienTower(position, 8f))
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
                int guardIndex = 60 + upperPatrols;
                SpawnGroundAlien(guardIndex, position, true);
                GroundAlien guard = groundAliens[groundAliens.Count - 1];
                guard.UpperPatrol = true;
                guard.PatrolCenter = position;
                guard.PatrolRadius = 10f;
                guard.PatrolPhase = upperPatrols * 0.83f;
                guard.PatrolSpeed = 0.62f + (upperPatrols % 4) * 0.08f;
                guard.NextShotTime = Time.time + 1.0f + (upperPatrols % 6) * 0.18f;
                upperPatrols++;
            }
        }

        // Safe fallback for unusual map layouts: place any missing elite guards
        // along the open northern approach, still separated from the other roster.
        for (int attempt = 0; upperPatrols < 20 && attempt < 60; attempt++)
        {
            Vector3 position = new Vector3(-88f + (attempt % 18) * 9.5f, 0f, 96f - (attempt / 18) * 8f);
            occupied.Add(position);
            int guardIndex = 60 + upperPatrols;
            SpawnGroundAlien(guardIndex, position, true);
            GroundAlien guard = groundAliens[groundAliens.Count - 1];
            guard.UpperPatrol = true;
            guard.PatrolCenter = position;
            guard.PatrolRadius = 10f;
            guard.PatrolPhase = upperPatrols * 0.83f;
            guard.PatrolSpeed = 0.62f + (upperPatrols % 4) * 0.08f;
            guard.NextShotTime = Time.time + 1.0f + (upperPatrols % 6) * 0.18f;
            upperPatrols++;
        }

        Debug.Log("PERSIA_STAGE3: spawned " + groundAliens.Count +
            " ground aliens (60 roaming + 18 perimeter guards + 20 northern elite patrols).");
        StartupCheckpoint.Set("StageThree_80GroundAliensSpawned");
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

    private void SpawnGroundAlien(int index, Vector3 position, bool guard)
    {
        int archetype = index % 3 + 1;
        GameObject alien;
#if UNITY_ANDROID
        alien = new GameObject((guard ? "S3_AlienGuard_" : "S3_AlienRover_") + index.ToString("00"));
        alien.transform.position = position;
        alien.transform.localScale = new Vector3(0.88f, 0.88f, 0.88f);
        CapsuleCollider capsule = alien.AddComponent<CapsuleCollider>();
        capsule.radius = 0.55f;
        capsule.height = 1.8f;
        capsule.center = new Vector3(0f, 0.9f, 0f);

        GameObject meshSource = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        MeshFilter sourceFilter = meshSource.GetComponent<MeshFilter>();
        MeshFilter alienFilter = alien.AddComponent<MeshFilter>();
        alienFilter.sharedMesh = sourceFilter.sharedMesh;
        MeshRenderer alienRenderer = alien.AddComponent<MeshRenderer>();
        Color mobileBodyColor = archetype == 1 ? new Color(0.11f, 0.55f, 0.68f)
            : (archetype == 2 ? new Color(0.18f, 0.70f, 0.53f) : new Color(0.36f, 0.37f, 0.78f));
        alienRenderer.sharedMaterial = CreateMaterial("S3_AlienBody_" + archetype, mobileBodyColor);
        alienRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        alienRenderer.receiveShadows = false;
        Collider temporaryCollider = meshSource.GetComponent<Collider>();
        if (temporaryCollider != null) Destroy(temporaryCollider);
        Renderer temporaryRenderer = meshSource.GetComponent<Renderer>();
        if (temporaryRenderer != null) temporaryRenderer.enabled = false;
        Destroy(meshSource);
#else
        alien = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        alien.name = (guard ? "S3_AlienGuard_" : "S3_AlienRover_") + index.ToString("00");
        alien.transform.position = position;
        alien.transform.localScale = new Vector3(0.88f, 0.95f, 0.88f);
        Renderer alienRenderer = alien.GetComponent<Renderer>();
        if (alienRenderer != null)
        {
            Color bodyColor = archetype == 1 ? new Color(0.11f, 0.55f, 0.68f)
                : (archetype == 2 ? new Color(0.18f, 0.70f, 0.53f) : new Color(0.36f, 0.37f, 0.78f));
            alienRenderer.sharedMaterial = CreateMaterial("S3_AlienBody_" + archetype, bodyColor);
        }
#endif
        alien.transform.SetParent(worldRoot, true);

        TargetHealth health = alien.AddComponent<TargetHealth>();
        health.SetMaxHealth(archetype == 3 ? 150 : (archetype == 2 ? 115 : 90));
        // Stage 3 already shows a live total-hostile counter. Avoid spawning 80
        // independent IMGUI health bars/raycast loops on mobile; aim still reads TargetHealth.
        EnemyChase chase = alien.AddComponent<EnemyChase>();
        if (guard)
        {
            chase.SetTarget(localPlayer.transform);
            chase.enabled = false;
            CreateSphereChild(alien.transform, "S3_GuardEyeLeft", new Vector3(-0.16f, 0.55f, 0.38f),
                new Vector3(0.13f, 0.13f, 0.08f), cyanGlowMaterial);
            CreateSphereChild(alien.transform, "S3_GuardEyeRight", new Vector3(0.16f, 0.55f, 0.38f),
                new Vector3(0.13f, 0.13f, 0.08f), cyanGlowMaterial);
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

    private void UpdateUpperGuardPatrols()
    {
        for (int i = 0; i < groundAliens.Count; i++)
        {
            GroundAlien alien = groundAliens[i];
            if (!alien.UpperPatrol || alien.Root == null || alien.Health == null ||
                alien.Health.CurrentHealth <= 0)
                continue;

            float phase = Time.time * alien.PatrolSpeed + alien.PatrolPhase;
            Vector3 position = alien.PatrolCenter + Vector3.right * (Mathf.Sin(phase) * alien.PatrolRadius);
            position.y = 0f;
            alien.Root.position = position;

            // Keep the sentry facing its patrol direction; aiming uses a separate vector.
            float heading = Mathf.Cos(phase) >= 0f ? 1f : -1f;
            alien.Root.rotation = Quaternion.LookRotation(Vector3.right * heading, Vector3.up);
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
            Vector3 direction = localPlayer.transform.position + Vector3.up * 0.72f - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                continue;
            float distance = direction.magnitude;
            float maxRange = alien.UpperPatrol ? 82f : 62f;
            if (distance > maxRange)
            {
                alien.NextShotTime = Time.time + 0.7f;
                continue;
            }

            if (alien.UpperPatrol)
            {
                CreateHostileProjectile(origin, direction.normalized, alien.Root, 16, 86f, greenGlowMaterial);
                alien.NextShotTime = Time.time + 1.55f + (alien.Archetype - 1) * 0.18f;
            }
            else
            {
                CreateHostileProjectile(origin, direction.normalized, alien.Root, 7, 62f, warningMaterial);
                alien.NextShotTime = Time.time + 2.6f + (alien.Archetype - 1) * 0.35f;
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

    private IEnumerator RunStageThreeIntro()
    {
        IsCinematic = true;
        IsIntroCinematic = true;
        StartupCheckpoint.Set("StageThreeIntroStarted");

        if (localPlayer != null)
        {
            localPlayer.SetMovementEnabled(false);
            localPlayer.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            if (localPlayer.Weapon != null) localPlayer.Weapon.enabled = false;
            if (localPlayer.Aim != null) localPlayer.Aim.enabled = false;
        }

        if (gameplayCamera == null || localPlayer == null)
        {
            IsIntroCinematic = false;
            IsCinematic = false;
            yield break;
        }

        Vector3 playerStart = localPlayer.transform.position;
        Vector3 playerFinish = playerStart + Vector3.forward * 6f;
        Vector3 overviewPosition = new Vector3(0f, 48f, -116f);
        Vector3 overviewTarget = new Vector3(0f, 13f, 0f);
        gameplayCamera.transform.position = overviewPosition;
        gameplayCamera.transform.rotation = Quaternion.LookRotation(overviewTarget - overviewPosition, Vector3.up);
        gameplayCamera.fieldOfView = Mathf.Max(savedCameraFieldOfView, 62f);

        // Hold a wide reveal long enough to read the citadel, skyline, sand-colored
        // terrain, three craft silhouettes and the guards on their posts.
        float holdElapsed = 0f;
        const float revealHold = 1.65f;
        while (holdElapsed < revealHold)
        {
            holdElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        float entranceElapsed = 0f;
        const float entranceDuration = 1.25f;
        while (entranceElapsed < entranceDuration)
        {
            entranceElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(entranceElapsed / entranceDuration));
            localPlayer.transform.position = Vector3.Lerp(playerStart, playerFinish, t);
            Vector3 cameraPosition = Vector3.Lerp(overviewPosition, new Vector3(0f, 38f, -91f), t);
            Vector3 cameraTarget = Vector3.Lerp(overviewTarget, new Vector3(0f, 12f, -7f), t);
            gameplayCamera.transform.position = cameraPosition;
            gameplayCamera.transform.rotation = Quaternion.LookRotation(cameraTarget - cameraPosition, Vector3.up);
            yield return null;
        }
        localPlayer.transform.position = playerFinish;

        // Match Stage 2's actual chase-camera angle and distance for continuity.
        Quaternion stageTwoOrbit = Quaternion.Euler(37f, 32f, 0f);
        Quaternion yawOnly = Quaternion.Euler(0f, 32f, 0f);
        Vector3 finalCameraPosition = localPlayer.transform.position + stageTwoOrbit * Vector3.back * 16.63f;
        Vector3 finalLookTarget = localPlayer.transform.position + yawOnly * Vector3.forward * 4.5f +
            Vector3.up * 0.90f;
        Vector3 cameraStartPosition = gameplayCamera.transform.position;
        Quaternion cameraStartRotation = gameplayCamera.transform.rotation;
        float cameraElapsed = 0f;
        const float cameraBlendDuration = 1.35f;
        while (cameraElapsed < cameraBlendDuration)
        {
            cameraElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cameraElapsed / cameraBlendDuration));
            gameplayCamera.transform.position = Vector3.Lerp(cameraStartPosition, finalCameraPosition, t);
            Quaternion finalRotation = Quaternion.LookRotation(finalLookTarget - finalCameraPosition, Vector3.up);
            gameplayCamera.transform.rotation = Quaternion.Slerp(cameraStartRotation, finalRotation, t);
            gameplayCamera.fieldOfView = Mathf.Lerp(Mathf.Max(savedCameraFieldOfView, 62f),
                savedCameraFieldOfView, t);
            yield return null;
        }

        gameplayCamera.transform.position = finalCameraPosition;
        gameplayCamera.transform.rotation = Quaternion.LookRotation(finalLookTarget - finalCameraPosition, Vector3.up);
        gameplayCamera.fieldOfView = savedCameraFieldOfView;
        IsIntroCinematic = false;
        IsCinematic = false;
        if (localPlayer != null)
        {
            localPlayer.enabled = true;
            localPlayer.SetMovementEnabled(true);
            if (localPlayer.Weapon != null) localPlayer.Weapon.enabled = true;
            if (localPlayer.Aim != null) localPlayer.Aim.enabled = true;
        }
        for (int i = 0; i < groundAliens.Count; i++)
            if (!groundAliens[i].Guard && groundAliens[i].Chase != null &&
                groundAliens[i].Health != null && groundAliens[i].Health.CurrentHealth > 0)
                groundAliens[i].Chase.enabled = true;

        StartupCheckpoint.Set("StageThreeIntroComplete");
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
