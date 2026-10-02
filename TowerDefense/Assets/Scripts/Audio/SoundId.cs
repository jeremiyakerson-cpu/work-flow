namespace TowerDefense.Audio
{
    /// <summary>Every sound effect the game plays. All are synthesized at startup (no audio files).</summary>
    public enum SoundId
    {
        ArrowShot,
        MagicZap,
        CannonShot,
        Frost,
        Poison,
        EnemyHit,
        EnemyDeath,
        BossDeath,
        Coin,
        LifeLost,
        WaveStart,
        BossWarning,
        Victory,
        Defeat,
        UiClick,
        Build,
        Upgrade,
        Sell,
    }

    /// <summary>Looping background tracks.</summary>
    public enum MusicTrack
    {
        None,
        Menu,
        Battle,
    }

    /// <summary>Volume group a sound belongs to (mapped to the settings sliders).</summary>
    public enum SoundCategory
    {
        Sfx,
        Ui,
        Music,
    }

    /// <summary>Mixing and throttling rules for one sound.</summary>
    public struct SoundInfo
    {
        public SoundId Id;
        public SoundCategory Category;
        /// <summary>Linear gain applied on top of the category volume.</summary>
        public float Volume;
        /// <summary>Minimum real-time seconds between two starts of this sound.</summary>
        public float MinInterval;
        /// <summary>Maximum overlapping instances of this sound.</summary>
        public int MaxVoices;
        /// <summary>Random pitch spread, e.g. 0.05 = +-5 %.</summary>
        public float PitchJitter;
        /// <summary>Higher wins when the voice pool is full (stingers beat tower shots).</summary>
        public int Priority;
    }

    /// <summary>
    /// Per-sound mix/throttle table. Tuned so that 3x game speed with a full
    /// map of towers stays readable: frequent combat sounds are rate-limited
    /// and capped in voices, rare stingers are never dropped.
    /// </summary>
    public static class SoundCatalog
    {
        public static readonly int Count = System.Enum.GetValues(typeof(SoundId)).Length;

        private static readonly SoundInfo[] Table = Build();

        public static SoundInfo Get(SoundId id) => Table[(int)id];

        private static SoundInfo[] Build()
        {
            var t = new SoundInfo[System.Enum.GetValues(typeof(SoundId)).Length];
            Set(t, SoundId.ArrowShot,   SoundCategory.Sfx, 0.45f, 0.06f, 3, 0.08f, 10);
            Set(t, SoundId.MagicZap,    SoundCategory.Sfx, 0.40f, 0.08f, 3, 0.06f, 10);
            Set(t, SoundId.CannonShot,  SoundCategory.Sfx, 0.65f, 0.12f, 2, 0.06f, 20);
            Set(t, SoundId.Frost,       SoundCategory.Sfx, 0.40f, 0.12f, 2, 0.05f, 10);
            Set(t, SoundId.Poison,      SoundCategory.Sfx, 0.40f, 0.12f, 2, 0.08f, 10);
            Set(t, SoundId.EnemyHit,    SoundCategory.Sfx, 0.25f, 0.07f, 2, 0.12f, 0);
            Set(t, SoundId.EnemyDeath,  SoundCategory.Sfx, 0.45f, 0.06f, 3, 0.10f, 30);
            Set(t, SoundId.BossDeath,   SoundCategory.Sfx, 1.00f, 0.50f, 1, 0.00f, 100);
            Set(t, SoundId.Coin,        SoundCategory.Sfx, 0.35f, 0.10f, 2, 0.03f, 40);
            Set(t, SoundId.LifeLost,    SoundCategory.Sfx, 0.80f, 0.25f, 1, 0.00f, 90);
            Set(t, SoundId.WaveStart,   SoundCategory.Sfx, 0.80f, 1.00f, 1, 0.00f, 95);
            Set(t, SoundId.BossWarning, SoundCategory.Sfx, 0.90f, 1.50f, 1, 0.00f, 100);
            Set(t, SoundId.Victory,     SoundCategory.Sfx, 0.90f, 1.00f, 1, 0.00f, 100);
            Set(t, SoundId.Defeat,      SoundCategory.Sfx, 0.90f, 1.00f, 1, 0.00f, 100);
            Set(t, SoundId.UiClick,     SoundCategory.Ui,  0.50f, 0.04f, 2, 0.02f, 80);
            Set(t, SoundId.Build,       SoundCategory.Sfx, 0.70f, 0.10f, 2, 0.04f, 80);
            Set(t, SoundId.Upgrade,     SoundCategory.Sfx, 0.70f, 0.10f, 2, 0.00f, 80);
            Set(t, SoundId.Sell,        SoundCategory.Sfx, 0.65f, 0.10f, 2, 0.00f, 80);
            return t;
        }

        private static void Set(SoundInfo[] t, SoundId id, SoundCategory cat, float vol, float minInterval, int maxVoices, float jitter, int priority)
        {
            t[(int)id] = new SoundInfo
            {
                Id = id, Category = cat, Volume = vol, MinInterval = minInterval,
                MaxVoices = maxVoices, PitchJitter = jitter, Priority = priority,
            };
        }
    }
}
