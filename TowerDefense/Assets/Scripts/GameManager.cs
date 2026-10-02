using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central hub: gold, lives, game-over state, and events the UI subscribes to.
/// Singleton - drop one instance in your scene.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Starting Resources")]
    public int startingGold = 150;
    public int startingLives = 20;

    public int Gold { get; private set; }
    public int Lives { get; private set; }
    public bool IsGameOver { get; private set; } = false;

    [Header("UI Events - hook these up in the Inspector")]
    public UnityEvent<int> onGoldChanged;
    public UnityEvent<int> onLivesChanged;
    public UnityEvent<int> onWaveStarted;
    public UnityEvent<int> onWaveCleared;
    public UnityEvent onGameOver;

    private void Awake()
    {
        Instance = this;
        Gold = startingGold;
        Lives = startingLives;
    }

    // ---------- Economy ----------
    public void AddGold(int amount)
    {
        Gold += amount;
        onGoldChanged?.Invoke(Gold);
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        onGoldChanged?.Invoke(Gold);
        return true;
    }

    // ---------- Lives / Game Over ----------
    public void DamageBase(int amount)
    {
        if (IsGameOver) return;
        Lives -= amount;
        onLivesChanged?.Invoke(Lives);
        if (Lives <= 0)
            TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        IsGameOver = true;
        Time.timeScale = 0f;
        onGameOver?.Invoke();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- Wave event forwarding (called by WaveManager) ----------
    public void OnWaveStarted(int waveNumber) => onWaveStarted?.Invoke(waveNumber);
    public void OnWaveCleared(int waveNumber) => onWaveCleared?.Invoke(waveNumber);

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
