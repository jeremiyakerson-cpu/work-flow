using System.Threading.Tasks;
using TowerDefense.Persistence;
using UnityEngine;

namespace TowerDefense.Audio
{
    /// <summary>
    /// Plays the synthesized SFX and music. Owns a pool of 2D AudioSources,
    /// per-sound throttling (real time, so 3x speed stays readable), small
    /// pitch variance, category volumes from <see cref="GameSettings"/>, and a
    /// music crossfade on unscaled time (works while paused / after game over).
    ///
    /// All samples are rendered on a worker thread at startup (pure C#, see
    /// SfxRecipes / MusicComposer); clips are created on the main thread as
    /// soon as the render finishes. Requests before that are silently dropped.
    ///
    /// Gameplay wiring: <see cref="BindToGameplay"/> subscribes to the contract
    /// events, <see cref="Unbind"/> removes every subscription (static events
    /// included). With <see cref="autoBindGameplay"/> on (default) the manager
    /// binds itself whenever GameManager.Instance changes.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        public const int SfxSampleRate = 44100;
        public const int MusicSampleRate = 32000;

        public static AudioManager Instance { get; private set; }

        [Tooltip("Simultaneous sound effects. Extra requests steal the lowest-priority voice.")]
        [SerializeField] private int sfxVoices = 16;
        [SerializeField] private float defaultCrossfadeSeconds = 1.2f;
        [Tooltip("Bind to GameManager.Instance / WaveManager.Instance automatically when they appear.")]
        public bool autoBindGameplay = true;
        [Tooltip("Switch to battle music on bind and back to menu music when the GameManager goes away.")]
        public bool autoMusic = true;

        public bool IsReady { get; private set; }
        public MusicTrack CurrentMusic { get; private set; } = MusicTrack.None;
        public bool IsBound => bound;

        /// <summary>Linear 0..1 for sound effects and UI sounds.</summary>
        public float SfxVolume { get => sfxVolume; set => sfxVolume = Mathf.Clamp01(value); }
        /// <summary>Linear 0..1 for music; applied to playing music immediately.</summary>
        public float MusicVolume { get => musicVolume; set => musicVolume = Mathf.Clamp01(value); }

        private float sfxVolume = SettingsData.DefaultSfxVolume;
        private float musicVolume = SettingsData.DefaultMusicVolume;

        // ---- clips
        private AudioClip[] sfxClips;
        private AudioClip menuClip, battleClip;
        private Task<RenderedLibrary> renderTask;

        private sealed class RenderedLibrary
        {
            public float[][] Sfx;
            public float[] Menu;
            public float[] Battle;
        }

        // ---- voices
        private AudioSource[] voices;
        private int[] voicePriority;
        private double[] voiceEnd;
        private readonly SoundThrottle throttle = new SoundThrottle();

        // ---- music crossfade (A/B)
        private AudioSource musicA, musicB;
        private AudioSource musicCurrent, musicPrevious;
        private float fadeDuration, fadeElapsed;
        private MusicTrack pendingMusic = MusicTrack.None;

        // ---- bindings
        private GameSettings boundSettings;
        private GameManager boundGm;
        private WaveManager boundWm;
        private bool bound;
        private int lastGold;
        private bool pendingCoin;
        private int sellFrame = -1;
        private float listenerCheckAt;
        private AudioListener ownListener;

        /// <summary>The live instance, created on a persistent "[Audio]" object if needed.</summary>
        public static AudioManager EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[Audio]");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            sfxVoices = Mathf.Clamp(sfxVoices, 4, 32);
            voices = new AudioSource[sfxVoices];
            voicePriority = new int[sfxVoices];
            voiceEnd = new double[sfxVoices];
            for (int i = 0; i < sfxVoices; i++) voices[i] = CreateSource(false);
            musicA = CreateSource(true);
            musicB = CreateSource(true);

            StartRender();
        }

        private AudioSource CreateSource(bool music)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = music;
            s.spatialBlend = 0f;           // 2D: the listener position is irrelevant
            s.priority = music ? 0 : 128;  // Unity: 0 = most important (never virtualize music)
            s.ignoreListenerPause = music;
            s.volume = 0f;
            return s;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Unbind();
            UnbindSettings();
            Instance = null;
        }

        // ------------------------------------------------------------------ rendering

        private void StartRender()
        {
            // Pure managed math: safe off the main thread. AudioClip calls stay on the main thread.
            renderTask = Task.Run(() =>
            {
                var lib = new RenderedLibrary { Sfx = new float[SoundCatalog.Count][] };
                for (int i = 0; i < lib.Sfx.Length; i++) lib.Sfx[i] = SfxRecipes.Render((SoundId)i, SfxSampleRate);
                lib.Menu = MusicComposer.Render(MusicTrack.Menu, MusicSampleRate);
                lib.Battle = MusicComposer.Render(MusicTrack.Battle, MusicSampleRate);
                return lib;
            });
        }

        private void FinishRenderIfDone()
        {
            if (IsReady || renderTask == null || !renderTask.IsCompleted) return;
            Task<RenderedLibrary> task = renderTask;
            renderTask = null;
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("[Audio] Synthesis failed; the game runs silent. " + task.Exception);
                return;
            }
            RenderedLibrary lib = task.Result;
            sfxClips = new AudioClip[lib.Sfx.Length];
            for (int i = 0; i < lib.Sfx.Length; i++) sfxClips[i] = MakeClip(((SoundId)i).ToString(), lib.Sfx[i], SfxSampleRate);
            menuClip = MakeClip("Music_Menu", lib.Menu, MusicSampleRate);
            battleClip = MakeClip("Music_Battle", lib.Battle, MusicSampleRate);
            IsReady = true;

            if (pendingMusic != MusicTrack.None)
            {
                MusicTrack t = pendingMusic;
                pendingMusic = MusicTrack.None;
                CurrentMusic = MusicTrack.None;
                PlayMusic(t);
            }
        }

        private static AudioClip MakeClip(string clipName, float[] samples, int rate)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ------------------------------------------------------------------ settings

        /// <summary>Follow the saved volume sliders. Pass SaveRuntime.Service.Settings.</summary>
        public void BindSettings(GameSettings settings)
        {
            UnbindSettings();
            boundSettings = settings;
            if (settings == null) return;
            settings.MusicVolumeChanged += HandleMusicVolume;
            settings.SfxVolumeChanged += HandleSfxVolume;
            MusicVolume = settings.GetMusicVolume();
            SfxVolume = settings.GetSfxVolume();
        }

        public void UnbindSettings()
        {
            if (boundSettings == null) return;
            boundSettings.MusicVolumeChanged -= HandleMusicVolume;
            boundSettings.SfxVolumeChanged -= HandleSfxVolume;
            boundSettings = null;
        }

        private void HandleMusicVolume(float v) => MusicVolume = v;
        private void HandleSfxVolume(float v) => SfxVolume = v;

        // ------------------------------------------------------------------ playback

        /// <summary>Play a sound effect if its throttle allows it. Returns true if it started.</summary>
        public bool Play(SoundId id, float volumeScale = 1f)
        {
            if (!IsReady || sfxVolume <= 0f || volumeScale <= 0f) return false;
            SoundInfo info = SoundCatalog.Get(id);
            AudioClip clip = sfxClips[(int)id];
            if (clip == null) return false;

            float pitch = 1f + (info.PitchJitter > 0f ? UnityEngine.Random.Range(-info.PitchJitter, info.PitchJitter) : 0f);
            double now = Time.realtimeSinceStartupAsDouble;
            double duration = clip.length / pitch;

            int voice = PickVoice(info.Priority, now);
            if (voice < 0) return false;
            if (!throttle.TryStart(id, now, duration)) return false;

            AudioSource src = voices[voice];
            src.Stop();
            src.clip = clip;
            src.pitch = pitch;
            src.volume = Mathf.Clamp01(info.Volume * sfxVolume * volumeScale);
            src.Play();
            voicePriority[voice] = info.Priority;
            voiceEnd[voice] = now + duration;
            return true;
        }

        /// <summary>Convenience for buttons.</summary>
        public void PlayUiClick() => Play(SoundId.UiClick);

        /// <summary>A free voice, else the lowest-priority (then oldest) one if the new sound outranks it.</summary>
        private int PickVoice(int priority, double now)
        {
            int victim = -1;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voiceEnd[i] <= now) return i;
                if (victim < 0 || voicePriority[i] < voicePriority[victim] ||
                    (voicePriority[i] == voicePriority[victim] && voiceEnd[i] < voiceEnd[victim]))
                    victim = i;
            }
            return victim >= 0 && voicePriority[victim] < priority ? victim : -1;
        }

        /// <summary>Crossfade to <paramref name="track"/> (None = fade out). Negative fade = default.</summary>
        public void PlayMusic(MusicTrack track, float fadeSeconds = -1f)
        {
            if (fadeSeconds < 0f) fadeSeconds = defaultCrossfadeSeconds;
            if (track == CurrentMusic && (track == MusicTrack.None || (musicCurrent != null && musicCurrent.isPlaying))) return;
            CurrentMusic = track;

            if (!IsReady)
            {
                pendingMusic = track;
                return;
            }

            AudioClip clip = track == MusicTrack.Menu ? menuClip : track == MusicTrack.Battle ? battleClip : null;

            // Retire whatever is fading out; the current track becomes the outgoing one.
            AudioSource incoming = musicCurrent == musicA ? musicB : musicA;
            if (musicPrevious != null && musicPrevious != musicCurrent) musicPrevious.Stop();
            musicPrevious = musicCurrent;
            musicCurrent = clip != null ? incoming : null;

            if (musicCurrent != null)
            {
                musicCurrent.Stop();
                musicCurrent.clip = clip;
                musicCurrent.volume = 0f;
                musicCurrent.Play();
            }
            fadeDuration = Mathf.Max(0.01f, fadeSeconds);
            fadeElapsed = 0f;
        }

        public void StopMusic(float fadeSeconds = -1f) => PlayMusic(MusicTrack.None, fadeSeconds);

        private void UpdateMusicFade()
        {
            if (musicCurrent == null && musicPrevious == null) return;
            fadeElapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(fadeElapsed / fadeDuration);
            if (musicCurrent != null) musicCurrent.volume = k * musicVolume;
            if (musicPrevious != null)
            {
                musicPrevious.volume = (1f - k) * musicVolume;
                if (k >= 1f)
                {
                    musicPrevious.Stop();
                    musicPrevious = null;
                }
            }
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            if (Instance != this) return; // duplicate pending destruction
            FinishRenderIfDone();
            UpdateMusicFade();
            if (autoBindGameplay) AutoBind();

            if (Time.unscaledTime >= listenerCheckAt)
            {
                listenerCheckAt = Time.unscaledTime + 1f;
                EnsureSingleListener();
            }
        }

        private void LateUpdate()
        {
            // Coin is decided at the end of the frame so selling a tower (gold up
            // + slot emptied in the same frame) plays only the sell sound.
            if (!pendingCoin) return;
            pendingCoin = false;
            if (sellFrame != Time.frameCount) Play(SoundId.Coin);
        }

        private void AutoBind()
        {
            GameManager gm = GameManager.Instance;
            if (bound && boundGm == null) // bound GameManager was destroyed (scene change)
            {
                Unbind();
                if (gm == null && autoMusic) PlayMusic(MusicTrack.Menu);
            }
            if (gm != null && (!bound || !ReferenceEquals(gm, boundGm)))
            {
                BindToGameplay(gm, WaveManager.Instance);
            }
            else if (bound && boundWm == null && WaveManager.Instance != null)
            {
                boundWm = WaveManager.Instance;
                boundWm.WaveSpawning += HandleWaveSpawning;
            }
            else if (!bound && gm == null && autoMusic && CurrentMusic == MusicTrack.None && IsReady)
            {
                PlayMusic(MusicTrack.Menu);
            }
        }

        /// <summary>
        /// Our sources are 2D, but Unity needs exactly one AudioListener to output
        /// anything. Add one here if the scene has none; disable ours if the
        /// camera brings its own (avoids the "2 audio listeners" warning).
        /// </summary>
        private void EnsureSingleListener()
        {
            AudioListener[] all = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            int others = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] != ownListener && all[i].enabled) others++;
            if (others == 0)
            {
                if (ownListener == null) ownListener = gameObject.AddComponent<AudioListener>();
                ownListener.enabled = true;
            }
            else if (ownListener != null && ownListener.enabled)
            {
                ownListener.enabled = false;
            }
        }

        // ------------------------------------------------------------------ gameplay events

        /// <summary>
        /// Subscribe to gameplay events. Replaces any previous binding.
        /// Either argument may be null (e.g. a sandbox scene without waves).
        /// </summary>
        public void BindToGameplay(GameManager gameManager, WaveManager waveManager)
        {
            Unbind();
            boundGm = gameManager;
            boundWm = waveManager;
            bound = true;

            if (boundGm != null)
            {
                boundGm.GoldChanged += HandleGoldChanged;
                boundGm.WaveStarted += HandleWaveStarted;
                boundGm.GameOverTriggered += HandleGameOver;
                boundGm.VictoryTriggered += HandleVictory;
                lastGold = boundGm.Gold;
            }
            if (boundWm != null) boundWm.WaveSpawning += HandleWaveSpawning;

            Tower.AnyFired += HandleTowerFired;
            Tower.AnyUpgraded += HandleTowerUpgraded;
            TowerPlacement.AnySlotChanged += HandleSlotChanged;
            Enemy.AnyDamaged += HandleEnemyDamaged;
            Enemy.AnyDied += HandleEnemyDied;
            Enemy.AnyLeaked += HandleEnemyLeaked;

            throttle.Reset();
            if (autoMusic) PlayMusic(MusicTrack.Battle);
        }

        /// <summary>Remove every gameplay subscription, static events included. Safe to call repeatedly.</summary>
        public void Unbind()
        {
            Tower.AnyFired -= HandleTowerFired;
            Tower.AnyUpgraded -= HandleTowerUpgraded;
            TowerPlacement.AnySlotChanged -= HandleSlotChanged;
            Enemy.AnyDamaged -= HandleEnemyDamaged;
            Enemy.AnyDied -= HandleEnemyDied;
            Enemy.AnyLeaked -= HandleEnemyLeaked;

            // Plain reference checks: the GameManager may already be destroyed,
            // but its managed events can (and must) still be unsubscribed.
            if (!ReferenceEquals(boundGm, null))
            {
                boundGm.GoldChanged -= HandleGoldChanged;
                boundGm.WaveStarted -= HandleWaveStarted;
                boundGm.GameOverTriggered -= HandleGameOver;
                boundGm.VictoryTriggered -= HandleVictory;
            }
            if (!ReferenceEquals(boundWm, null)) boundWm.WaveSpawning -= HandleWaveSpawning;

            boundGm = null;
            boundWm = null;
            bound = false;
            pendingCoin = false;
        }

        private void HandleTowerFired(Tower t)
        {
            TowerData d = t != null ? t.data : null;
            SoundId id = d == null
                ? SoundId.ArrowShot
                : SoundMapping.ForTowerShot(d.damageType == DamageType.Magic, d.damageType == DamageType.Poison,
                                            d.splashRadius, d.appliesSlow, d.appliesPoison);
            Play(id);
        }

        private void HandleTowerUpgraded(Tower t) => Play(SoundId.Upgrade);

        private void HandleSlotChanged(TowerPlacement slot)
        {
            if (slot != null && slot.IsOccupied) Play(SoundId.Build);
            else
            {
                Play(SoundId.Sell);
                sellFrame = Time.frameCount;
            }
        }

        private void HandleEnemyDamaged(Enemy e, float amount, DamageType type)
        {
            if (type == DamageType.Poison) return; // poison ticks every frame: the cloud sound covers it
            Play(SoundId.EnemyHit);
        }

        private void HandleEnemyDied(Enemy e) => Play(SoundMapping.ForEnemyDeath(e != null && e.IsBoss));

        private void HandleEnemyLeaked(Enemy e) => Play(SoundId.LifeLost);

        private void HandleGoldChanged(int gold)
        {
            bool gained = gold > lastGold;
            lastGold = gold;
            // Level setup (Configure) also raises gold; only reward sounds while waves run.
            if (gained && (boundWm == null || boundWm.IsRunning)) pendingCoin = true;
        }

        private void HandleWaveStarted(int wave)
        {
            if (boundWm != null && boundWm.IsBossWave(wave)) return; // the boss warning replaces the horn
            Play(SoundId.WaveStart);
        }

        private void HandleWaveSpawning(int wave, bool isBoss)
        {
            if (isBoss) Play(SoundId.BossWarning);
        }

        private void HandleGameOver()
        {
            Play(SoundId.Defeat);
            if (autoMusic) StopMusic(0.8f);
        }

        private void HandleVictory()
        {
            Play(SoundId.Victory);
            if (autoMusic) StopMusic(0.8f);
        }
    }
}
