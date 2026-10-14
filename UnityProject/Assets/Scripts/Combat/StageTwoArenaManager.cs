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

                // Use the same six selectable hero designs as the local player instead
                // of unrelated capsule/helmet placeholders. Each offline competitor
                // selects one hero silhouette and applies its matching player palette.
                int heroChoice = Random.Range(0, 6);
                StylizedCharacterVisual visual = StylizedCharacterVisual.Attach(bot.transform, true, 1);
                if (visual != null)
                    visual.ConfigurePlayerHero(heroChoice);

                StageTwoBotCompetitor brain = bot.AddComponent<StageTwoBotCompetitor>();
                brain.Configure(this, i + 1);
                bots.Add(bot);
                kills[brain.CompetitorId] = 0;
            }

            StartupCheckpoint.Set("StageTwoArenaBotsReady");
        }

        private Vector3 FindBotSpawn(int index, int total)
        {
            // Assign each competitor a distinct map cell, then validate spacing from
            // the local player, existing competitors, aliens and solid world objects.
            int columns = Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, total)));
            int rows = Mathf.CeilToInt(total / (float)Mathf.Max(1, columns));
            int column = index % columns;
            int row = index / columns;
            float cellWidth = 168f / columns;
            float cellDepth = 168f / rows;

            for (int attempt = 0; attempt < 48; attempt++)
            {
                Vector3 candidate = new Vector3(
                    -84f + (column + Random.Range(0.12f, 0.88f)) * cellWidth,
                    0f,
                    -84f + (row + Random.Range(0.12f, 0.88f)) * cellDepth);
                if (IsBotSpawnClear(candidate, 14f, 10f, 8f))
                    return candidate;
            }

            // If the assigned cell is blocked or too close to the player, search
            // other map-wide positions without collapsing the lobby into one cluster.
            for (int attempt = 0; attempt < 100; attempt++)
            {
                Vector3 candidate = new Vector3(
                    Random.Range(-82f, 82f), 0f, Random.Range(-82f, 82f));
                if (IsBotSpawnClear(candidate, 14f, 10f, 8f))
                    return candidate;
            }

            // Last-resort fallback stays in the assigned cell; all normal placements
            // have already been checked against obstacles and other actors.
            return new Vector3(
                -84f + (column + 0.5f) * cellWidth,
                0f,
                -84f + (row + 0.5f) * cellDepth);
        }

        private bool IsBotSpawnClear(Vector3 candidate, float playerSpacing,
            float competitorSpacing, float enemySpacing)
        {
            if (Physics.CheckSphere(candidate + Vector3.up, 0.45f, ~0,
                QueryTriggerInteraction.Ignore))
                return false;

            if (localPlayer != null &&
                Vector3.Distance(candidate, localPlayer.position) < playerSpacing)
                return false;

            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i] != null &&
                    Vector3.Distance(candidate, bots[i].transform.position) < competitorSpacing)
                    return false;
            }

            EnemyChase[] enemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null &&
                    Vector3.Distance(candidate, enemies[i].transform.position) < enemySpacing)
                    return false;
            }

            return true;
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
                .ThenBy(pair => pair.Key == "YOU" ? 0 : 1)
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
