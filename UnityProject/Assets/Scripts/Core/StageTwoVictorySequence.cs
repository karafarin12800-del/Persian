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
            Flash,
            ShipDescent,
            Boarding,
            Ascent,
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
            public Rigidbody[] Bodies;
            public bool[] BodyWasKinematic;
            public bool[] BodyDetectedCollisions;
            public Vector3[] BodyVelocity;
            public Vector3[] BodyAngularVelocity;
            public Vector3 ApproachPosition;
            public Vector3 DeckLocalPosition;
            public bool Boarded;
        }

        private readonly List<ActorState> actors = new List<ActorState>();
        private Transform shipRoot;
        private GameObject beamOuter;
        private GameObject beamCore;
        private Material hullMaterial;
        private Material darkMaterial;
        private Material cyanMaterial;
        private Material outerBeamMaterial;
        private Material coreBeamMaterial;
        private Vector3 landingPosition;
        private Vector3 shipStartPosition;
        private float groundY;
        private float phaseElapsed;
        private bool cleaned;

        private const float FlashDuration = 0.42f;
        private const float DescentDuration = 1.35f;
        private const float BoardingDuration = 3.55f;
        private const float AscentDuration = 2.0f;
        private const float AscentHeight = 68f;

        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }
        public SequencePhase Phase { get; private set; } = SequencePhase.Idle;
        public float FlashAlpha
        {
            get
            {
                if (Phase != SequencePhase.Flash) return 0f;
                return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseElapsed / FlashDuration));
            }
        }

        public void Begin(Transform localPlayer)
        {
            if (IsRunning || IsComplete) return;

            Vector3 mapReference = localPlayer != null ? localPlayer.position : Vector3.zero;
            Vector3 landingGround = FindLandingPoint(mapReference);
            groundY = landingGround.y;
            landingPosition = new Vector3(landingGround.x, groundY + 0.92f, landingGround.z);
            CollectLivingActors(localPlayer);
            shipStartPosition = landingPosition + Vector3.up * 34f;

            BuildShip();
            // Rotate the long hull axis along the nearby road corridor so its
            // wings are less likely to clip the village buildings while landing.
            shipRoot.rotation = Quaternion.Euler(0f, 90f, 0f);
            shipRoot.position = shipStartPosition;
            BuildEnergyBeam();

            phaseElapsed = 0f;
            Phase = SequencePhase.Flash;
            IsRunning = true;
            IsComplete = false;
            cleaned = false;

            Debug.Log("PERSIA_STAGE2_END: evacuation flash started after the final alien was defeated.");
        }

        private void Update()
        {
            if (!IsRunning) return;

            phaseElapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            switch (Phase)
            {
                case SequencePhase.Flash:
                    if (phaseElapsed >= FlashDuration)
                        ChangePhase(SequencePhase.ShipDescent);
                    break;

                case SequencePhase.ShipDescent:
                    UpdateBeam();
                    float descent = Mathf.Clamp01(phaseElapsed / DescentDuration);
                    shipRoot.position = Vector3.Lerp(shipStartPosition, landingPosition, Mathf.SmoothStep(0f, 1f, descent));
                    if (descent >= 1f)
                    {
                        shipRoot.position = landingPosition;
                        ChangePhase(SequencePhase.Boarding);
                    }
                    break;

                case SequencePhase.Boarding:
                    shipRoot.position = landingPosition;
                    UpdateBoardingActors();
                    UpdateBeam();
                    if (phaseElapsed >= BoardingDuration)
                    {
                        BoardAnyRemainingActors();
                        ChangePhase(SequencePhase.Ascent);
                    }
                    break;

                case SequencePhase.Ascent:
                    float ascent = Mathf.Clamp01(phaseElapsed / AscentDuration);
                    shipRoot.position = landingPosition + Vector3.up * (AscentHeight * Mathf.SmoothStep(0f, 1f, ascent));
                    UpdateBeam();
                    if (ascent >= 1f)
                    {
                        IsRunning = false;
                        IsComplete = true;
                        Phase = SequencePhase.Complete;
                        Debug.Log("PERSIA_STAGE2_END: ship ascended out of frame; returning to stage menu.");
                    }
                    break;
            }
        }

        private void ChangePhase(SequencePhase next)
        {
            Phase = next;
            phaseElapsed = 0f;
        }

        private void UpdateBoardingActors()
        {
            float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            for (int i = 0; i < actors.Count; i++)
            {
                ActorState actor = actors[i];
                if (actor == null || actor.Root == null || actor.Boarded) continue;

                Vector3 current = actor.Root.position;
                Vector3 approach = actor.ApproachPosition;
                approach.y = current.y;

                // First move toward the landing area around the ship, then climb
                // aboard. The late cutoff keeps the scene moving even if an actor
                // starts far away or is caught on nearby scenery.
                bool approaching = phaseElapsed < 2.2f &&
                    Vector2.Distance(new Vector2(current.x, current.z),
                        new Vector2(approach.x, approach.z)) > 1.1f;

                Vector3 target = approaching
                    ? approach
                    : shipRoot.TransformPoint(actor.DeckLocalPosition);

                Vector3 delta = target - current;
                Vector3 flatDirection = new Vector3(delta.x, 0f, delta.z);
                if (flatDirection.sqrMagnitude > 0.02f)
                {
                    Quaternion facing = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
                    actor.Root.rotation = Quaternion.Slerp(actor.Root.rotation, facing,
                        1f - Mathf.Exp(-7f * deltaTime));
                }

                actor.Root.position = Vector3.MoveTowards(current, target,
                    (approaching ? 7.5f : 9f) * deltaTime);

                if (!approaching && Vector3.Distance(actor.Root.position, target) <= 0.8f)
                    BoardActor(actor);
            }
        }

        private void BoardActor(ActorState actor)
        {
            if (actor == null || actor.Root == null || actor.Boarded || shipRoot == null) return;
            actor.Root.SetParent(shipRoot, true);
            actor.Root.localPosition = actor.DeckLocalPosition;
            actor.Root.localRotation = Quaternion.identity;
            actor.Boarded = true;
        }

        private void BoardAnyRemainingActors()
        {
            for (int i = 0; i < actors.Count; i++)
                BoardActor(actors[i]);
        }

        private void CollectLivingActors(Transform localPlayer)
        {
            actors.Clear();
            List<Transform> seen = new List<Transform>();

            // The local PlayerController has already been gated off for the end
            // cinematic, so explicitly include its root before querying active AIs.
            if (localPlayer != null)
            {
                PlayerController localController = localPlayer.GetComponentInChildren<PlayerController>(true);
                if (localController == null || !localController.IsDefeated)
                    AddActor(localPlayer, true, seen);
            }

            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController controller = players[i];
                if (controller == null || controller.IsDefeated) continue;
                AddActor(controller.transform, true, seen);
            }

            StageTwoBotCompetitor[] bots = FindObjectsByType<StageTwoBotCompetitor>(FindObjectsSortMode.None);
            for (int i = 0; i < bots.Length; i++)
            {
                StageTwoBotCompetitor bot = bots[i];
                if (bot == null || !bot.gameObject.activeInHierarchy) continue;
                AddActor(bot.transform, false, seen);
            }

            if (actors.Count == 0 && localPlayer != null)
                AddActor(localPlayer, true, seen);

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
            state.DeckLocalPosition = new Vector3(
                Mathf.Cos(angle) * 4.2f,
                1.15f,
                Mathf.Sin(angle) * 2.6f);
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

        private Vector3 FindLandingPoint(Vector3 reference)
        {
            float[] roadCoordinates = { -91f, -67f, -43f, -19f, 5f, 29f, 53f, 77f };
            Vector3 best = new Vector3(reference.x, reference.y, reference.z);
            float bestDistance = float.MaxValue;
            bool foundClear = false;

            for (int x = 0; x < roadCoordinates.Length; x++)
            {
                for (int z = 0; z < roadCoordinates.Length; z++)
                {
                    Vector3 candidate = new Vector3(roadCoordinates[x], reference.y, roadCoordinates[z]);
                    float distance = (new Vector2(candidate.x - reference.x, candidate.z - reference.z)).sqrMagnitude;
                    if (distance > 625f || distance >= bestDistance) continue;
                    if (!IsLandingAreaClear(candidate)) continue;

                    bestDistance = distance;
                    best = candidate;
                    foundClear = true;
                }
            }

            if (!foundClear)
                best = new Vector3(reference.x, reference.y, reference.z);

            RaycastHit hit;
            Vector3 rayOrigin = new Vector3(best.x, Mathf.Max(80f, reference.y + 80f), best.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 180f, ~0, QueryTriggerInteraction.Ignore))
                best.y = hit.point.y;
            else
                best.y = reference.y;

            return best;
        }

        private bool IsLandingAreaClear(Vector3 candidate)
        {
            // The lower edge of this probe stays above the terrain, so the ground
            // itself does not make every candidate appear occupied.
            Collider[] overlaps = Physics.OverlapBox(
                candidate + Vector3.up * 4.2f,
                new Vector3(4.5f, 2.2f, 8.0f),
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null) continue;
                Transform hitTransform = collider.transform;
                bool belongsToActor = false;
                for (int a = 0; a < actors.Count; a++)
                {
                    Transform root = actors[a].Root;
                    if (root != null && (hitTransform == root || hitTransform.IsChildOf(root)))
                    {
                        belongsToActor = true;
                        break;
                    }
                }
                if (!belongsToActor &&
                    hitTransform.GetComponentInParent<PlayerController>() == null &&
                    hitTransform.GetComponentInParent<StageTwoBotCompetitor>() == null)
                    return false;
            }

            return true;
        }

        private void BuildShip()
        {
            shipRoot = new GameObject("StageTwoEvacuationShip").transform;
            hullMaterial = CreateMaterial(new Color(0.62f, 0.72f, 0.80f, 1f), false, false);
            darkMaterial = CreateMaterial(new Color(0.075f, 0.14f, 0.22f, 1f), false, false);
            cyanMaterial = CreateMaterial(new Color(0.20f, 0.86f, 1f, 1f), false, true);
            outerBeamMaterial = CreateMaterial(new Color(0.12f, 0.42f, 1f, 0.20f), true, true);
            coreBeamMaterial = CreateMaterial(new Color(0.42f, 0.86f, 1f, 0.50f), true, true);

            CreateShipPart("MainHull", PrimitiveType.Sphere, new Vector3(0f, 0f, 0f),
                new Vector3(18f, 1.55f, 9f), hullMaterial);
            CreateShipPart("LowerKeel", PrimitiveType.Cube, new Vector3(0f, -0.22f, -0.15f),
                new Vector3(13f, 0.55f, 6.1f), darkMaterial);
            CreateShipPart("UpperDeck", PrimitiveType.Cube, new Vector3(0f, 0.48f, 0.15f),
                new Vector3(10f, 0.34f, 4.6f), hullMaterial);
            CreateShipPart("CommandDome", PrimitiveType.Sphere, new Vector3(0f, 0.92f, 0.45f),
                new Vector3(5.4f, 1.15f, 3.5f), hullMaterial);
            CreateShipPart("CentralSpine", PrimitiveType.Cube, new Vector3(0f, 0.90f, -1.6f),
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
                CreateShipPart("DeckLight", PrimitiveType.Cube, new Vector3(window * 1.25f, 0.76f, 2.1f),
                    new Vector3(0.42f, 0.12f, 0.14f), cyanMaterial);
                CreateShipPart("DeckLight", PrimitiveType.Cube, new Vector3(window * 1.25f, 0.76f, -2.1f),
                    new Vector3(0.42f, 0.12f, 0.14f), cyanMaterial);
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

        private void BuildEnergyBeam()
        {
            beamOuter = CreateBeamPart("StageTwoEvacuationBeamOuter", outerBeamMaterial);
            beamCore = CreateBeamPart("StageTwoEvacuationBeamCore", coreBeamMaterial);
            UpdateBeam();
        }

        private GameObject CreateBeamPart(string objectName, Material material)
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = objectName;
            Collider collider = beam.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            Renderer renderer = beam.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return beam;
        }

        private void UpdateBeam()
        {
            if (shipRoot == null || beamOuter == null || beamCore == null) return;

            float height = Mathf.Max(3f, shipRoot.position.y - groundY);
            float centerY = groundY + height * 0.5f;
            beamOuter.transform.position = new Vector3(shipRoot.position.x, centerY, shipRoot.position.z);
            beamOuter.transform.localScale = new Vector3(3.6f, height * 0.5f, 3.6f);
            beamCore.transform.position = new Vector3(shipRoot.position.x, centerY, shipRoot.position.z);
            beamCore.transform.localScale = new Vector3(1.05f, height * 0.5f, 1.05f);
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
                case SequencePhase.ShipDescent: return "EVACUATION SHIP INBOUND";
                case SequencePhase.Boarding: return "ALL SURVIVORS: BOARD THE SHIP";
                case SequencePhase.Ascent: return "EVACUATION IN PROGRESS";
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

            if (beamOuter != null) Destroy(beamOuter);
            if (beamCore != null) Destroy(beamCore);
            if (shipRoot != null) Destroy(shipRoot.gameObject);
            if (hullMaterial != null) Destroy(hullMaterial);
            if (darkMaterial != null) Destroy(darkMaterial);
            if (cyanMaterial != null) Destroy(cyanMaterial);
            if (outerBeamMaterial != null) Destroy(outerBeamMaterial);
            if (coreBeamMaterial != null) Destroy(coreBeamMaterial);

            actors.Clear();
            Debug.Log("PERSIA_STAGE2_END: cutscene actors and ship cleaned up.");
        }

        private void OnDestroy()
        {
            FinishAndCleanup();
        }
    }
}
