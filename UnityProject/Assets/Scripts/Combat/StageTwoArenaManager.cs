using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Local AI-filled Stage 2 arena. Human online matchmaking requires a network
    /// backend; this manager keeps the offline build playable and tracks kill credits.
    /// </summary>
    public sealed class StageTwoArenaManager : MonoBehaviour
    {
        public static StageTwoArenaManager Instance { get; private set; }

        private readonly Dictionary<string, int> kills = new Dictionary<string, int>();
        private readonly List<GameObject> bots = new List<GameObject>();
        private Transform localPlayer;
        private bool finished;

        public int LocalKills => kills.TryGetValue("YOU", out int value) ? value : 0;
        public int LastLocalRank { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearBots();
        }

        public void BeginArena(Transform player, int botCount)
        {
            ClearBots();
            kills.Clear();
            finished = false;
            LastLocalRank = 0;
            localPlayer = player;
            kills["YOU"] = 0;
            botCount = Mathf.Clamp(botCount, 0, 9);

            for (int i = 0; i < botCount; i++)
            {
                Vector3 position = FindBotSpawn(i, botCount);
                GameObject bot = new GameObject("StageTwo_AI_Competitor_" + (i + 1).ToString("00"));
                bot.transform.position = position;
                bot.transform.localScale = Vector3.one;

                Color[] palette =
                {
                    new Color(0.20f, 0.72f, 0.94f), new Color(0.90f, 0.42f, 0.18f),
                    new Color(0.66f, 0.38f, 0.91f), new Color(0.92f, 0.72f, 0.20f),
                    new Color(0.16f, 0.78f, 0.57f), new Color(0.84f, 0.30f, 0.42f),
                    new Color(0.42f, 0.62f, 0.96f), new Color(0.78f, 0.80f, 0.84f),
                    new Color(0.40f, 0.82f, 0.25f)
                };
                Material suit = RuntimeMaterialFactory.Create(bot.name + "_Suit", palette[i % palette.Length]);
                Material dark = RuntimeMaterialFactory.Create(bot.name + "_Trim", new Color(0.045f, 0.07f, 0.10f));
                CreatePart(bot.transform, "Armor", PrimitiveType.Capsule,
                    new Vector3(0f, 1.02f, 0f), new Vector3(0.62f, 0.66f, 0.42f), suit);
                CreatePart(bot.transform, "Helmet", PrimitiveType.Sphere,
                    new Vector3(0f, 1.70f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), dark);
                CreatePart(bot.transform, "Visor", PrimitiveType.Cube,
                    new Vector3(0f, 1.70f, 0.19f), new Vector3(0.25f, 0.09f, 0.035f),
                    RuntimeMaterialFactory.Create(bot.name + "_Visor", new Color(0.28f, 0.96f, 1f)));
                StageTwoBotCompetitor brain = bot.AddComponent<StageTwoBotCompetitor>();
                brain.Configure(this, i + 1);
                bots.Add(bot);
                kills[brain.CompetitorId] = 0;
            }

            StartupCheckpoint.Set("StageTwoArenaBotsReady");
        }

        private Vector3 FindBotSpawn(int index, int total)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float angle = (index / (float)Mathf.Max(1, total)) * Mathf.PI * 2f + Random.Range(-0.28f, 0.28f);
                float radius = Random.Range(24f, 48f);
                Vector3 candidate = (localPlayer != null ? localPlayer.position : Vector3.zero)
                    + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                candidate.y = 0f;
                if (Physics.CheckSphere(candidate + Vector3.up, 0.45f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                return candidate;
            }

            float fallbackAngle = (index + 1) * Mathf.PI * 2f / Mathf.Max(1, total);
            Vector3 fallback = (localPlayer != null ? localPlayer.position : Vector3.zero)
                + new Vector3(Mathf.Cos(fallbackAngle), 0f, Mathf.Sin(fallbackAngle)) * (30f + index * 1.5f);
            fallback.y = 0f;
            return fallback;
        }

        private static void CreatePart(Transform parent, string partName, PrimitiveType type,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public void RegisterKill(Transform attacker)
        {
            if (finished || attacker == null) return;
            StageTwoBotCompetitor bot = attacker.GetComponentInParent<StageTwoBotCompetitor>();
            string id = bot != null ? bot.CompetitorId
                : (attacker.GetComponentInParent<PlayerController>() != null ? "YOU" : string.Empty);
            if (string.IsNullOrEmpty(id)) return;
            if (!kills.ContainsKey(id)) kills[id] = 0;
            kills[id]++;
        }

        public string GetLeaderboardText()
        {
            return string.Join("\n", kills.OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key == "YOU" ? 0 : 1)
                .ThenBy(pair => pair.Key, System.StringComparer.Ordinal)
                .Take(3)
                .Select((pair, index) => (index + 1) + ". " +
                    (pair.Key == "YOU" ? "YOU" : "AI " + pair.Key.Replace("AI-", "")) + "   " + pair.Value));
        }

        public void FinishArena()
        {
            if (finished) return;
            finished = true;
            List<KeyValuePair<string, int>> ranking = kills.OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, System.StringComparer.Ordinal).ToList();
            int rank = ranking.FindIndex(pair => pair.Key == "YOU") + 1;
            LastLocalRank = rank;

            if (rank == 1)
            {
                PlayerPrefs.SetInt("PW.Stage3Unlocked", 1);
                PlayerPrefs.SetInt("PW.Stage2FirstPlaceWins", PlayerPrefs.GetInt("PW.Stage2FirstPlaceWins", 0) + 1);
                PlayerPrefs.SetInt("PW.StoreCoins", PlayerPrefs.GetInt("PW.StoreCoins", 500) + 300);
            }
            else if (rank == 2)
            {
                PlayerPrefs.SetInt("PW.Reward.VoidRifle", 1);
                PlayerPrefs.SetInt("PW.StoreCoins", PlayerPrefs.GetInt("PW.StoreCoins", 500) + 200);
            }
            else if (rank == 3)
            {
                PlayerPrefs.SetInt("PW.Reward.AlienSword", 1);
                PlayerPrefs.SetInt("PW.StoreCoins", PlayerPrefs.GetInt("PW.StoreCoins", 500) + 150);
            }
            PlayerPrefs.Save();
            StartupCheckpoint.Set("StageTwoArenaRank_" + rank);
        }

        private void ClearBots()
        {
            for (int i = 0; i < bots.Count; i++)
                if (bots[i] != null) Destroy(bots[i]);
            bots.Clear();
        }
    }
}
