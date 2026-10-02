using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    public static partial class MenuScreens
    {
        // ------------------------------------------------------------------ pause

        /// <summary>Pause menu over the dimmed battlefield. Back = resume.</summary>
        public static UIScreen ShowPause(PauseMenuCallbacks cb)
        {
            cb ??= new PauseMenuCallbacks();
            UIScreen s = Root.CreateScreen("Pause", ScreenBackground.Dim);
            int count = 2 + (cb.onRestart != null ? 1 : 0) + (cb.onQuitToMenu != null ? 1 : 0);
            Image panel = TitledPanel(s, "Paused", new Vector2(760f, 200f + count * 154f), UITheme.Gold);
            RectTransform col = ButtonColumn(panel.transform, 170f, 580f);
            var size = new Vector2(580f, UITheme.ButtonHeight);

            Action resume = () => cb.onResume?.Invoke();
            s.OnBack = resume;
            UIFactory.Button(col, "Resume", resume, UITheme.Primary, size);

            if (cb.onRestart != null)
            {
                Action restart = cb.onRestart;
                UIFactory.Button(col, "Restart", () =>
                {
                    if (!cb.confirmDestructive) { restart(); return; }
                    Root.Confirm("Restart level?", "Your progress in this level will be lost.", "Restart", "Cancel", restart, null, true);
                }, UITheme.Neutral, size);
            }

            IUiSettings settings = cb.settings;
            UIButtonView settingsButton = UIFactory.Button(col, "Settings", () => ShowSettings(settings), UITheme.Secondary, size);
            settingsButton.Interactable = settings != null;

            if (cb.onQuitToMenu != null)
            {
                Action quit = cb.onQuitToMenu;
                UIFactory.Button(col, "Quit to Menu", () =>
                {
                    if (!cb.confirmDestructive) { quit(); return; }
                    Root.Confirm("Quit to menu?", "Your progress in this level will be lost.", "Quit", "Cancel", quit, null, true);
                }, UITheme.Danger, size);
            }
            return s;
        }

        // ------------------------------------------------------------------ settings

        /// <summary>Music / SFX volume, haptics and reset progress, bound to <paramref name="settings"/>.</summary>
        public static UIScreen ShowSettings(IUiSettings settings, Action onClose = null)
        {
            UIScreen s = Root.CreateScreen("Settings", ScreenBackground.Dim, true);
            Image panel = TitledPanel(s, "Settings", new Vector2(1100f, 820f), UITheme.Gold);
            Transform p = panel.transform;

            bool closed = false;
            Action close = () =>
            {
                if (closed) return;
                closed = true;
                settings?.Save();
                s.Close();
                onClose?.Invoke();
            };
            s.OnBack = close;
            s.SetBackdropAction(close);

            if (settings == null)
            {
                Text none = UIFactory.Label(p, "Settings are not available.", UITheme.FontBody, UITheme.TextDim);
                UIFactory.Center(none.rectTransform, new Vector2(0f, 40f), new Vector2(900f, 100f));
            }
            else
            {
                SettingRow(p, "Music", -190f);
                Slider music = UIFactory.Slider(p, new Vector2(560f, 100f), settings.MusicVolume, v => settings.MusicVolume = v);
                UIFactory.Place((RectTransform)music.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-80f, -190f), new Vector2(560f, 100f));

                SettingRow(p, "Sound effects", -320f);
                Slider sfx = UIFactory.Slider(p, new Vector2(560f, 100f), settings.SfxVolume, v => settings.SfxVolume = v);
                UIFactory.Place((RectTransform)sfx.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-80f, -320f), new Vector2(560f, 100f));

                SettingRow(p, "Haptics", -450f);
                Toggle haptics = UIFactory.Toggle(p, 110f, settings.HapticsEnabled, v => settings.HapticsEnabled = v);
                UIFactory.Place((RectTransform)haptics.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-80f, -450f), new Vector2(110f, 110f));

                UIButtonView reset = UIFactory.Button(p, "Reset progress", () =>
                    Root.Confirm("Reset all progress?", "Stars, unlocked levels and records will be erased. This cannot be undone.",
                                 "Reset", "Cancel", () =>
                                 {
                                     settings.ResetProgress();
                                     Root.ShowToast("Progress reset");
                                 }, null, true),
                    UITheme.Danger, new Vector2(440f, UITheme.ButtonHeight), UITheme.FontBody);
                UIFactory.Place(reset.Rect, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 48f), new Vector2(440f, UITheme.ButtonHeight));
            }

            UIButtonView done = UIFactory.Button(p, "Done", close, UITheme.Primary, new Vector2(440f, UITheme.ButtonHeight));
            UIFactory.Place(done.Rect, new Vector2(0.5f, 0f), settings == null ? new Vector2(0.5f, 0f) : new Vector2(0f, 0f),
                            new Vector2(settings == null ? 0f : 20f, 48f), new Vector2(440f, UITheme.ButtonHeight));
            return s;
        }

        private static Text SettingRow(Transform panel, string label, float y)
        {
            Text t = UIFactory.Label(panel, label, UITheme.FontLarge - 6, UITheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-480f, y), new Vector2(380f, 90f));
            return t;
        }

        // ------------------------------------------------------------------ victory / defeat

        public static UIScreen ShowVictory(ResultsData data, ResultsCallbacks cb)
        {
            data ??= new ResultsData();
            data.victory = true;
            return ShowResults(data, cb);
        }

        public static UIScreen ShowDefeat(ResultsData data, ResultsCallbacks cb)
        {
            data ??= new ResultsData();
            data.victory = false;
            return ShowResults(data, cb);
        }

        /// <summary>Victory (1-3 animated stars) or defeat screen with stats and next/retry/menu.</summary>
        public static UIScreen ShowResults(ResultsData data, ResultsCallbacks cb)
        {
            data ??= new ResultsData();
            cb ??= new ResultsCallbacks();
            UIScreen s = Root.CreateScreen(data.victory ? "Victory" : "Defeat", ScreenBackground.Dim);
            string heading = data.victory ? "Victory!" : (data.isEndless ? "Overrun!" : "Defeat");
            Image panel = TitledPanel(s, heading, new Vector2(1180f, 860f), data.victory ? UITheme.Gold : UITheme.TextBad);
            Transform p = panel.transform;
            s.OnBack = cb.onMenu ?? cb.onRetry;

            if (!string.IsNullOrEmpty(data.levelTitle))
            {
                Text lt = UIFactory.Label(p, data.levelTitle, UITheme.FontBody, UITheme.Parchment);
                UIFactory.Place(lt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 56f));
            }

            float statsTop = -230f;
            if (data.victory && !data.isEndless)
            {
                Image[] stars = UIFactory.Stars(p, 3, 0, 170f, 30f);
                var row = (RectTransform)stars[0].transform.parent;
                UIFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), row.sizeDelta);
                int earned = Mathf.Clamp(data.stars, 0, 3);
                for (int i = 0; i < 3; i++)
                {
                    stars[i].transform.localScale = Vector3.one;
                    if (i >= earned) continue;
                    Image star = stars[i];
                    UITween.To(star, 0.45f, v =>
                    {
                        if (star == null) return;
                        star.color = UITheme.Gold;
                        star.transform.localScale = Vector3.one * Mathf.LerpUnclamped(2.2f, 1f, v);
                    }, Ease.OutBack, 0.45f + 0.35f * i, () =>
                    {
                        if (star != null) UITween.Punch(row, 0.06f, 0.2f);
                    });
                }
                statsTop = -420f;
            }

            RectTransform stats = UIFactory.Rect("Stats", p);
            UIFactory.Place(stats, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, statsTop), new Vector2(900f, 260f));
            UIFactory.VerticalLayout(stats.gameObject, 6f);

            if (data.isEndless || data.wavesToWin <= 0)
            {
                StatLine(stats, "Waves survived", data.wave.ToString());
                if (data.bestWave > 0) StatLine(stats, "Best", data.bestWave.ToString());
                if (data.isNewBest)
                {
                    Text nb = UIFactory.Label(stats, "New record!", UITheme.FontLarge, UITheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                    nb.rectTransform.sizeDelta = new Vector2(900f, 72f);
                    UITween.Scale(nb.transform, Vector3.zero, Vector3.one, 0.5f, Ease.OutElastic, 0.6f);
                }
            }
            else
            {
                StatLine(stats, "Waves", Mathf.Min(data.wave, data.wavesToWin) + " / " + data.wavesToWin);
            }
            if (data.startingLives > 0) StatLine(stats, "Lives left", data.livesLeft + " / " + data.startingLives);
            if (data.extraStats != null)
                for (int i = 0; i < data.extraStats.Count; i++)
                    StatLine(stats, data.extraStats[i].Key, data.extraStats[i].Value);

            // Buttons: Menu | Retry | Next (whichever exist), centred along the bottom.
            RectTransform row2 = UIFactory.Rect("Actions", p);
            UIFactory.Place(row2, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(1100f, UITheme.ButtonHeight));
            UIFactory.HorizontalLayout(row2.gameObject, 30f);
            var size = new Vector2(320f, UITheme.ButtonHeight);
            if (cb.onMenu != null) UIFactory.Button(row2, "Menu", cb.onMenu, UITheme.Neutral, size);
            if (cb.onRetry != null) UIFactory.Button(row2, data.victory ? "Replay" : "Retry", cb.onRetry, UITheme.Secondary, size);
            if (cb.onNext != null && data.victory) UIFactory.Button(row2, "Next", cb.onNext, UITheme.Primary, size);
            return s;
        }

        private static void StatLine(RectTransform parent, string label, string value)
        {
            RectTransform line = UIFactory.Rect("Stat", parent);
            line.sizeDelta = new Vector2(900f, 60f);
            Text l = UIFactory.Label(line, label, UITheme.FontBody, UITheme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Stretch(l.rectTransform);
            l.rectTransform.offsetMax = new Vector2(-450f, 0f);
            l.rectTransform.offsetMin = new Vector2(120f, 0f);
            Text v = UIFactory.Label(line, value, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Stretch(v.rectTransform);
            v.rectTransform.offsetMin = new Vector2(450f, 0f);
            v.rectTransform.offsetMax = new Vector2(-120f, 0f);
        }
    }
}
