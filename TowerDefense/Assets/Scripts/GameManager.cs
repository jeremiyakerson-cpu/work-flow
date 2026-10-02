using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central hub: gold, lives, game-over/victory state, pause and game speed, and
/// events the UI subscribes to. Singleton - drop one instance in your scene.
/// Both UnityEvents (Inspector wiring) and C# events (code wiring) fire.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Starting Resources")]
    public int startingGold = 150;
    public int startingLives = 20;

    public int Gold { get; private set; }
    public int Lives { get; private set; }
    public int StartingLives { get; private set; }
    public bool IsGameOver { get; private set; } = false;
    public bool IsVictory { get; private set; } = false;
    public bool IsPaused { get; private set; } = false;
    public float GameSpeed { get; private set; } = 1f;

    [Header("UI Events - hook these up in the Inspector")]
    public UnityEvent<int> onGoldChanged;
    public UnityEvent<int> onLivesChanged;
    public UnityEvent<int> onWaveStarted;
    public UnityEvent<int> onWaveCleared;
    public UnityEvent onGameOver;
    public UnityEvent onVictory;

    // Code-side mirrors of the UnityEvents above.
    public event Action<int> GoldChanged;
    public event Action<int> LivesChanged;
    public event Action<int> WaveStarted;
    public event Action<int> WaveCleared;
    public event Action GameOverTriggered;
    public event Action VictoryTriggered;
    public event Action<bool> PausedChanged;
    public event Action<float> SpeedChanged;

    private void Awake()
    {
        Instance = this;
        Gold = startingGold;
        Lives = startingLives;
        StartingLives = startingLives;
        Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Reset economy for a level (called by the level loader before waves start).</summary>
    public void Configure(int gold, int lives)
    {
        Gold = gold;
        Lives = lives;
        StartingLives = lives;
        IsGameOver = false;
        IsVictory = false;
        onGoldChanged?.Invoke(Gold);
        GoldChanged?.Invoke(Gold);
        onLivesChanged?.Invoke(Lives);
        LivesChanged?.Invoke(Lives);
    }

    // ---------- Economy ----------
    public void AddGold(int amount)
    {
        Gold += amount;
        onGoldChanged?.Invoke(Gold);
        GoldChanged?.Invoke(Gold);
    }

    public bool CanAfford(int amount) => Gold >= amount;

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        onGoldChanged?.Invoke(Gold);
        GoldChanged?.Invoke(Gold);
        return true;
    }

    // ---------- Lives / Game Over ----------
    public void DamageBase(int amount)
    {
        if (IsGameOver || IsVictory) return;
        Lives = Mathf.Max(0, Lives - amount);
        onLivesChanged?.Invoke(Lives);
        LivesChanged?.Invoke(Lives);
        if (Lives <= 0)
            TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        IsGameOver = true;
        Time.timeScale = 0f;
        onGameOver?.Invoke();
        GameOverTriggered?.Invoke();
    }

    /// <summary>Called by WaveManager once the level's final wave is cleared.</summary>
    public void TriggerVictory()
    {
        if (IsGameOver || IsVictory) return;
        IsVictory = true;
        Time.timeScale = 0f;
        onVictory?.Invoke();
        VictoryTriggered?.Invoke();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- Pause / speed ----------
    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;
        PausedChanged?.Invoke(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        if (!IsGameOver && !IsVictory) Time.timeScale = GameSpeed;
        PausedChanged?.Invoke(false);
    }

    public void TogglePause()
    {
        if (IsPaused) Resume(); else Pause();
    }

    /// <summary>1x / 2x / 3x fast-forward. Applied immediately unless paused or finished.</summary>
    public void SetGameSpeed(float speed)
    {
        GameSpeed = Mathf.Clamp(speed, 0.25f, 4f);
        if (!IsPaused && !IsGameOver && !IsVictory) Time.timeScale = GameSpeed;
        SpeedChanged?.Invoke(GameSpeed);
    }

    // ---------- Wave event forwarding (called by WaveManager) ----------
    public void OnWaveStarted(int waveNumber)
    {
        onWaveStarted?.Invoke(waveNumber);
        WaveStarted?.Invoke(waveNumber);
    }

    public void OnWaveCleared(int waveNumber)
    {
        onWaveCleared?.Invoke(waveNumber);
        WaveCleared?.Invoke(waveNumber);
    }

    // ---------- Enemy event forwarding (called by Enemy.cs) ----------
    public void OnEnemyKilled(Enemy e)
    {
        if (WaveManager.Instance != null) WaveManager.Instance.NotifyEnemyGone();
    }

    public void OnEnemyLeaked(Enemy e)
    {
        if (WaveManager.Instance != null) WaveManager.Instance.NotifyEnemyGone();
    }
}
