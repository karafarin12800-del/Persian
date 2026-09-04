using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrototypeMatchFlowBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartPrototypeFlow()
    {
        if (SceneManager.GetActiveScene().name != "PersiaWarPrototype") return;
        if (Object.FindFirstObjectByType<PrototypeMatchFlow>() != null) return;
        GameObject root = new GameObject("PrototypeMatchFlow");
        root.AddComponent<PrototypeMatchFlow>();
    }
}
