using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// Entry points for every full-screen menu: main menu, level select, pause,
    /// settings, victory/defeat. Each returns the pushed <see cref="UIScreen"/>
    /// (Close() it, or call <see cref="CloseAll"/>). Creates the UIRoot on demand.
    /// </summary>
    public static partial class MenuScreens
    {
        private static UIRoot Root => UIRoot.Create();

        /// <summary>Close every screen and modal (e.g. when a level starts).</summary>
        public static void CloseAll(bool animate = false)
        {
            if (UIRoot.Instance == null) return;
            UIRoot.Instance.CloseAllModals(animate);
            UIRoot.Instance.CloseAllScreens(animate);
        }

        // ------------------------------------------------------------------ main menu

        public static UIScreen ShowMainMenu(MainMenuCallbacks cb)
        {
            cb ??= new MainMenuCallbacks();
            UIScreen s = Root.CreateScreen("MainMenu", ScreenBackground.Opaque);
            s.OnBack = null;
            Decorate(s);

            Text title = UIFactory.Label(s.Content, cb.title, UITheme.FontHuge, UITheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1600f, 180f));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = UITheme.Shade(UITheme.GoldDark, 0.5f);
            outline.effectDistance = new Vector2(4f, -4f);

            if (!string.IsNullOrEmpty(cb.subtitle))
            {
                Text sub = UIFactory.Label(s.Content, cb.subtitle, UITheme.FontLarge, UITheme.Parchment);
                UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(1400f, 80f));
            }

            RectTransform column = UIFactory.Rect("Buttons", s.Content);
            UIFactory.Place(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 90f), new Vector2(640f, 520f));
            UIFactory.VerticalLayout(column.gameObject, 28f);

            var buttons = new List<UIButtonView>(3);
            if (cb.onPlayCampaign != null)
                buttons.Add(UIFactory.Button(column, "Play Campaign", cb.onPlayCampaign, UITheme.Primary, new Vector2(640f, 150f), UITheme.FontLarge));
            if (cb.onEndless != null)
                buttons.Add(UIFactory.Button(column, "Endless", cb.onEndless, UITheme.Secondary, new Vector2(640f, 150f), UITheme.FontLarge));
            Action settings = cb.onSettings;
            if (settings == null && cb.settings != null)
            {
                IUiSettings impl = cb.settings;
                settings = () => ShowSettings(impl);
            }
            if (settings != null)
                buttons.Add(UIFactory.Button(column, "Settings", settings, UITheme.Neutral, new Vector2(640f, 150f), UITheme.FontLarge));

            for (int i = 0; i < buttons.Count; i++)
                UITween.Scale(buttons[i].transform, Vector3.zero, Vector3.one, 0.35f, Ease.OutBack, 0.12f + 0.08f * i);

            if (!string.IsNullOrEmpty(cb.footer))
            {
                Text footer = UIFactory.Label(s.Content, cb.footer, UITheme.FontSmall - 4, UITheme.TextDim, TextAnchor.LowerRight);
                UIFactory.Place(footer.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 16f), new Vector2(600f, 40f));
            }
            return s;
        }

        // ------------------------------------------------------------------ level select

        /// <summary>
        /// Grid of level cards. <paramref name="onPick"/> fires for unlocked cards
        /// (the screen stays open: call CloseAll() when the level is ready).
        /// </summary>
        public static UIScreen ShowLevelSelect(IReadOnlyList<LevelSelectEntry> entries, Action<LevelSelectEntry> onPick,
                                               Action onBack, string title = "Select Level")
        {
            UIScreen s = Root.CreateScreen("LevelSelect", ScreenBackground.Opaque);
            Action back = () =>
            {
                s.Close();
                onBack?.Invoke();
            };
            s.OnBack = back;
            Decorate(s);

            UIButtonView backButton = UIFactory.IconButton(s.Content, UISprites.Triangle, back, UITheme.Neutral, UITheme.IconButton);
            UIFactory.Place(backButton.Rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(UITheme.Margin, -UITheme.Margin),
                            new Vector2(UITheme.IconButton, UITheme.IconButton));
            backButton.Icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            Text header = UIFactory.Label(s.Content, title, UITheme.FontTitle, UITheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -UITheme.Margin), new Vector2(1200f, 130f));

            // Scrollable, wrapping grid.
            RectTransform viewport = UIFactory.Stretch(UIFactory.Rect("Viewport", s.Content));
            viewport.offsetMin = new Vector2(40f, 24f);
            viewport.offsetMax = new Vector2(-40f, -190f);
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = UIFactory.Rect("Grid", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(360f, 320f);
            grid.spacing = new Vector2(36f, 36f);
            grid.padding = new RectOffset(20, 20, 20, 40);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 40f;

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    LevelSelectEntry e = entries[i];
                    if (e == null) continue;
                    UIButtonView card = LevelCard(content, e, i, onPick);
                    UITween.Scale(card.transform, Vector3.one * 0.6f, Vector3.one, 0.3f, Ease.OutBack, 0.03f * i);
                }
            }
            return s;
        }

        private static UIButtonView LevelCard(RectTransform parent, LevelSelectEntry e, int index, Action<LevelSelectEntry> onPick)
        {
            Color color = e.locked ? UITheme.Neutral : e.isEndless ? new Color(0.45f, 0.30f, 0.62f, 1f) : UITheme.PanelLight;
            UIButtonView card = UIFactory.Button(parent, null, () =>
            {
                if (e.locked)
                {
                    if (UIRoot.Instance != null) UIRoot.Instance.ShowToast("Finish earlier levels to unlock");
                    return;
                }
                onPick?.Invoke(e);
            }, color, new Vector2(360f, 320f), UITheme.FontBody, 30f);
            UIFactory.Outline(card.Face.transform, e.locked ? UITheme.Shade(UITheme.PanelBorder, 0.6f) : UITheme.PanelBorder, 30f, 0f);

            Transform face = card.Face.transform;
            Text num = UIFactory.Label(face, e.isEndless ? "ENDLESS" : (index + 1).ToString(), e.isEndless ? UITheme.FontBody : UITheme.FontTitle,
                                       e.locked ? UITheme.TextDim : UITheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(num.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(330f, 110f));

            Text name = UIFactory.FitLabel(face, e.title ?? e.id ?? "Level", UITheme.FontBody, UITheme.Text, 0f);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(330f, 60f));

            if (e.locked)
            {
                Text lockText = UIFactory.Label(face, "LOCKED", UITheme.FontBody, UITheme.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Place(lockText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(330f, 60f));
            }
            else if (e.isEndless)
            {
                Text best = UIFactory.Label(face, e.bestWave > 0 ? "Best: wave " + e.bestWave : "Survive as long as you can",
                                            UITheme.FontSmall, UITheme.Parchment);
                UIFactory.Place(best.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(330f, 70f));
            }
            else
            {
                Image[] stars = UIFactory.Stars(face, 3, Mathf.Clamp(e.stars, 0, 3), 66f, 10f);
                var row = (RectTransform)stars[0].transform.parent;
                UIFactory.Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), row.sizeDelta);
            }

            if (!string.IsNullOrEmpty(e.subtitle) && !e.locked)
            {
                Text sub = UIFactory.Label(face, e.subtitle, UITheme.FontSmall - 4, UITheme.TextDim);
                UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(330f, 34f));
            }
            return card;
        }

        // ------------------------------------------------------------------ shared decoration

        /// <summary>Soft glows on opaque screens so they don't look flat.</summary>
        private static void Decorate(UIScreen s)
        {
            if (!s.IsOpaque) return;
            AddGlow(s.Rect, new Vector2(0.15f, 0.85f), 1100f, new Color(0.45f, 0.32f, 0.15f, 0.35f));
            AddGlow(s.Rect, new Vector2(0.9f, 0.15f), 1300f, new Color(0.18f, 0.32f, 0.45f, 0.35f));
            AddGlow(s.Rect, new Vector2(0.55f, 0.5f), 900f, new Color(0.25f, 0.4f, 0.2f, 0.18f));
            s.Content.SetAsLastSibling();
        }

        private static void AddGlow(RectTransform parent, Vector2 anchor, float size, Color color)
        {
            Image g = UIFactory.Image(parent, "Glow", UISprites.Glow, color);
            UIFactory.Place(g.rectTransform, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        }

        /// <summary>Centred rounded panel with a gold title, used by the in-game screens.</summary>
        internal static Image TitledPanel(UIScreen s, string title, Vector2 size, Color titleColor)
        {
            Image panel = UIFactory.Panel(s.Content, "Panel", UITheme.Panel, 40f);
            UIFactory.Center(panel.rectTransform, Vector2.zero, size);
            UIFactory.Outline(panel.transform, UITheme.PanelBorder, 40f, 0f);
            Text t = UIFactory.Label(panel.transform, title, UITheme.FontTitle, titleColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(size.x - 60f, 130f));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return panel;
        }

        internal static RectTransform ButtonColumn(Transform parent, float top, float width)
        {
            RectTransform col = UIFactory.Rect("Buttons", parent);
            UIFactory.Place(col, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(width, 700f));
            UIFactory.VerticalLayout(col.gameObject, 24f);
            return col;
        }
    }
}
