namespace TowerDefense.Platform
{
    /// <summary>
    /// Maps gameplay moments to haptics: build/upgrade (medium), sell (light),
    /// life lost (warning), boss wave and boss kill (heavy), victory (success),
    /// defeat (error). All go through <see cref="Haptics"/>, so the settings
    /// toggle and rate limits apply. <see cref="AppLifecycle"/> binds it
    /// automatically; call <see cref="Unbind"/> to drop every subscription.
    /// </summary>
    public sealed class GameplayHaptics
    {
        private GameManager boundGm;
        private WaveManager boundWm;

        public bool IsBound { get; private set; }
        public GameManager BoundGameManager => boundGm;

        public void BindToGameplay(GameManager gameManager, WaveManager waveManager)
        {
            Unbind();
            boundGm = gameManager;
            boundWm = waveManager;
            IsBound = true;
            if (boundGm != null)
            {
                boundGm.GameOverTriggered += HandleDefeat;
                boundGm.VictoryTriggered += HandleVictory;
            }
            if (boundWm != null) boundWm.WaveSpawning += HandleWaveSpawning;
            Enemy.AnyLeaked += HandleLeaked;
            Enemy.AnyDied += HandleDied;
            Tower.AnyUpgraded += HandleUpgraded;
            TowerPlacement.AnySlotChanged += HandleSlotChanged;
            Haptics.Prepare();
        }

        /// <summary>Attach a WaveManager that appeared after binding.</summary>
        public void AttachWaveManager(WaveManager waveManager)
        {
            if (!ReferenceEquals(boundWm, null)) boundWm.WaveSpawning -= HandleWaveSpawning;
            boundWm = waveManager;
            if (boundWm != null) boundWm.WaveSpawning += HandleWaveSpawning;
        }

        public bool HasWaveManager => boundWm != null;

        public void Unbind()
        {
            Enemy.AnyLeaked -= HandleLeaked;
            Enemy.AnyDied -= HandleDied;
            Tower.AnyUpgraded -= HandleUpgraded;
            TowerPlacement.AnySlotChanged -= HandleSlotChanged;
            if (!ReferenceEquals(boundGm, null))
            {
                boundGm.GameOverTriggered -= HandleDefeat;
                boundGm.VictoryTriggered -= HandleVictory;
            }
            if (!ReferenceEquals(boundWm, null)) boundWm.WaveSpawning -= HandleWaveSpawning;
            boundGm = null;
            boundWm = null;
            IsBound = false;
        }

        private static void HandleLeaked(Enemy e) => Haptics.Play(HapticFeedback.Warning);
        private static void HandleDied(Enemy e)
        {
            if (e != null && e.IsBoss) Haptics.Play(HapticFeedback.Heavy);
        }
        private static void HandleUpgraded(Tower t) => Haptics.Play(HapticFeedback.Medium);
        private static void HandleSlotChanged(TowerPlacement slot) =>
            Haptics.Play(slot != null && slot.IsOccupied ? HapticFeedback.Medium : HapticFeedback.Light);
        private static void HandleWaveSpawning(int wave, bool isBoss)
        {
            if (isBoss) Haptics.Play(HapticFeedback.Heavy);
        }
        private static void HandleVictory() => Haptics.Play(HapticFeedback.Success);
        private static void HandleDefeat() => Haptics.Play(HapticFeedback.Error);
    }
}
