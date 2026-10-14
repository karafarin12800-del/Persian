using System;
using System.Reflection;
using UnityEngine;
using PersiaWar.Unity2D5D;

/// <summary>
/// Stage-two post-match results and navigation. Kept in a separate component so
/// the existing gameplay/touch-control implementation remains untouched.
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class StageTwoResultsMenu : MonoBehaviour
{
    private PrototypeFlow flow;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle buttonStyle;
    private Texture2D pixel;
    private bool handledFinishedMatch;
    private int lastAwardedSessionId = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForLoadedScene()
    {
        AttachToCurrentScene();
    }

    private static void AttachToCurrentScene()
    {
        PrototypeFlow[] flows = FindObjectsByType<PrototypeFlow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < flows.Length; i++)
        {
            if (flows[i] != null && flows[i].GetComponent<StageTwoResultsMenu>() == null)
                flows[i].gameObject.AddComponent<StageTwoResultsMenu>();
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveFlow();
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        ResolveFlow();
    }

    private void ResolveFlow()
    {
        flow = GetComponent<PrototypeFlow>();
        if (flow == null)
            flow = FindFirstObjectByType<PrototypeFlow>(FindObjectsInactive.Include);
    }

    private static object Read(object target, string name)
    {
        if (target == null) return null;
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field != null ? field.GetValue(target) : null;
    }

    private static bool Write(object target, string name, object value)
    {
        if (target == null) return false;
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null || !field.FieldType.IsInstanceOfType(value)) return false;
        field.SetValue(target, value);
        return true;
    }

    private static void Invoke(object target, string name)
    {
        if (target == null) return;
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method != null && method.GetParameters().Length == 0) method.Invoke(target, null);
    }

    private bool IsStageTwoFinished(out GameSession session)
    {
        session = GameSession.Instance;
        if (flow == null) ResolveFlow();
        object stage = Read(flow, "stageNumber");
        return flow != null && stage is int && (int)stage >= 2 && session != null && session.IsFinished;
    }

    private void EnsureStyles()
    {
        if (pixel == null)
        {
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }
        float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.3f);
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(36 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            titleStyle.normal.textColor = new Color(1f, 0.84f, 0.42f);
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * scale), alignment = TextAnchor.MiddleCenter, wordWrap = true };
            textStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(19 * scale), fontStyle = FontStyle.Bold, wordWrap = true };
        }
    }

    private void Fill(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, pixel);
        GUI.color = old;
    }

    private void OnGUI()
    {
        GameSession session;
        if (!IsStageTwoFinished(out session)) return;

        EnsureStyles();
        // An opaque, later-drawn result panel replaces the simple legacy end overlay
        // only for Stage 2. Stage 1's extraction/next-stage flow is unaffected.
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.015f, 0.035f, 0.055f, 0.985f));

        float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.72f, 1.2f);
        float width = Mathf.Min(Screen.width - 28f * scale, 650f * scale);
        float height = Mathf.Min(Screen.height - 28f * scale, 650f * scale);
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        Fill(panel, new Color(0.045f, 0.085f, 0.12f, 1f));
        Fill(new Rect(panel.x, panel.y, panel.width, 5f * scale), session.PlayerWon
            ? new Color(0.25f, 0.83f, 0.48f, 1f)
            : new Color(0.90f, 0.25f, 0.22f, 1f));

        StageTwoArenaManager arena = flow != null ? flow.GetComponent<StageTwoArenaManager>() : null;
        int rank = arena != null ? arena.LastLocalRank : 0;
        int competitors = Mathf.Max(1, Read(flow, "stageTwoLobbyPlayers") is int n ? n : 10);
        if (rank <= 0) rank = session.PlayerWon ? 1 : competitors;
        rank = Mathf.Clamp(rank, 1, competitors);

        GUI.Label(new Rect(panel.x + 12, panel.y + 14 * scale, panel.width - 24, 48 * scale),
            session.PlayerWon ? "MATCH COMPLETE" : "MATCH OVER", titleStyle);
        GUI.Label(new Rect(panel.x + 18, panel.y + 64 * scale, panel.width - 36, 40 * scale),
            "YOUR RANK  #" + rank + "  /  " + competitors, titleStyle);
        GUI.Label(new Rect(panel.x + 18, panel.y + 108 * scale, panel.width - 36, 34 * scale),
            (session.PlayerWon ? "SURVIVOR — WELL PLAYED" : "ELIMINATED — TRY AGAIN") +
            "\nScore: " + session.Score + "     Eliminations: " + session.EnemiesDefeated, textStyle);

        int coins = rank == 1 ? 300 : (rank == 2 ? 180 : (rank == 3 ? 120 : 40));
        // Grant the match reward exactly once for this finished GameSession.
        int sessionId = session.GetInstanceID();
        if (lastAwardedSessionId != sessionId)
        {
            int balance = PlayerPrefs.GetInt("PW.StoreCoins", 500);
            PlayerPrefs.SetInt("PW.StoreCoins", balance + coins);
            PlayerPrefs.Save();
            Write(flow, "storeCoins", balance + coins);
            lastAwardedSessionId = sessionId;
        }
        GUI.Label(new Rect(panel.x + 18, panel.y + 164 * scale, panel.width - 36, 30 * scale),
            "MATCH REWARD  +" + coins + " COINS", textStyle);

        float gap = 10 * scale;
        float btnH = 46 * scale;
        float btnW = (panel.width - 44 * scale - gap) * 0.5f;
        float x1 = panel.x + 22 * scale;
        float x2 = x1 + btnW + gap;
        float y = panel.y + 214 * scale;

        if (GUI.Button(new Rect(x1, y, panel.width - 44 * scale, btnH), "PLAY AGAIN", buttonStyle))
        {
            handledFinishedMatch = true;
            session.RestartMission();
            return;
        }
        y += btnH + 10 * scale;
        if (GUI.Button(new Rect(x1, y, btnW, btnH), "CHOOSE MAP", buttonStyle))
        {
            Time.timeScale = 1f;
            Write(flow, "matchPaused", false);
            Write(flow, "startingMatch", false);
            Write(flow, "spawnChosen", false);
            Write(flow, "stageTwoShopOpen", false);
            Write(flow, "mode", Enum.Parse(Read(flow, "mode").GetType(), "DropMap"));
            // GateGameplay takes a bool; stop the previous match before changing screens.
            MethodInfo gate = flow.GetType().GetMethod("GateGameplay", BindingFlags.Instance | BindingFlags.NonPublic);
            if (gate != null) gate.Invoke(flow, new object[] { false });
            return;
        }
        if (GUI.Button(new Rect(x2, y, btnW, btnH), "CHOOSE CHARACTER", buttonStyle))
        {
            Time.timeScale = 1f;
            Write(flow, "matchPaused", false);
            Write(flow, "stageTwoShopOpen", false);
            Write(flow, "mode", Enum.Parse(Read(flow, "mode").GetType(), "HeroSelect"));
            MethodInfo gate = flow.GetType().GetMethod("GateGameplay", BindingFlags.Instance | BindingFlags.NonPublic);
            if (gate != null) gate.Invoke(flow, new object[] { false });
            return;
        }
        y += btnH + 10 * scale;
        if (GUI.Button(new Rect(x1, y, btnW, btnH), "STORE", buttonStyle))
        {
            Time.timeScale = 1f;
            Write(flow, "matchPaused", false);
            Write(flow, "mode", Enum.Parse(Read(flow, "mode").GetType(), "Store"));
            MethodInfo gate = flow.GetType().GetMethod("GateGameplay", BindingFlags.Instance | BindingFlags.NonPublic);
            if (gate != null) gate.Invoke(flow, new object[] { false });
            return;
        }
        if (GUI.Button(new Rect(x2, y, btnW, btnH), "COINS & PURCHASES", buttonStyle))
        {
            Time.timeScale = 1f;
            Write(flow, "matchPaused", false);
            Write(flow, "mode", Enum.Parse(Read(flow, "mode").GetType(), "Store"));
            MethodInfo gate = flow.GetType().GetMethod("GateGameplay", BindingFlags.Instance | BindingFlags.NonPublic);
            if (gate != null) gate.Invoke(flow, new object[] { false });
            return;
        }
        y += btnH + 10 * scale;
        if (GUI.Button(new Rect(x1, y, panel.width - 44 * scale, btnH), "MAIN MENU", buttonStyle))
        {
            Time.timeScale = 1f;
            Write(flow, "matchPaused", false);
            Write(flow, "stageTwoShopOpen", false);
            Write(flow, "mode", Enum.Parse(Read(flow, "mode").GetType(), "MainMenu"));
            MethodInfo gate = flow.GetType().GetMethod("GateGameplay", BindingFlags.Instance | BindingFlags.NonPublic);
            if (gate != null) gate.Invoke(flow, new object[] { false });
        }
    }
}
