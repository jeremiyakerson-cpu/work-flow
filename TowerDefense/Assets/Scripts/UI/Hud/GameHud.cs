using System;
using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Input;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// In-level HUD: lives and gold (animated), wave counter, pause and 1x/2x/3x
    /// speed, next-wave call with countdown and bonus, boss/final-wave banners,
    /// hero portrait + ability cooldown, spell buttons, the radial build/upgrade
    /// menu, pause menu, mode hints and the optional tutorial.
    ///
    /// Usage: <c>var hud = GameHud.Bind(gm, wm, hero, towers, options);</c> after the
    /// level is built, and <c>hud.Unbind()</c> before tearing it down. Unbind
    /// removes every event subscription (including static TowerPlacement events).
    /// Victory/defeat screens are shown by the integration layer via MenuScreens.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        /// <summary>The currently bound HUD (null between levels).</summary>
        public static GameHud Current { get; private set; }

        public TowerMenu TowerMenu => towerMenu;
        public TutorialOverlay Tutorial => tutorial;
        public bool IsBound => bound;

        /// <summary>A spell was cast (audio / VFX hook).</summary>
        public event Action<SpellDefinition, Vector3> SpellCast;

        private GameManager gm;
        private WaveManager wm;
        private HeroUnit hero;
        private IReadOnlyList<TowerData> towers;
        private HudOptions options;
        private TouchInputController input;
        private bool bound;

        private RectTransform rect;
        private AnimatedCounter lives;
        private AnimatedCounter gold;
        private Text waveLabel;
        private RectTransform difficultyRoot;
        private Image difficultyBadge;
        private UIButtonView pauseButton;
        private UIButtonView speedButton;
        private HeroWidget heroWidget;
        private RectTransform spellRow;
        private readonly List<SpellButton> spells = new List<SpellButton>(2);
        private NextWaveButton nextWave;
        private Banner banner;
        private TutorialOverlay tutorial;
        private TowerMenu towerMenu;
        private WorldIndicator heroMarker;
        private RectTransform modeHint;
        private Text modeHintLabel;
        private CanvasGroup modeHintGroup;

        private UIScreen pauseScreen;
        private Func<bool> backHandler;
        private int shownWave = -1;
        private int shownWaveTotal = -1;
        private float shownSpeed = -1f;
        private string pendingHint;

        // ------------------------------------------------------------------ binding

        /// <summary>
        /// Build the HUD for a level and subscribe to its systems. Any previous HUD
        /// is unbound first. <paramref name="hero"/> may be null (or set later via SetHero).
        /// </summary>
        public static GameHud Bind(GameManager gameManager, WaveManager waveManager, HeroUnit hero,
                                   IReadOnlyList<TowerData> towers, HudOptions options = null)
        {
            UIRoot ui = UIRoot.Create();
            if (Current != null) Current.Unbind();

            RectTransform rt = UIFactory.Stretch(UIFactory.Rect("GameHud", ui.HudLayer));
            var hud = rt.gameObject.AddComponent<GameHud>();
            hud.rect = rt;
            hud.Build(ui);
            hud.Attach(gameManager, waveManager, hero, towers, options ?? new HudOptions());
            Current = hud;
            return hud;
        }

        /// <summary>Unsubscribe from everything, close HUD-owned screens and destroy the HUD.</summary>
        public void Unbind()
        {
            if (bound)
            {
                bound = false;
                if (gm != null)
                {
                    gm.GoldChanged -= OnGoldChanged;
                    gm.LivesChanged -= OnLivesChanged;
                    gm.WaveStarted -= OnWaveStarted;
                    gm.PausedChanged -= OnPausedChanged;
                    gm.SpeedChanged -= OnSpeedChanged;
                    gm.GameOverTriggered -= OnGameEnded;
                    gm.VictoryTriggered -= OnGameEnded;
                }
                if (wm != null) wm.WaveSpawning -= OnWaveSpawning;
                if (input != null)
                {
                    input.HeroSelectionChanged -= OnHeroSelectionChanged;
                    input.TargetingChanged -= OnTargetingChanged;
                    input.InvalidTap -= OnInvalidTap;
                    input.HeroMoveOrdered -= OnHeroMoveOrdered;
                    input.CancelTargeting();
                    input.DeselectHero();
                }
                if (towerMenu != null)
                {
                    towerMenu.TowerBuilt -= OnTowerBuilt;
                    towerMenu.TowerChanged -= OnTowerChanged;
                    towerMenu.Unbind();
                }
                if (nextWave != null) nextWave.Called -= OnWaveCalled;
                if (tutorial != null)
                {
                    tutorial.Finished -= OnTutorialFinished;
                    tutorial.Hide();
                }
                for (int i = 0; i < spells.Count; i++)
                {
                    spells[i].Cast -= OnSpellCast;
                    spells[i].TargetingStarted -= OnSpellTargeting;
                }
                if (heroWidget != null) heroWidget.Unbind();
                if (UIRoot.Instance != null && backHandler != null) UIRoot.Instance.RemoveBackHandler(backHandler);
                ClosePauseScreen();
            }

            if (Current == this) Current = null;
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (bound) Unbind();
            if (towerMenu != null) Destroy(towerMenu.gameObject);
            if (heroMarker != null) Destroy(heroMarker.gameObject);
            if (Current == this) Current = null;
        }

        private void Attach(GameManager gameManager, WaveManager waveManager, HeroUnit heroUnit,
                            IReadOnlyList<TowerData> towerList, HudOptions opts)
        {
            gm = gameManager;
            wm = waveManager;
            towers = towerList;
            options = opts;
            input = opts.input != null ? opts.input : TouchInputController.Instance;

            if (gm != null)
            {
                gm.GoldChanged += OnGoldChanged;
                gm.LivesChanged += OnLivesChanged;
                gm.WaveStarted += OnWaveStarted;
                gm.PausedChanged += OnPausedChanged;
                gm.SpeedChanged += OnSpeedChanged;
                gm.GameOverTriggered += OnGameEnded;
                gm.VictoryTriggered += OnGameEnded;
                lives.SetImmediate(gm.Lives);
                gold.SetImmediate(gm.Gold);
                RefreshSpeed(gm.GameSpeed);
            }
            ShowDifficulty(gm != null ? gm.CurrentDifficulty : DifficultyMode.Normal);
            if (wm != null) wm.WaveSpawning += OnWaveSpawning;
            if (input != null)
            {
                input.HeroSelectionChanged += OnHeroSelectionChanged;
                input.TargetingChanged += OnTargetingChanged;
                input.InvalidTap += OnInvalidTap;
                input.HeroMoveOrdered += OnHeroMoveOrdered;
            }

            towerMenu.Bind(gm, towers, input);
            towerMenu.TowerBuilt += OnTowerBuilt;
            towerMenu.TowerChanged += OnTowerChanged;

            nextWave.Bind(wm, input, opts.nextWaveAtSpawn);
            nextWave.Called += OnWaveCalled;

            BuildSpells(opts.spells);
            SetHero(heroUnit);
            RefreshWave(true);

            backHandler = HandleBack;
            UIRoot.Instance.PushBackHandler(backHandler);
            bound = true;

            if (opts.showTutorial)
            {
                tutorial.Finished += OnTutorialFinished;
                tutorial.Begin(hero != null);
            }
        }

        /// <summary>Attach (or replace) the hero after Bind, e.g. when it spawns later.</summary>
        public void SetHero(HeroUnit heroUnit)
        {
            hero = heroUnit;
            heroWidget.Bind(hero, input);
            if (input != null) input.SetHero(hero);
            if (heroMarker != null) heroMarker.Hide();
            LayoutBottomRow();
        }

        // ------------------------------------------------------------------ construction

        private void Build(UIRoot ui)
        {
            float m = UITheme.Margin;

            // Lives + gold panel (top-left).
            Image res = UIFactory.Panel(rect, "Resources", UITheme.Panel, 30f, true);
            UIFactory.Place(res.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(m, -m), new Vector2(480f, 116f));
            UIFactory.Outline(res.transform, UITheme.PanelBorder, 30f, 0f);
            lives = Counter(res.transform, UISprites.Heart, UITheme.Health, 18f);
            gold = Counter(res.transform, UISprites.Coin, Color.white, 246f);

            // Wave pill under it.
            Image wave = UIFactory.Panel(rect, "Wave", UITheme.Panel, 34f, true);
            UIFactory.Place(wave.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(m, -m - 128f), new Vector2(330f, 72f));
            waveLabel = UIFactory.Label(wave.transform, "", UITheme.FontBody, UITheme.Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(waveLabel.rectTransform, 6f);

            // Difficulty badge right of the wave pill (filled in on Attach).
            difficultyRoot = UIFactory.Rect("DifficultySlot", rect);
            UIFactory.Place(difficultyRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(m + 330f + 14f, -m - 128f - 8f),
                            new Vector2(210f, 56f));

            // Pause + speed (top-right).
            pauseButton = UIFactory.IconButton(rect, UISprites.PauseIcon, OnPauseTapped, UITheme.Neutral, UITheme.IconButton, false, 0.6f);
            UIFactory.Place(pauseButton.Rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-m, -m),
                            new Vector2(UITheme.IconButton, UITheme.IconButton));
            speedButton = UIFactory.Button(rect, "1x", OnSpeedTapped, UITheme.Secondary, new Vector2(UITheme.IconButton, UITheme.IconButton),
                                           UITheme.FontLarge);
            UIFactory.Place(speedButton.Rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-m - UITheme.IconButton - UITheme.Spacing, -m),
                            new Vector2(UITheme.IconButton, UITheme.IconButton));

            // Hero + ability (bottom-left), spells to their right.
            heroWidget = new HeroWidget(rect);
            spellRow = UIFactory.Rect("Spells", rect);
            UIFactory.Place(spellRow, Vector2.zero, Vector2.zero, new Vector2(m, m + 50f), new Vector2(600f, SpellButton.Size));
            UIFactory.HorizontalLayout(spellRow.gameObject, UITheme.Spacing + 20f, TextAnchor.MiddleLeft);

            nextWave = new NextWaveButton(rect);

            // Mode hint (top-centre) while choosing a hero destination / spell target.
            Image hint = UIFactory.Panel(rect, "ModeHint", UITheme.WithAlpha(UITheme.Panel, 0.9f), 34f, false);
            modeHint = hint.rectTransform;
            UIFactory.Place(modeHint, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -m), new Vector2(760f, 76f));
            modeHintLabel = UIFactory.Label(hint.transform, "", UITheme.FontBody, UITheme.Highlight);
            UIFactory.Stretch(modeHintLabel.rectTransform, 8f);
            modeHintGroup = UIFactory.Group(hint.gameObject);
            modeHintGroup.blocksRaycasts = false;
            modeHint.gameObject.SetActive(false);

            banner = new Banner(rect);
            tutorial = new TutorialOverlay(rect);
            towerMenu = TowerMenu.Create(ui.WorldLayer);
            heroMarker = WorldIndicator.Create("[HeroMarker]", UISprites.WorldMarker, WorldIndicator.MarkerSortingOrder);
        }

        private static AnimatedCounter Counter(Transform parent, Sprite icon, Color iconColor, float x)
        {
            Image img = UIFactory.Image(parent, "Icon", icon, iconColor);
            UIFactory.Place(img.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(72f, 72f));
            Text t = UIFactory.Label(parent, "0", UITheme.FontLarge, UITheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x + 86f, 0f), new Vector2(130f, 80f));
            var counter = t.gameObject.AddComponent<AnimatedCounter>();
            counter.Label = t;
            counter.PunchTarget = img.transform;
            return counter;
        }

        private void BuildSpells(IReadOnlyList<SpellDefinition> defs)
        {
            spells.Clear();
            if (defs == null) return;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] == null) continue;
                var sb = new SpellButton(spellRow, defs[i], input);
                sb.Cast += OnSpellCast;
                sb.TargetingStarted += OnSpellTargeting;
                spells.Add(sb);
            }
        }

        private void LayoutBottomRow()
        {
            // Spells sit right of the hero block when there is a hero.
            float x = UITheme.Margin + (hero != null ? 150f + UITheme.Spacing + 124f + 40f : 0f);
            spellRow.anchoredPosition = new Vector2(x, UITheme.Margin + 50f);
        }

        // ------------------------------------------------------------------ frame update (polling only what has no events)

        private void Update()
        {
            if (!bound) return;
            bool playing = gm == null || (!gm.IsGameOver && !gm.IsVictory);
            nextWave.Tick(playing);
            heroWidget.Tick();
            float dt = Time.deltaTime; // spells cool down in game time
            for (int i = 0; i < spells.Count; i++) spells[i].Tick(dt);
        }

        // ------------------------------------------------------------------ GameManager / WaveManager events

        private void OnGoldChanged(int value) => gold.SetValue(value);

        private void OnLivesChanged(int value) => lives.SetValue(value);

        private void OnWaveStarted(int wave)
        {
            RefreshWave(false);
            if (tutorial.IsActive && tutorial.Current == TutorialStep.CallWave) tutorial.Complete(TutorialStep.CallWave);
        }

        private void OnWaveSpawning(int wave, bool isBoss)
        {
            if (isBoss) banner.Show("BOSS INCOMING", "Wave " + wave, UITheme.Danger);
            else if (wm != null && wm.wavesToWin > 0 && wave == wm.wavesToWin)
                banner.Show("FINAL WAVE", "Hold the line!", UITheme.Shade(UITheme.Gold, 0.75f));
        }

        private void RefreshWave(bool immediate)
        {
            int cur = wm != null ? wm.CurrentWave : 0;
            int total = wm != null ? wm.wavesToWin : 0;
            if (cur == shownWave && total == shownWaveTotal) return;
            shownWave = cur;
            shownWaveTotal = total;
            waveLabel.text = total > 0 ? "Wave " + cur + "/" + total : "Wave " + cur;
            if (!immediate) UITween.Punch(waveLabel.transform, 0.25f, 0.35f);
        }

        private void OnSpeedChanged(float speed) => RefreshSpeed(speed);

        private void RefreshSpeed(float speed)
        {
            if (Mathf.Approximately(speed, shownSpeed)) return;
            shownSpeed = speed;
            int s = Mathf.RoundToInt(speed);
            speedButton.SetLabel(s <= 1 ? "1x" : s == 2 ? "2x" : "3x");
            speedButton.SetColor(s <= 1 ? UITheme.Secondary : s == 2 ? UITheme.Shade(UITheme.Gold, 0.85f) : UITheme.Danger);
        }

        private void OnPausedChanged(bool paused)
        {
            if (paused) OpenPauseScreen();
            else ClosePauseScreen();
        }

        private void OnGameEnded()
        {
            ClosePauseScreen();
            CancelModes();
            towerMenu.Close();
            nextWave.Hide();
            if (tutorial.IsActive) tutorial.Hide();
        }

        // ------------------------------------------------------------------ buttons

        private void OnPauseTapped()
        {
            if (gm == null || gm.IsGameOver || gm.IsVictory) return;
            gm.Pause(); // the pause screen opens from PausedChanged
        }

        private void OnSpeedTapped()
        {
            if (gm == null) return;
            int s = Mathf.RoundToInt(gm.GameSpeed);
            gm.SetGameSpeed(s >= 3 ? 1f : s + 1f);
        }

        private void OnWaveCalled(int bonus)
        {
            if (bonus > 0 && UIRoot.Instance != null) UIRoot.Instance.ShowToast("Early call! +" + bonus + " gold");
            options.onWaveCalledEarly?.Invoke(bonus);
            if (tutorial.IsActive) tutorial.Complete(TutorialStep.CallWave);
        }

        private void OpenPauseScreen()
        {
            if (pauseScreen != null || gm == null || gm.IsGameOver || gm.IsVictory) return;
            CancelModes();
            towerMenu.Close();
            pauseScreen = MenuScreens.ShowPause(new PauseMenuCallbacks
            {
                onResume = () => { if (gm != null) gm.Resume(); },
                onRestart = options.onRestart,
                onQuitToMenu = options.onQuitToMenu,
                settings = options.settings,
            });
            pauseScreen.Closed += OnPauseScreenClosed;
        }

        private void ClosePauseScreen()
        {
            if (pauseScreen == null) return;
            UIScreen s = pauseScreen;
            pauseScreen = null;
            s.Closed -= OnPauseScreenClosed;
            s.Close();
            // Settings / confirm dialogs opened from the pause menu go with it.
            if (UIRoot.Instance != null) UIRoot.Instance.CloseAllModals(true);
        }

        private void OnPauseScreenClosed() => pauseScreen = null;

        /// <summary>Esc / back: cancel modes, close the build menu, else pause.</summary>
        private bool HandleBack()
        {
            if (!bound) return false;
            if (CancelModes()) return true;
            if (towerMenu.IsOpen) { towerMenu.Close(); return true; }
            if (gm != null && !gm.IsGameOver && !gm.IsVictory && !gm.IsPaused) { gm.Pause(); return true; }
            return false;
        }

        private bool CancelModes() => input != null && input.CancelModes();

        // ------------------------------------------------------------------ input / menu events

        private void OnHeroSelectionChanged(bool selected)
        {
            heroWidget.SetSelected(selected);
            if (selected && hero != null)
            {
                heroMarker.Follow(hero.transform, 1.7f, UITheme.Highlight);
                ShowModeHint("Tap the road to move " + hero.heroName);
            }
            else
            {
                heroMarker.Hide();
                if (input == null || !input.IsTargeting) HideModeHint();
            }
        }

        private void OnTargetingChanged(bool targeting)
        {
            if (targeting)
            {
                ShowModeHint(string.IsNullOrEmpty(pendingHint) ? "Tap the road" : pendingHint);
            }
            else if (input == null || !input.IsHeroSelected)
            {
                HideModeHint();
            }
            pendingHint = null;
        }

        private void OnInvalidTap(Vector3 world)
        {
            if (UIRoot.Instance != null)
                UIRoot.Instance.FloatText(world, "Not on the road", UITheme.TextBad, input != null ? input.Cam : null);
        }

        private void OnHeroMoveOrdered(Vector3 destination)
        {
            if (tutorial.IsActive) tutorial.Complete(TutorialStep.MoveHero);
        }

        private void OnTowerBuilt(TowerPlacement slot, TowerData data)
        {
            if (tutorial.IsActive) tutorial.Complete(TutorialStep.Build);
            options.onTowerBuilt?.Invoke(slot, data);
        }

        private void OnTowerChanged(TowerPlacement slot) => options.onTowerChanged?.Invoke(slot);

        private void OnSpellCast(SpellButton spell, Vector3 position) => SpellCast?.Invoke(spell.Definition, position);

        private void OnSpellTargeting(SpellButton spell) => pendingHint = spell.Definition.targetingHint;

        private void OnTutorialFinished()
        {
            tutorial.Finished -= OnTutorialFinished;
            options.onTutorialFinished?.Invoke();
        }

        private void ShowModeHint(string text)
        {
            modeHintLabel.text = text;
            float w = Mathf.Clamp(modeHintLabel.preferredWidth + 80f, 420f, 1100f);
            modeHint.sizeDelta = new Vector2(w, 76f);
            if (!modeHint.gameObject.activeSelf)
            {
                modeHint.gameObject.SetActive(true);
                modeHintGroup.alpha = 0f;
                UITween.Move(modeHint, new Vector2(0f, 40f), new Vector2(0f, -UITheme.Margin), UITheme.NormalAnim, Ease.OutBack);
            }
            // Also cancels a pending fade-out (and its deactivate callback).
            UITween.Fade(modeHintGroup, modeHintGroup.alpha, 1f, UITheme.FastAnim);
        }

        private void HideModeHint()
        {
            if (!modeHint.gameObject.activeSelf) return;
            UITween.Fade(modeHintGroup, modeHintGroup.alpha, 0f, UITheme.FastAnim, Ease.InQuad, 0f, () =>
            {
                if (modeHint != null) modeHint.gameObject.SetActive(false);
            });
        }

        /// <summary>
        /// Small difficulty badge next to the wave counter. Bind shows GameManager.CurrentDifficulty;
        /// call again if the difficulty is configured after Bind.
        /// </summary>
        public void ShowDifficulty(DifficultyMode mode)
        {
            if (difficultyBadge != null) Destroy(difficultyBadge.gameObject);
            difficultyBadge = DifficultyStyle.Badge(difficultyRoot, mode, UITheme.FontSmall - 4);
            UIFactory.Stretch(difficultyBadge.rectTransform);
        }

        /// <summary>Show a custom announcement banner (e.g. level intro).</summary>
        public void ShowBanner(string title, string subtitle, Color color) => banner.Show(title, subtitle, color);

        /// <summary>Restart the tutorial manually (e.g. from a help button).</summary>
        public void ShowTutorial()
        {
            tutorial.Finished -= OnTutorialFinished;
            tutorial.Finished += OnTutorialFinished;
            tutorial.Begin(hero != null);
        }

        /// <summary>Reset spell cooldowns (when a level restarts without rebinding).</summary>
        public void ResetSpells()
        {
            for (int i = 0; i < spells.Count; i++) spells[i].ResetCooldown();
        }
    }
}
