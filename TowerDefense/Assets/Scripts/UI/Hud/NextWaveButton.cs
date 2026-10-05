using System;
using TowerDefense.Input;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// "Call next wave" button: visible while WaveManager.IsCountingDown, shows the
    /// countdown as a draining ring plus the early-call bonus (hidden on difficulties
    /// without one, e.g. Impossible), and calls
    /// CallNextWaveEarly() on tap. Optionally pinned to the first path's spawn
    /// point on screen (KR style), clamped inside the HUD's safe area.
    /// </summary>
    public sealed class NextWaveButton
    {
        private const float Size = 140f;

        /// <summary>Raised with the bonus gold after a successful early call.</summary>
        public event Action<int> Called;

        public RectTransform Root { get; }

        private readonly RectTransform hudRect;
        private readonly UIButtonView button;
        private readonly Image timerRing;
        private readonly Text bonusLabel;
        private readonly GameObject bonusPill;
        private readonly Text caption;
        private readonly Transform pulse;

        private WaveManager wm;
        private TouchInputController input;
        private bool atSpawn;
        private bool visible;
        private int shownBonus = int.MinValue;
        private int shownSeconds = int.MinValue;
        private string captionPrefix = "";
        private float lastFill = -1f;

        public NextWaveButton(RectTransform hud)
        {
            hudRect = hud;
            Root = UIFactory.Rect("NextWave", hud);
            Root.sizeDelta = new Vector2(Size, Size + 60f);

            timerRing = UIFactory.Image(Root, "Timer", UISprites.Ring(0.16f), UITheme.Gold);
            UIFactory.Place(timerRing.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 14f),
                            new Vector2(Size + 28f, Size + 28f));
            timerRing.type = Image.Type.Filled;
            timerRing.fillMethod = Image.FillMethod.Radial360;
            timerRing.fillOrigin = (int)Image.Origin360.Top;
            timerRing.fillClockwise = false;

            button = UIFactory.IconButton(Root, UISprites.Triangle, OnTapped, UITheme.Danger, Size, true, 0.5f);
            UIFactory.Place(button.Rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Size, Size));
            pulse = button.Face.transform;

            Image pill = UIFactory.Panel(Root, "Bonus", UITheme.Shade(UITheme.Panel, 0.9f), 26f, false);
            bonusPill = pill.gameObject;
            UIFactory.Place(pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(170f, 54f));
            Image coin = UIFactory.Image(pill.transform, "Coin", UISprites.Coin, Color.white);
            UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(42f, 42f));
            bonusLabel = UIFactory.Label(pill.transform, "", UITheme.FontBody - 4, UITheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(bonusLabel.rectTransform);
            bonusLabel.rectTransform.offsetMin = new Vector2(46f, 0f);

            caption = UIFactory.Label(button.Face.transform, "", UITheme.FontSmall - 2, UITheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(Size, 30f));

            Root.gameObject.SetActive(false);
        }

        public void Bind(WaveManager waveManager, TouchInputController controller, bool pinToSpawn)
        {
            wm = waveManager;
            input = controller;
            atSpawn = pinToSpawn;
            visible = false;
            Root.gameObject.SetActive(false);
            if (!atSpawn)
                UIFactory.Place(Root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-UITheme.Margin, UITheme.Margin + 10f), Root.sizeDelta);
        }

        public void Hide()
        {
            visible = false;
            Root.gameObject.SetActive(false);
        }

        public void Tick(bool allowed)
        {
            bool show = allowed && wm != null && wm.IsCountingDown;
            if (show != visible)
            {
                visible = show;
                Root.gameObject.SetActive(show);
                if (show)
                {
                    captionPrefix = wm.CurrentWave == 0 ? "START " : "NEXT ";
                    shownSeconds = int.MinValue;
                    UITween.Scale(Root, Vector3.zero, Vector3.one, 0.3f, Ease.OutBack);
                    shownBonus = int.MinValue;
                    lastFill = -1f;
                }
            }
            if (!visible) return;

            // Same rule CallNextWaveEarly pays out. Difficulties without the bonus
            // (Impossible) hide the pill; the button still calls the wave early.
            bool bonusEnabled = wm.EarlyCallBonusEnabled;
            if (bonusEnabled != bonusPill.activeSelf) bonusPill.SetActive(bonusEnabled);
            int bonus = bonusEnabled ? wm.EarlyCallBonusPreview : 0;
            if (bonusEnabled && bonus != shownBonus)
            {
                shownBonus = bonus;
                bonusLabel.text = "+" + NumberCache.Get(bonus);
            }

            int seconds = Mathf.CeilToInt(wm.TimeUntilNextWave);
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds; // rebuilt once per second, not per frame
                caption.text = captionPrefix + NumberCache.Get(seconds) + "s";
            }

            float fill = wm.delayBetweenWaves > 0f ? Mathf.Clamp01(wm.TimeUntilNextWave / wm.delayBetweenWaves) : 0f;
            if (Mathf.Abs(fill - lastFill) > 0.002f)
            {
                lastFill = fill;
                timerRing.fillAmount = fill;
            }

            float s = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f);
            pulse.localScale = new Vector3(s, s, 1f);

            if (atSpawn) PositionAtSpawn();
        }

        private void PositionAtSpawn()
        {
            if (wm.Paths == null || wm.Paths.Count == 0 || wm.Paths[0] == null || wm.Paths[0].Count == 0) return;
            Camera cam = input != null ? input.Cam : Camera.main;
            if (!UIRoot.WorldToLayer(hudRect, wm.Paths[0][0], cam, out Vector2 local)) return;

            Rect r = hudRect.rect;
            Vector2 half = Root.sizeDelta * 0.5f;
            float pad = 12f;
            // Leave the top-left resource panel and the top-right buttons alone.
            float topLimit = r.yMax - 190f - half.y;
            float x = Mathf.Clamp(local.x, r.xMin + half.x + pad, r.xMax - half.x - pad);
            float y = Mathf.Clamp(local.y, r.yMin + half.y + pad, Mathf.Max(r.yMin + half.y + pad, topLimit));
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
            Root.pivot = new Vector2(0.5f, 0.5f);
            Root.anchoredPosition = new Vector2(x, y) - r.center; // anchored to the parent's centre
        }

        private void OnTapped()
        {
            if (wm == null || !wm.IsCountingDown) return;
            int bonus = wm.CallNextWaveEarly();
            Hide();
            Called?.Invoke(bonus);
        }
    }
}
