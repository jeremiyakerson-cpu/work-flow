using System;
using TowerDefense.Core;
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
    /// <summary>True once the level is won or lost.</summary>
    public bool IsFinished => IsGameOver || IsVictory;
    /// <summary>
    /// Level result in stars: 1-3 after a victory (from lives kept vs StartingLives,
    /// KR thresholds), 0 after a loss or while still playing.
    /// </summary>
    public int Stars => IsVictory ? StarRating.Rate(Lives, StartingLives) : 0;

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
        if (Instance != this) return;
        Instance = null;
        // Don't leave the next scene (menus) frozen after a game over/victory/pause.
        Time.timeScale = 1f;
    }

    /// <summary>Reset economy for a level (called by the level loader before waves start). Normal difficulty, exact values.</summary>
    public void Configure(int gold, int lives)
    {
        ConfigureExact(gold, lives, DifficultyMode.Normal);
    }

    /// <summary>
    /// Reset economy for a level on a difficulty: <paramref name="baseGold"/> and
    /// <paramref name="baseLives"/> are the level's authored (Normal) values and are
    /// scaled by the difficulty's rules (lives never below 1; Impossible = 1 life).
    /// Pair with WaveManager.ApplyLevel(level, mode) so enemies scale too.
    /// </summary>
    public void Configure(int baseGold, int baseLives, DifficultyMode mode)
    {
        DifficultyMode m = Difficulty.IsDefined(mode) ? mode : DifficultyMode.Normal;
        DifficultyRules rules = Difficulty.Rules(m);
        ConfigureExact(rules.ApplyStartingGold(baseGold), rules.ApplyLives(baseLives), m);
    }

    /// <summary>Difficulty the current level was configured with (Normal unless Configure got a mode).</summary>
    public DifficultyMode CurrentDifficulty { get; private set; } = DifficultyMode.Normal;

    /// <summary>Rules of <see cref="CurrentDifficulty"/>.</summary>
    public DifficultyRules CurrentDifficultyRules => Difficulty.Rules(CurrentDifficulty);

    private void ConfigureExact(int gold, int lives, DifficultyMode mode)
    {
        CurrentDifficulty = mode;
        Gold = gold;
        Lives = lives;
        StartingLives = lives;
        IsGameOver = false;
        IsVictory = false;
        ApplyTimeScale(); // un-freeze if a previous level ended without a scene reload
        onGoldChanged?.Invoke(Gold);
        GoldChanged?.Invoke(Gold);
        onLivesChanged?.Invoke(Lives);
        LivesChanged?.Invoke(Lives);
    }

    // ---------- Economy ----------
    public void AddGold(int amount)
    {
        if (amount == 0) return;
        // Negative amounts are allowed (penalties) but gold never goes below 0 or overflows.
        Gold = (int)Math.Max(0L, Math.Min((long)Gold + amount, EconomyRules.MaxGold));
        onGoldChanged?.Invoke(Gold);
        GoldChanged?.Invoke(Gold);
    }

    public bool CanAfford(int amount) => amount <= 0 || Gold >= amount;

    /// <summary>Spend gold if affordable. Negative amounts are rejected (use AddGold to give gold).</summary>
    public bool SpendGold(int amount)
    {
        if (amount < 0 || Gold < amount) return false;
        if (amount == 0) return true;
        Gold -= amount;
        onGoldChanged?.Invoke(Gold);
        GoldChanged?.Invoke(Gold);
        return true;
    }

    // ---------- Lives / Game Over ----------
    public void DamageBase(int amount)
    {
        if (IsGameOver || IsVictory || amount <= 0) return;
        Lives = Mathf.Max(0, Lives - amount);
        onLivesChanged?.Invoke(Lives);
        LivesChanged?.Invoke(Lives);
        if (Lives <= 0)
            TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        if (IsGameOver || IsVictory) return;
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
        IsPaused = false;
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
        ApplyTimeScale();
        PausedChanged?.Invoke(false);
    }

    public void TogglePause()
    {
        if (IsPaused) Resume(); else Pause();
    }

    /// <summary>1x / 2x / 3x fast-forward. Applied immediately unless paused or finished.</summary>
    public void SetGameSpeed(float speed)
    {
        GameSpeed = speed > 0f ? Mathf.Clamp(speed, 0.25f, 4f) : 1f; // NaN/0 would freeze or break time
        ApplyTimeScale();
        SpeedChanged?.Invoke(GameSpeed);
    }

    // Single place that decides Time.timeScale: frozen while paused or finished.
    private void ApplyTimeScale()
    {
        Time.timeScale = IsPaused || IsGameOver || IsVictory ? 0f : GameSpeed;
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
    // Wave bookkeeping is no longer done here: enemies unregister themselves from
    // WaveManager exactly once when they die, leak or are removed.
    public void OnEnemyKilled(Enemy e) { }

    public void OnEnemyLeaked(Enemy e) { }
}
