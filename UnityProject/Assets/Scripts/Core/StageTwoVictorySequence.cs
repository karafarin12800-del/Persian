using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Stage 2 evacuation cutscene. It uses unscaled time so it continues after
    /// GameSession.EndMission pauses the gameplay simulation.
    /// </summary>
    public sealed class StageTwoVictorySequence : MonoBehaviour
    {
        public enum SequencePhase
        {
            Idle,
            CameraSettle,
            Flash,
            Decloak,
            Boarding,
            Ascent,
            DepartureWait,
            Complete
        }

        private sealed class ActorState
        {
            public Transform Root;
            public bool IsLocalPlayer;
            public Transform OriginalParent;
            public Vector3 OriginalLocalPosition;
            public Quaternion OriginalLocalRotation;
            public Vector3 OriginalLocalScale;
            public Collider[] Colliders;
            public bool[] ColliderWasEnabled;
            public Renderer[] Renderers;
            public bool[] RendererWasEnabled;
            public Rigidbody[] Bodies;
            public bool[] BodyWasKinematic;
            public bool[] BodyDetectedCollisions;
            public Vector3[] BodyVelocity;
            public Vector3[] BodyAngularVelocity;
            public Vector3 ApproachPosition;
            public Vector3 InteriorLocalPosition;
            public bool Boarded;
            public bool HiddenInsideShip;
        }

        private readonly List<ActorState> actors = new List<ActorState>();
        private readonly List<Material> effectMaterials = new List<Material>();
        private Transform shipRoot;
        private Renderer[] shipRenderers;
        private static Transform retainedStageThreeShip;
        private bool preserveShipForStageThree;
        private Camera cinematicCamera;
        private Vector3 originalCameraPosition;
        private Quaternion originalCameraRotation;
        private float originalCameraFieldOfView;
        private float originalCameraOrthographicSize;
        private bool cameraWasOrthographic;
        private CameraClearFlags originalCameraClearFlags;
        private Color originalCameraBackgroundColor;
        private float originalCameraFarClipPlane;
        private ParticleSystem orbitalStars;
        private Material orbitalStarMaterial;
        private Vector3 ascentCameraOffset;
        private Vector3 cameraStartPosition;
        private Quaternion cameraStartRotation;
        private Vector3 cameraTargetPosition;
        private Quaternion cameraTargetRotation;
        private Vector3 landingPosition;
        private Vector3 shipForward;
        private float groundY;
        private float phaseElapsed;
        private float nextPlayerDiscoveryRetryTime;
        private bool cleaned;
        private bool cameraStateCaptured;
        private LineRenderer greenRing;
        private LineRenderer whiteRing;
        private ParticleSystem greenSparks;
        private ParticleSystem whiteSparks;
        private Material hullMaterial;
        private Material darkMaterial;
        private Material cyanMaterial;
        private Material ringGreenMaterial;
        private Material ringWhiteMaterial;

        private const float CameraSettleDuration = 0.58f;
        private const float FlashDuration = 0.20f;
        private const float DecloakDuration = 2.0f;
        private const float BoardingDuration = 6.2f;
        private const float AscentDuration = 4.2f;
        private const float AscentHeight = 180f;
        private const float DepartureWaitDuration = 3.0f;
        private const float MinimumDecloakDistance = 14f;

        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }
        public float DepartureWaitProgress { get; private set; }

        /// <summary>
        /// Preserve the exact evacuation ship instance from Stage 2 so Stage 3 can
        /// bring it back down from orbit and let the player disembark.
        /// </summary>
        public void PreserveShipForStageThree()
        {
            if (shipRoot != null)
                preserveShipForStageThree = true;
        }

        public static Transform TakeRetainedShipForStageThree()
        {
            Transform ship = retainedStageThreeShip;
            retainedStageThreeShip = null;
            if (ship != null)
                ship.gameObject.SetActive(true);
            return ship;
        }

        public static void DiscardRetainedShipForSceneReload()
        {
            Transform ship = retainedStageThreeShip;
            retainedStageThreeShip = null;
            if (ship != null)
                Destroy(ship.gameObject);
            StartupCheckpoint.Set("RetainedStageTwoShipDiscardedBeforeSceneReload");
        }
        public SequencePhase Phase { get; private set; } = SequencePhase.Idle;
        public float FlashAlpha
        {
            get
            {
                if (Phase != SequencePhase.Flash) return 0f;
                float t = Mathf.Clamp01(phaseElapsed / FlashDuration);
                return 1f - Mathf.SmoothStep(0f, 1f, t);
            }
        }

        // A short green haze accompanies the two-second stealth-field collapse.
        public float DecloakGlowAlpha
        {
            get
            {
                if (Phase != SequencePhase.Decloak) return 0f;
                return 1f - Mathf.Clamp01(phaseElapsed / DecloakDuration);
            }
        }

        public void Begin(Transform localPlayer, Camera cutsceneCamera)
        {
            if (IsRunning || IsComplete) return;

            if (localPlayer == null)
            {
                PlayerController discoveredPlayer = FindFirstObjectByType<PlayerController>();
                if (discoveredPlayer != null) localPlayer = discoveredPlayer.transform;
            }

            if (localPlayer == null)
            {
                // Do not mark the cutscene complete when the player reference is
                // temporarily unavailable. PrototypeFlow treats IsComplete as
                // permission to leave Stage 2, which previously skipped the ship
                // reveal and could trigger the broken transition on Android.
                Phase = SequencePhase.Idle;
                IsRunning = false;
                IsComplete = false;
                nextPlayerDiscoveryRetryTime = Time.unscaledTime + 0.5f;
                Debug.LogWarning("PERSIA_STAGE2_END: local player not ready; retrying before starting evacuation.");
                return;
            }

            cinematicCamera = cutsceneCamera != null ? cutsceneCamera : Camera.main;
            if (cinematicCamera != null)
            {
                originalCameraPosition = cinematicCamera.transform.position;
                originalCameraRotation = cinematicCamera.transform.rotation;
                originalCameraFieldOfView = cinematicCamera.fieldOfView;
                originalCameraOrthographicSize = cinematicCamera.orthographicSize;
                cameraWasOrthographic = cinematicCamera.orthographic;
                originalCameraClearFlags = cinematicCamera.clearFlags;
                originalCameraBackgroundColor = cinematicCamera.backgroundColor;
                originalCameraFarClipPlane = cinematicCamera.farClipPlane;
                cameraStartPosition = originalCameraPosition;
                cameraStartRotation = originalCameraRotation;
                cameraStateCaptured = true;
            }

            // Only the local hero is moved. Other competitors remain observers.
            CollectLivingActors(localPlayer);

            Vector3 viewForward = GetFlatDirection(
                cinematicCamera != null ? cinematicCamera.transform.forward : localPlayer.forward);
            if (viewForward.sqrMagnitude < 0.001f)
                viewForward = GetFlatDirection(localPlayer.forward);
            if (viewForward.sqrMagnitude < 0.001f)
                viewForward = Vector3.forward;

            Vector3 landingGround = FindDecloakPoint(localPlayer.position, viewForward, localPlayer);
            if (float.IsNaN(landingGround.x) || float.IsNaN(landingGround.y) ||
                float.IsNaN(landingGround.z))
            {
                // A crowded or obstructed arena must never bypass the evacuation
                // cinematic. Use a bounded emergency landing point ahead of the
                // player rather than marking the sequence complete without a ship.
                Vector3 fallbackDirection = GetFlatDirection(viewForward);
                if (fallbackDirection.sqrMagnitude < 0.001f)
                    fallbackDirection = Vector3.forward;
                Vector3 fallbackPoint = localPlayer.position +
                    fallbackDirection.normalized * MinimumDecloakDistance;
                fallbackPoint.x = Mathf.Clamp(fallbackPoint.x, -72f, 72f);
                fallbackPoint.z = Mathf.Clamp(fallbackPoint.z, -72f, 72f);
                fallbackPoint.y = 0f;

                RaycastHit fallbackHit;
                Vector3 fallbackRayOrigin = new Vector3(fallbackPoint.x,
                    Mathf.Max(80f, localPlayer.position.y + 80f), fallbackPoint.z);
                if (Physics.Raycast(fallbackRayOrigin, Vector3.down, out fallbackHit,
                    180f, ~0, QueryTriggerInteraction.Ignore))
                    fallbackPoint.y = fallbackHit.point.y;

                landingGround = fallbackPoint;
                Debug.LogWarning("PERSIA_STAGE2_END: no clear landing site; using bounded emergency landing point so the ship cinematic still runs.");
            }

            groundY = landingGround.y;
            landingPosition = new Vector3(landingGround.x, groundY + 0.92f, landingGround.z);
            Vector3 toShip = landingPosition - localPlayer.position;
            toShip.y = 0f;
            shipForward = toShip.sqrMagnitude > 0.001f ? toShip.normalized : viewForward;

            // Keep the hero near screen centre while making room to reveal the
            // transport at the nearest safe, reachable landing site ahead.
            if (cinematicCamera != null)
            {
                cameraTargetPosition = localPlayer.position - viewForward * 19f + Vector3.up * 11.5f;
                Vector3 lookTarget = localPlayer.position + Vector3.up * 1.2f;
                Vector3 lookDirection = lookTarget - cameraTargetPosition;
                cameraTargetRotation = lookDirection.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(lookDirection, Vector3.up)
                    : originalCameraRotation;
            }

            BuildShip();
            shipRoot.position = landingPosition;
            shipRoot.rotation = Quaternion.LookRotation(shipForward, Vector3.up);
            shipRoot.localScale = Vector3.one * 0.015f;
            SetShipAlpha(0f);
            BuildDecloakEffects();
            shipRenderers = shipRoot != null
                ? shipRoot.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];

            for (int i = 0; i < actors.Count; i++)
            {
                ActorState actor = actors[i];
                if (actor == null || actor.Root == null) continue;
                // Compute the entry point at full ship scale. The ship itself is
                // currently tiny for the decloak effect, so TransformPoint here
                // would incorrectly place the approach point almost at its centre.
                actor.ApproachPosition = landingPosition +
                    shipRoot.rotation * new Vector3(0f, 0f, -6.35f);
                actor.InteriorLocalPosition = new Vector3(0f, -0.57f, -2.25f);
            }

            phaseElapsed = 0f;
            Phase = SequencePhase.CameraSettle;
            IsRunning = true;
            IsComplete = false;
            cleaned = false;

            Debug.Log("PERSIA_STAGE2_END: camera framing; ship hidden at nearest clear forward landing site.");
        }

        private void Update()
        {
            if (!IsRunning)
            {
                // PlayerController/playerRoot can be briefly unavailable while the
                // match-ending flow disables gameplay. Retry discovery instead of
                // silently completing the cutscene and advancing without a ship.
                if (!IsComplete && Phase == SequencePhase.Idle &&
                    Time.unscaledTime >= nextPlayerDiscoveryRetryTime)
                {
                    nextPlayerDiscoveryRetryTime = Time.unscaledTime + 0.5f;
                    PlayerController discoveredPlayer = FindFirstObjectByType<PlayerController>();
                    if (discoveredPlayer != null)
                        Begin(discoveredPlayer.transform, cinematicCamera);
                }
                return;
            }

            float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            phaseElapsed += deltaTime;

            switch (Phase)
            {
                case SequencePhase.CameraSettle:
                {
                    float t = Mathf.Clamp01(phaseElapsed / CameraSettleDuration);
                    float eased = Mathf.SmoothStep(0f, 1f, t);
                    if (cinematicCamera != null)
                    {
                        cinematicCamera.transform.position =
                            Vector3.Lerp(cameraStartPosition, cameraTargetPosition, eased);
                        cinematicCamera.transform.rotation =
                            Quaternion.Slerp(cameraStartRotation, cameraTargetRotation, eased);
                        if (cameraWasOrthographic)
                            cinematicCamera.orthographicSize = Mathf.Max(originalCameraOrthographicSize, 20f);
                        else
                            cinematicCamera.fieldOfView = Mathf.Max(originalCameraFieldOfView, 78f);
                    }

                    if (t >= 1f)
                    {
                        if (cinematicCamera != null)
                        {
                            cinematicCamera.transform.position = cameraTargetPosition;
                            cinematicCamera.transform.rotation = cameraTargetRotation;
                        }
                        ChangePhase(SequencePhase.Flash);
                    }
                    break;
                }

                case SequencePhase.Flash:
                    if (phaseElapsed >= FlashDuration)
                    {
                        StartDecloakEffects();
                        ChangePhase(SequencePhase.Decloak);
                    }
                    break;

                case SequencePhase.Decloak:
                {
                    float t = Mathf.Clamp01(phaseElapsed / DecloakDuration);
                    float eased = Mathf.SmoothStep(0f, 1f, t);
                    if (shipRoot != null)
                    {
                        shipRoot.position = landingPosition;
                        shipRoot.localScale = Vector3.one * Mathf.Lerp(0.015f, 1f, eased);
                    }
                    SetShipAlpha(eased);
                    UpdateDecloakEffects(eased);

                    if (t >= 1f)
                    {
                        if (shipRoot != null) shipRoot.localScale = Vector3.one;
                        SetShipAlpha(1f);
                        ChangePhase(SequencePhase.Boarding);
                    }
                    break;
                }

                case SequencePhase.Boarding:
                    if (shipRoot != null) shipRoot.position = landingPosition;
                    UpdateLocalBoarding();
                    if (phaseElapsed >= BoardingDuration)
                    {
                        // Safety fallback prevents an obstructed approach from
                        // leaving the final sequence stuck indefinitely.
                        BoardAnyRemainingActors();
                        BeginOrbitalAscent();
                        ChangePhase(SequencePhase.Ascent);
                    }
                    break;

                case SequencePhase.Ascent:
                {
                    float t = Mathf.Clamp01(phaseElapsed / AscentDuration);
                    float eased = Mathf.SmoothStep(0f, 1f, t);
                    if (shipRoot != null)
                        shipRoot.position = landingPosition + Vector3.up * (AscentHeight * eased);

                    // Never attach the camera to the ship. Keep its position/rotation
                    // fixed while the transport rises out of the existing frame.
                    if (t >= 0.15f && ShipHasLeftCameraFrame())
                    {
                        CompleteAscentAfterShipLeavesFrame();
                        break;
                    }
                    if (t >= 1f)
                    {
                        // If the ship model has no usable renderers, still finish safely.
                        CompleteAscentAfterShipLeavesFrame();
                    }
                    break;
                }
                case SequencePhase.DepartureWait:
                {
                    // Hold the exact same world/camera view for a short, visible loading
                    // interval after the ship has left. PrototypeFlow displays the bar.
                    DepartureWaitProgress = Mathf.Clamp01(phaseElapsed / DepartureWaitDuration);
                    if (phaseElapsed >= DepartureWaitDuration)
                    {
                        DepartureWaitProgress = 1f;
                        IsRunning = false;
                        IsComplete = true;
                        Phase = SequencePhase.Complete;
                        StartupCheckpoint.Set("StageTwoDepartureWaitComplete_OpenStage3Selection");
                        Debug.Log("PERSIA_STAGE2_END: fixed-view departure wait completed; opening Stage 3 player selection.");
                    }
                    break;
                }
            }
        }

        private bool ShipHasLeftCameraFrame()
        {
            if (shipRoot == null) return true;
            if (cinematicCamera == null)
                return false;

            if (shipRenderers == null || shipRenderers.Length == 0)
                shipRenderers = shipRoot.GetComponentsInChildren<Renderer>(true);

            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(cinematicCamera);
            for (int i = 0; i < shipRenderers.Length; i++)
            {
                Renderer shipRenderer = shipRenderers[i];
                if (shipRenderer == null || !shipRenderer.enabled ||
                    !shipRenderer.gameObject.activeInHierarchy)
                    continue;

                if (GeometryUtility.TestPlanesAABB(frustum, shipRenderer.bounds))
                    return false;
            }

            // If a model has no enabled renderers, it is no longer visible to the player.
            return true;
        }

        private void CompleteAscentAfterShipLeavesFrame()
        {
            if (cinematicCamera != null && cameraStateCaptured)
            {
                cinematicCamera.transform.position = originalCameraPosition;
                cinematicCamera.transform.rotation = originalCameraRotation;
                cinematicCamera.orthographic = cameraWasOrthographic;
                cinematicCamera.clearFlags = originalCameraClearFlags;
                cinematicCamera.backgroundColor = originalCameraBackgroundColor;
                cinematicCamera.farClipPlane = originalCameraFarClipPlane;
                if (cameraWasOrthographic)
                    cinematicCamera.orthographicSize = originalCameraOrthographicSize;
                else
                    cinematicCamera.fieldOfView = originalCameraFieldOfView;
            }

            // No orbital sky or camera motion: restore the original camera and preserve
            // the same frame while a three-second progress bar fills.
            if (orbitalStars != null)
            {
                orbitalStars.gameObject.SetActive(false);
                Destroy(orbitalStars.gameObject);
                orbitalStars = null;
            }
            if (orbitalStarMaterial != null)
            {
                Destroy(orbitalStarMaterial);
                orbitalStarMaterial = null;
            }

            DepartureWaitProgress = 0f;
            ChangePhase(SequencePhase.DepartureWait);
            StartupCheckpoint.Set("StageTwoTransportLeftFrame_BeginFixedViewWait");
            Debug.Log("PERSIA_STAGE2_END: ship left the frame; keeping the camera fixed during the departure progress bar.");
        }

        private void BeginOrbitalAscent()
        {
            if (shipRoot == null) return;

            if (cinematicCamera != null)
            {
                ascentCameraOffset = cinematicCamera.transform.position - shipRoot.position;
                cinematicCamera.farClipPlane = Mathf.Max(originalCameraFarClipPlane, 600f);
            }
            else
            {
                ascentCameraOffset = new Vector3(0f, 12f, -32f);
            }
        }

        private void CreateOrbitalStarfield()
        {
            if (cinematicCamera == null || orbitalStars != null) return;

            GameObject starObject = new GameObject("StageTwoOrbitalStarfield");
            starObject.transform.SetParent(cinematicCamera.transform, false);
            starObject.transform.localPosition = Vector3.zero;
            starObject.transform.localRotation = Quaternion.identity;

            orbitalStars = starObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = orbitalStars.main;
            main.duration = AscentDuration;
            main.loop = false;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startColor = new Color(0.78f, 0.89f, 1f, 0.96f);
            main.maxParticles = 180;

            ParticleSystem.EmissionModule emission = orbitalStars.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, (short)150)
            });

            ParticleSystem.ShapeModule shape = orbitalStars.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, 0f, 85f);
            shape.scale = new Vector3(105f, 70f, 5f);

            ParticleSystemRenderer starRenderer = orbitalStars.GetComponent<ParticleSystemRenderer>();
            if (starRenderer != null)
            {
                starRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                starRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                starRenderer.receiveShadows = false;
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                    Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    orbitalStarMaterial = new Material(shader);
                    orbitalStarMaterial.name = "StageTwoOrbitalStarfieldMaterial";
                    orbitalStarMaterial.color = Color.white;
                    if (orbitalStarMaterial.HasProperty("_BaseColor"))
                        orbitalStarMaterial.SetColor("_BaseColor", Color.white);
                    if (orbitalStarMaterial.HasProperty("_Color"))
                        orbitalStarMaterial.SetColor("_Color", Color.white);
                    starRenderer.sharedMaterial = orbitalStarMaterial;
                }
            }

            orbitalStars.Play(true);
            StartupCheckpoint.Set("StageTwoTransportEnteringSpace");
        }

        private void ChangePhase(SequencePhase next)
        {
            Phase = next;
            phaseElapsed = 0f;
        }

        private void UpdateLocalBoarding()
        {
            if (actors.Count == 0 || shipRoot == null) return;

            ActorState actor = actors[0];
            if (actor == null || actor.Root == null || actor.Boarded) return;

            float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            Vector3 current = actor.Root.position;
            Vector3 approach = actor.ApproachPosition;
            approach.y = current.y;

            bool approaching = Vector2.Distance(
                new Vector2(current.x, current.z),
                new Vector2(approach.x, approach.z)) > 1.0f;

            Vector3 target = approaching
                ? approach
                : shipRoot.TransformPoint(actor.InteriorLocalPosition);

            Vector3 flatDirection = target - current;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude > 0.02f)
            {
                Quaternion facing = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
                actor.Root.rotation = Quaternion.Slerp(actor.Root.rotation, facing,
                    1f - Mathf.Exp(-8f * deltaTime));
            }

            actor.Root.position = Vector3.MoveTowards(current, target, 6.5f * deltaTime);
            if (!approaching && Vector3.Distance(actor.Root.position, target) <= 0.85f)
                BoardActor(actor);
        }

        private void BoardActor(ActorState actor)
        {
            if (actor == null || actor.Root == null || actor.Boarded || shipRoot == null) return;

            // The hero crosses the rear ramp into the passenger cabin. Hide the
            // model only after boarding so they are not visible riding on the roof.
            actor.Root.SetParent(shipRoot, true);
            actor.Root.localPosition = actor.InteriorLocalPosition;
            actor.Root.localRotation = Quaternion.identity;
            if (actor.Renderers != null)
            {
                for (int i = 0; i < actor.Renderers.Length; i++)
                    if (actor.Renderers[i] != null)
                        actor.Renderers[i].enabled = false;
            }
            actor.HiddenInsideShip = true;
            actor.Boarded = true;
            Debug.Log("PERSIA_STAGE2_END: local hero entered the ship passenger bay.");
        }

        private void BoardAnyRemainingActors()
        {
            for (int i = 0; i < actors.Count; i++)
                BoardActor(actors[i]);
        }

        private void CollectLivingActors(Transform localPlayer)
        {
            actors.Clear();
            if (localPlayer == null) return;

            PlayerController controller = localPlayer.GetComponentInChildren<PlayerController>(true);
            if (controller != null && controller.IsDefeated) return;

            AddActor(localPlayer, true, new List<Transform>());
            for (int i = 0; i < actors.Count; i++)
                PrepareActorForCutscene(actors[i]);
        }

        private void AddActor(Transform actorRoot, bool isLocalPlayer, List<Transform> seen)
        {
            if (actorRoot == null) return;
            for (int i = 0; i < seen.Count; i++)
                if (seen[i] == actorRoot || actorRoot.IsChildOf(seen[i]) || seen[i].IsChildOf(actorRoot))
                    return;

            seen.Add(actorRoot);
            ActorState state = new ActorState();
            state.Root = actorRoot;
            state.IsLocalPlayer = isLocalPlayer;
            state.OriginalParent = actorRoot.parent;
            state.OriginalLocalPosition = actorRoot.localPosition;
            state.OriginalLocalRotation = actorRoot.localRotation;
            state.OriginalLocalScale = actorRoot.localScale;
            int index = actors.Count;
            float angle = index * Mathf.PI * 2f / Mathf.Max(1, 10);
            state.ApproachPosition = new Vector3(
                landingPosition.x + Mathf.Cos(angle) * 7.3f,
                actorRoot.position.y,
                landingPosition.z + Mathf.Sin(angle) * 5.2f);
            state.InteriorLocalPosition = new Vector3(0f, -0.57f, -2.25f);
            actors.Add(state);
        }

        private void PrepareActorForCutscene(ActorState actor)
        {
            if (actor == null || actor.Root == null) return;

            actor.Colliders = actor.Root.GetComponentsInChildren<Collider>(true);
            actor.ColliderWasEnabled = new bool[actor.Colliders.Length];
            for (int i = 0; i < actor.Colliders.Length; i++)
            {
                Collider collider = actor.Colliders[i];
                if (collider == null) continue;
                actor.ColliderWasEnabled[i] = collider.enabled;
                collider.enabled = false;
            }

            actor.Renderers = actor.Root.GetComponentsInChildren<Renderer>(true);
            actor.RendererWasEnabled = new bool[actor.Renderers.Length];
            for (int i = 0; i < actor.Renderers.Length; i++)
                actor.RendererWasEnabled[i] = actor.Renderers[i] != null && actor.Renderers[i].enabled;

            actor.Bodies = actor.Root.GetComponentsInChildren<Rigidbody>(true);
            actor.BodyWasKinematic = new bool[actor.Bodies.Length];
            actor.BodyDetectedCollisions = new bool[actor.Bodies.Length];
            actor.BodyVelocity = new Vector3[actor.Bodies.Length];
            actor.BodyAngularVelocity = new Vector3[actor.Bodies.Length];
            for (int i = 0; i < actor.Bodies.Length; i++)
            {
                Rigidbody body = actor.Bodies[i];
                if (body == null) continue;
                actor.BodyWasKinematic[i] = body.isKinematic;
                actor.BodyDetectedCollisions[i] = body.detectCollisions;
                actor.BodyVelocity[i] = body.velocity;
                actor.BodyAngularVelocity[i] = body.angularVelocity;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            PlayerController controller = actor.Root.GetComponentInChildren<PlayerController>(true);
            if (controller != null) controller.enabled = false;
            StageTwoBotCompetitor bot = actor.Root.GetComponentInChildren<StageTwoBotCompetitor>(true);
            if (bot != null) bot.enabled = false;
        }

        private Vector3 FindDecloakPoint(Vector3 reference, Vector3 forward, Transform localPlayer)
        {
            forward = GetFlatDirection(forward);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

            // Search the nearest ship-length landing distance first. Only fan out
            // to either side when the directly-forward site is blocked.
            float[] distances =
            {
                MinimumDecloakDistance, 16f, 18f, 21f, 24f, 28f, 33f
            };
            float[] angleOffsets =
            {
                0f, 10f, -10f, 20f, -20f, 30f, -30f, 40f, -40f, 50f, -50f
            };

            for (int d = 0; d < distances.Length; d++)
            {
                float distance = distances[d];
                for (int a = 0; a < angleOffsets.Length; a++)
                {
                    float offset = angleOffsets[a];
                    Vector3 direction = Quaternion.AngleAxis(offset, Vector3.up) * forward;
                    Vector3 candidate = reference + direction * distance;
                    if (candidate.x < -83f || candidate.x > 83f ||
                        candidate.z < -83f || candidate.z > 83f)
                        continue;

                    candidate.y = 0f;
                    Quaternion shipRotation = Quaternion.LookRotation(direction, Vector3.up);
                    Vector3 rearRampApproach = candidate - direction * 6.35f;
                    if (!IsLandingAreaClear(candidate, shipRotation, localPlayer))
                        continue;
                    if (!HasClearApproach(reference, rearRampApproach, localPlayer))
                        continue;

                    RaycastHit hit;
                    Vector3 rayOrigin = new Vector3(candidate.x,
                        Mathf.Max(80f, reference.y + 80f), candidate.z);
                    if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 180f,
                        ~0, QueryTriggerInteraction.Ignore))
                        candidate.y = hit.point.y;

                    Debug.Log("PERSIA_STAGE2_END: nearest clear forward ship site selected at " +
                        distance.ToString("F1") + "m; angle=" + offset.ToString("F0") + " degrees.");
                    return candidate;
                }
            }

            // Never place the ship inside a building or across a blocked approach.
            // A NaN result makes the caller skip the cutscene rather than teleport
            // the player into geometry.
            Debug.LogError("PERSIA_STAGE2_END: no clear forward landing site or unobstructed boarding path found.");
            return new Vector3(float.NaN, float.NaN, float.NaN);
        }

        private bool HasClearApproach(Vector3 fromPosition, Vector3 toPosition, Transform localPlayer)
        {
            Vector3 from = fromPosition + Vector3.up * 1.05f;
            Vector3 to = toPosition + Vector3.up * 1.05f;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.1f) return true;

            // Sweep the character-width corridor rather than using a centre ray,
            // so automatic boarding will not cut through walls, cars or fences.
            RaycastHit[] hits = Physics.SphereCastAll(
                from, 0.42f, delta / distance, distance,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || collider.isTrigger) continue;
                Transform hit = collider.transform;
                if (localPlayer != null &&
                    (hit == localPlayer || hit.IsChildOf(localPlayer) || localPlayer.IsChildOf(hit)))
                    continue;
                if (hit.GetComponentInParent<PlayerController>() != null ||
                    hit.GetComponentInParent<StageTwoBotCompetitor>() != null ||
                    hit.GetComponentInParent<EnemyChase>() != null ||
                    hit.GetComponentInParent<PickupItem>() != null)
                    continue;
                return false;
            }
            return true;
        }

        private static Vector3 GetFlatDirection(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
        }

        private bool IsLandingAreaClear(Vector3 candidate, Quaternion shipRotation, Transform localPlayer)
        {
            // Check the full roughly 21m x 11m footprint at hull height, plus
            // the space above it. Both probes start above the ground so the floor
            // itself does not make every candidate appear occupied.
            Collider[] lowOverlaps = Physics.OverlapBox(
                candidate + Vector3.up * 2.30f,
                new Vector3(10.8f, 2.15f, 5.6f),
                shipRotation,
                ~0,
                QueryTriggerInteraction.Ignore);
            Collider[] highOverlaps = Physics.OverlapBox(
                candidate + Vector3.up * 6.10f,
                new Vector3(10.8f, 3.50f, 5.6f),
                shipRotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            if (HasBlockingOverlap(lowOverlaps, localPlayer)) return false;
            if (HasBlockingOverlap(highOverlaps, localPlayer)) return false;
            return true;
        }

        private static bool HasBlockingOverlap(Collider[] overlaps, Transform localPlayer)
        {
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null || collider.isTrigger) continue;

                // Terrain, the road mesh and sidewalks are walkable surfaces, not
                // obstructions to an invisible ship appearing just above the ground.
                if (IsWalkableGroundCollider(collider)) continue;

                Transform hitTransform = collider.transform;
                if (localPlayer != null &&
                    (hitTransform == localPlayer || hitTransform.IsChildOf(localPlayer) ||
                     localPlayer.IsChildOf(hitTransform)))
                    continue;

                // Any other solid object—including a house, tree, car, fence or
                // competitor—means that this is not clear landing space.
                return true;
            }
            return false;
        }

        private static bool IsWalkableGroundCollider(Collider collider)
        {
            if (collider == null) return false;
            if (collider is TerrainCollider) return true;

            string objectName = collider.gameObject.name;
            return objectName == "AndroidTerrain3D" ||
                objectName == "Terrain3D" ||
                objectName == "StageTwoVillageDirtLanes" ||
                objectName == "AndroidRoadGrid" ||
                objectName == "AndroidSidewalkGrid" ||
                objectName == "AndroidContinuousGroundFoundation" ||
                objectName.StartsWith("Road", System.StringComparison.OrdinalIgnoreCase) ||
                objectName.StartsWith("Sidewalk", System.StringComparison.OrdinalIgnoreCase);
        }

        private void BuildShip()
        {
            shipRoot = new GameObject("StageTwoEvacuationShip").transform;
            // Transparent-capable materials allow the ship to phase in over the
            // full two-second reveal instead of simply popping into existence.
            hullMaterial = CreateMaterial(new Color(0.62f, 0.72f, 0.80f, 1f), true, true);
            darkMaterial = CreateMaterial(new Color(0.075f, 0.14f, 0.22f, 1f), true, false);
            cyanMaterial = CreateMaterial(new Color(0.32f, 1f, 0.47f, 1f), true, true);

            // Split the hull into port/starboard lobes to leave a real open
            // passenger channel down the centre instead of a solid hull roof.
            CreateShipPart("PortHull", PrimitiveType.Sphere, new Vector3(-6.5f, 0f, 0f),
                new Vector3(8.8f, 1.45f, 9.0f), hullMaterial);
            CreateShipPart("StarboardHull", PrimitiveType.Sphere, new Vector3(6.5f, 0f, 0f),
                new Vector3(8.8f, 1.45f, 9.0f), hullMaterial);

            CreateShipPart("PortKeel", PrimitiveType.Cube, new Vector3(-3.8f, -0.58f, -0.15f),
                new Vector3(5.6f, 0.36f, 6.1f), darkMaterial);
            CreateShipPart("StarboardKeel", PrimitiveType.Cube, new Vector3(3.8f, -0.58f, -0.15f),
                new Vector3(5.6f, 0.36f, 6.1f), darkMaterial);

            // An open-stern passenger bay. The hero walks up the ramp and through
            // the centre aisle; the bay is enclosed by low sidewalls, not a roof.
            CreateShipPart("PassengerCabinFloor", PrimitiveType.Cube,
                new Vector3(0f, -0.72f, -1.85f), new Vector3(3.35f, 0.14f, 4.65f), darkMaterial);
            CreateShipPart("PassengerCabinPortWall", PrimitiveType.Cube,
                new Vector3(-1.72f, 0.12f, -1.85f), new Vector3(0.18f, 1.72f, 4.65f), darkMaterial);
            CreateShipPart("PassengerCabinStarboardWall", PrimitiveType.Cube,
                new Vector3(1.72f, 0.12f, -1.85f), new Vector3(0.18f, 1.72f, 4.65f), darkMaterial);
            CreateShipPart("PassengerCabinForwardBulkhead", PrimitiveType.Cube,
                new Vector3(0f, 0.12f, 0.43f), new Vector3(3.35f, 1.72f, 0.18f), darkMaterial);
            CreateShipPart("RearEntryRamp", PrimitiveType.Cube,
                new Vector3(0f, -0.72f, -5.20f), new Vector3(3.8f, 0.14f, 2.65f), darkMaterial);

            CreateShipPart("PortDeck", PrimitiveType.Cube, new Vector3(-4.35f, 0.48f, 0.15f),
                new Vector3(3.1f, 0.34f, 4.6f), hullMaterial);
            CreateShipPart("StarboardDeck", PrimitiveType.Cube, new Vector3(4.35f, 0.48f, 0.15f),
                new Vector3(3.1f, 0.34f, 4.6f), hullMaterial);
            CreateShipPart("CommandDome", PrimitiveType.Sphere, new Vector3(0f, 0.92f, 1.0f),
                new Vector3(5.4f, 1.15f, 3.5f), hullMaterial);
            CreateShipPart("CentralSpine", PrimitiveType.Cube, new Vector3(0f, 0.90f, 2.0f),
                new Vector3(7.2f, 0.52f, 1.2f), darkMaterial);
            CreateShipPart("PortWing", PrimitiveType.Cube, new Vector3(-7.2f, -0.12f, 0.4f),
                new Vector3(5.3f, 0.52f, 2.4f), darkMaterial);
            CreateShipPart("StarboardWing", PrimitiveType.Cube, new Vector3(7.2f, -0.12f, 0.4f),
                new Vector3(5.3f, 0.52f, 2.4f), darkMaterial);
            CreateShipPart("FrontNose", PrimitiveType.Sphere, new Vector3(0f, -0.02f, 4.0f),
                new Vector3(5.4f, 0.9f, 2.5f), hullMaterial);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int engine = 0; engine < 3; engine++)
                {
                    float z = -3.0f + engine * 2.2f;
                    CreateShipPart("EngineGlow", PrimitiveType.Sphere,
                        new Vector3(side * (5.0f + (engine == 1 ? 1.2f : 0f)), -0.20f, z),
                        new Vector3(1.0f, 0.34f, 1.0f), cyanMaterial);
                }
            }

            for (int window = -3; window <= 3; window++)
            {
                float z = window * 1.05f;
                CreateShipPart("DeckLight", PrimitiveType.Cube, new Vector3(-4.35f, 0.76f, z),
                    new Vector3(0.16f, 0.12f, 0.42f), cyanMaterial);
                CreateShipPart("DeckLight", PrimitiveType.Cube, new Vector3(4.35f, 0.76f, z),
                    new Vector3(0.16f, 0.12f, 0.42f), cyanMaterial);
            }
        }

        private void CreateShipPart(string name, PrimitiveType type, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(shipRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void BuildDecloakEffects()
        {
            ringGreenMaterial = CreateEffectMaterial(new Color(0.20f, 1f, 0.38f, 0.92f));
            ringWhiteMaterial = CreateEffectMaterial(new Color(0.88f, 1f, 0.94f, 0.90f));

            greenRing = CreateEnergyRing("StageTwoDecloakGreenRing", ringGreenMaterial, 9.3f, -0.72f, 0.20f);
            whiteRing = CreateEnergyRing("StageTwoDecloakWhiteRing", ringWhiteMaterial, 7.4f, 0.82f, 0.13f);
            greenRing.enabled = false;
            whiteRing.enabled = false;

            greenSparks = CreateSparkSystem("StageTwoGreenDecloakSparks",
                new Color(0.18f, 1f, 0.30f, 1f), (short)72, 9.0f);
            whiteSparks = CreateSparkSystem("StageTwoWhiteDecloakSparks",
                new Color(0.94f, 1f, 1f, 1f), (short)48, 7.5f);
        }

        private LineRenderer CreateEnergyRing(string objectName, Material material,
            float radius, float localHeight, float width)
        {
            GameObject ringObject = new GameObject(objectName);
            ringObject.transform.SetParent(shipRoot, false);
            ringObject.transform.localPosition = Vector3.up * localHeight;

            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 65;
            ring.startWidth = width;
            ring.endWidth = width * 0.45f;
            ring.numCapVertices = 2;
            ring.sharedMaterial = material;
            ring.startColor = material != null ? material.color : Color.white;
            ring.endColor = ring.startColor;

            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = (i / (float)(ring.positionCount - 1)) * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius));
            }
            return ring;
        }

        private Material CreateEffectMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            if (shader == null) return null;

            Material material = new Material(shader);
            material.name = "StageTwoDecloakEffectMaterial";
            material.color = color;
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            SetMaterialAlpha(material, color.a);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * 1.4f);
            }
            effectMaterials.Add(material);
            return material;
        }

        private ParticleSystem CreateSparkSystem(string objectName, Color color,
            short firstBurstCount, float speed)
        {
            GameObject host = new GameObject(objectName);
            host.transform.position = landingPosition + Vector3.up * 1.2f;

            ParticleSystem sparks = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = sparks.main;
            main.duration = DecloakDuration;
            main.loop = false;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.92f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.22f);
            main.startColor = color;
            main.gravityModifier = 0.05f;
            main.maxParticles = 260;

            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 9f;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, firstBurstCount),
                new ParticleSystem.Burst(0.48f, (short)(firstBurstCount * 2 / 3)),
                new ParticleSystem.Burst(1.12f, (short)(firstBurstCount / 2))
            });

            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 6.0f;
            shape.radiusThickness = 0.18f;
            shape.scale = new Vector3(1.55f, 0.62f, 1.25f);

            ParticleSystemRenderer particleRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                    Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                if (particleShader != null)
                {
                    Material particleMaterial = new Material(particleShader);
                    particleMaterial.name = objectName + "Material";
                    particleMaterial.color = color;
                    if (particleMaterial.HasProperty("_BaseColor"))
                        particleMaterial.SetColor("_BaseColor", color);
                    if (particleMaterial.HasProperty("_Color"))
                        particleMaterial.SetColor("_Color", color);
                    particleRenderer.sharedMaterial = particleMaterial;
                    effectMaterials.Add(particleMaterial);
                }
            }

            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return sparks;
        }

        private void StartDecloakEffects()
        {
            if (greenRing != null) greenRing.enabled = true;
            if (whiteRing != null) whiteRing.enabled = true;
            if (greenSparks != null) greenSparks.Play(true);
            if (whiteSparks != null) whiteSparks.Play(true);
        }

        private void UpdateDecloakEffects(float progress)
        {
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            if (greenRing != null)
            {
                greenRing.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 1.15f, eased);
                greenRing.transform.localRotation = Quaternion.Euler(0f, phaseElapsed * 120f, 0f);
            }
            if (whiteRing != null)
            {
                whiteRing.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, 1.04f, eased);
                whiteRing.transform.localRotation = Quaternion.Euler(0f, -phaseElapsed * 155f, 0f);
            }

            float alpha = Mathf.Lerp(0.95f, 0.28f, eased);
            SetMaterialAlpha(ringGreenMaterial, alpha);
            SetMaterialAlpha(ringWhiteMaterial, alpha);
        }

        private void SetShipAlpha(float alpha)
        {
            SetMaterialAlpha(hullMaterial, alpha);
            SetMaterialAlpha(darkMaterial, alpha);
            SetMaterialAlpha(cyanMaterial, alpha);
        }

        private static void SetMaterialAlpha(Material material, float alpha)
        {
            if (material == null) return;
            alpha = Mathf.Clamp01(alpha);
            Color color = material.color;
            color.a = alpha;
            material.color = color;
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_FadeAlpha")) material.SetFloat("_FadeAlpha", alpha);
        }

        private static Material CreateMaterial(Color color, bool transparent, bool emissive)
        {
            Shader shader = transparent
                ? (Shader.Find("PersiaWar/AndroidFade") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard"))
                : (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color"));

            if (shader == null) return null;

            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (transparent && shader.name == "Standard")
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = 3000;
            }

            if (emissive)
            {
                Color emission = new Color(color.r, color.g, color.b, 1f) * 1.5f;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission);
                }
            }
            return material;
        }

        public string GetCaption()
        {
            switch (Phase)
            {
                case SequencePhase.Flash: return "STEALTH FIELD COLLAPSE";
                case SequencePhase.Decloak: return "ALIEN SHIP REVEALING";
                case SequencePhase.Boarding: return "BOARD THE SHIP";
                case SequencePhase.Ascent: return "ASCENDING INTO SPACE — STAGE 3 APPROACH";
                default: return string.Empty;
            }
        }

        public void FinishAndCleanup()
        {
            if (cleaned) return;
            cleaned = true;

            for (int i = 0; i < actors.Count; i++)
            {
                ActorState actor = actors[i];
                if (actor == null || actor.Root == null) continue;

                actor.Root.SetParent(actor.OriginalParent, false);
                actor.Root.localPosition = actor.OriginalLocalPosition;
                actor.Root.localRotation = actor.OriginalLocalRotation;
                actor.Root.localScale = actor.OriginalLocalScale;

                if (actor.Colliders != null)
                {
                    for (int c = 0; c < actor.Colliders.Length; c++)
                        if (actor.Colliders[c] != null)
                            actor.Colliders[c].enabled = actor.ColliderWasEnabled[c];
                }

                if (actor.Renderers != null && actor.RendererWasEnabled != null)
                {
                    for (int r = 0; r < actor.Renderers.Length; r++)
                        if (actor.Renderers[r] != null)
                            actor.Renderers[r].enabled = actor.RendererWasEnabled[r];
                }

                if (actor.Bodies != null)
                {
                    for (int b = 0; b < actor.Bodies.Length; b++)
                    {
                        Rigidbody body = actor.Bodies[b];
                        if (body == null) continue;
                        body.isKinematic = actor.BodyWasKinematic[b];
                        body.detectCollisions = actor.BodyDetectedCollisions[b];
                        body.velocity = actor.BodyVelocity[b];
                        body.angularVelocity = actor.BodyAngularVelocity[b];
                    }
                }

                if (!actor.IsLocalPlayer)
                    Destroy(actor.Root.gameObject);
            }

            if (cinematicCamera != null && cameraStateCaptured)
            {
                cinematicCamera.transform.position = originalCameraPosition;
                cinematicCamera.transform.rotation = originalCameraRotation;
                cinematicCamera.orthographic = cameraWasOrthographic;
                cinematicCamera.clearFlags = originalCameraClearFlags;
                cinematicCamera.backgroundColor = originalCameraBackgroundColor;
                cinematicCamera.farClipPlane = originalCameraFarClipPlane;
                if (cameraWasOrthographic)
                    cinematicCamera.orthographicSize = originalCameraOrthographicSize;
                else
                    cinematicCamera.fieldOfView = originalCameraFieldOfView;
            }

            if (greenSparks != null) Destroy(greenSparks.gameObject);
            if (whiteSparks != null) Destroy(whiteSparks.gameObject);
            if (orbitalStars != null) Destroy(orbitalStars.gameObject);
            orbitalStars = null;
            if (orbitalStarMaterial != null) Destroy(orbitalStarMaterial);
            orbitalStarMaterial = null;
            // The decloak rings belong to the ship hierarchy; remove them before
            // handing the same ship instance to Stage 3.
            if (greenRing != null) Destroy(greenRing.gameObject);
            if (whiteRing != null) Destroy(whiteRing.gameObject);

            if (shipRoot != null)
            {
                if (preserveShipForStageThree)
                {
                    if (retainedStageThreeShip != null && retainedStageThreeShip != shipRoot)
                        Destroy(retainedStageThreeShip.gameObject);
                    shipRoot.SetParent(null, true);
                    shipRoot.name = "StageThreeEntryTransportShip";
                    shipRoot.gameObject.SetActive(false);
                    retainedStageThreeShip = shipRoot;
                }
                else
                {
                    Destroy(shipRoot.gameObject);
                }
            }

            // Ship materials remain alive only when the exact ship is being passed
            // into Stage 3. All cutscene-only materials are still released.
            if (!preserveShipForStageThree)
            {
                if (hullMaterial != null) Destroy(hullMaterial);
                if (darkMaterial != null) Destroy(darkMaterial);
                if (cyanMaterial != null) Destroy(cyanMaterial);
            }
            for (int i = 0; i < effectMaterials.Count; i++)
                if (effectMaterials[i] != null) Destroy(effectMaterials[i]);
            effectMaterials.Clear();

            actors.Clear();
            Debug.Log("PERSIA_STAGE2_END: cutscene actors and ship cleaned up.");
        }

        private void OnDestroy()
        {
            FinishAndCleanup();
        }
    }
}
