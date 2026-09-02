using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [Header("Match")]
    [SerializeField] private bool useMissionTimer = false;
    [SerializeField] private float missionTime = 300f;

    public int EnemiesDefeated { get; private set; }
    public int Score { get; private set; }
    public float TimeRemaining { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }
    public bool PlayerWon { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        TimeRemaining = missionTime;
        IsRunning = true;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!IsRunning || IsFinished || !useMissionTimer) return;
        TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
        if (TimeRemaining <= 0f) EndMission(false);
    }

    public void RegisterEnemyDefeated()
    {
        if (IsFinished) return;
        EnemiesDefeated++;
        Score += 10;
    }

    public void AddScore(int value)
    {
        if (IsFinished) return;
        Score += Mathf.Max(0, value);
    }

    public void EndMission(bool won)
    {
        if (IsFinished) return;
        IsFinished = true;
        PlayerWon = won;
        IsRunning = false;
        Time.timeScale = 0f;
    }

    public void RestartMission()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }
}