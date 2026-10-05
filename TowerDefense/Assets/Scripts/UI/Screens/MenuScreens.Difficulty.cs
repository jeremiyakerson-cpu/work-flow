using System;
using System.Collections.Generic;
using TowerDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>Colours and short labels for difficulty modes (picker, badges, results).</summary>
    public static class DifficultyStyle
    {
        public static readonly Color Easy = new Color(0.34f, 0.66f, 0.31f, 1f);
        public static readonly Color Normal = new Color(0.27f, 0.45f, 0.72f, 1f);
        public static readonly Color Hard = new Color(0.86f, 0.50f, 0.16f, 1f);
        public static readonly Color Impossible = new Color(0.66f, 0.15f, 0.24f, 1f);

        public static Color ColorOf(DifficultyMode mode)
        {
            switch (mode)
            {
                case DifficultyMode.Easy: return Easy;
                case DifficultyMode.Hard: return Hard;
                case DifficultyMode.Impossible: return Impossible;
                default: return Normal;
            }
        }

        /// <summary>Upper-case name for small badges.</summary>
        public static string BadgeText(DifficultyMode mode) => Difficulty.DisplayName(mode).ToUpperInvariant();

        /// <summary>
        /// Small rounded pill with the mode's colour and name (HUD, level cards, results).
        /// Never a raycast target. Size it with UIFactory.Place on the returned image.
        /// </summary>
        public static Image Badge(Transform parent, DifficultyMode mode, int fontSize)
        {
            Image pill = UIFactory.Panel(parent, "Difficulty", ColorOf(mode), 22f, false);
            UIFactory.Outline(pill.transform, UITheme.WithAlpha(UITheme.Text, 0.55f), 22f, 0f);
            Text t = UIFactory.FitLabel(pill.transform, BadgeText(mode), fontSize, UITheme.Text, 4f);
            t.fontStyle = FontStyle.Bold;
            return pill;
        }
    }

    public static partial class MenuScreens
    {
        private const float PickerWidth = 1560f;
        private const float PickerHeight = 880f;
        private const float OptionWidth = 345f;
        private const float OptionHeight = 400f;
        private const float OptionSpacing = 24f;

        /// <summary>
        /// Modal difficulty picker shown after a level is picked: one card per option
        /// (name, modifiers, best result on that difficulty, lock), the last used mode
        /// preselected, Back and Start. Tapping a card selects it; Start calls
        /// <paramref name="onStart"/> with the selected mode (the modal closes first).
        /// Back, Esc or a backdrop tap close it and call <paramref name="onBack"/>.
        /// </summary>
        public static UIScreen ShowDifficultyPicker(DifficultyPickerData data, Action<DifficultyMode> onStart, Action onBack = null)
        {
            data ??= new DifficultyPickerData();
            IReadOnlyList<DifficultyOption> options = data.options ?? Array.Empty<DifficultyOption>();

            UIScreen s = Root.CreateScreen("DifficultyPicker", ScreenBackground.Dim, true);
            Image panel = TitledPanel(s, "Difficulty", new Vector2(PickerWidth, PickerHeight), UITheme.Gold);
            Transform p = panel.transform;

            if (!string.IsNullOrEmpty(data.levelTitle))
            {
                Text lt = UIFactory.Label(p, data.levelTitle, UITheme.FontBody, UITheme.Parchment);
                UIFactory.Place(lt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f),
                                new Vector2(PickerWidth - 120f, 52f));
            }

            bool done = false;
            Action back = () =>
            {
                if (done) return;
                done = true;
                s.Close();
                onBack?.Invoke();
            };
            s.OnBack = back;
            s.SetBackdropAction(back);

            // Cards.
            RectTransform row = UIFactory.Rect("Options", p);
            float rowWidth = options.Count * OptionWidth + Mathf.Max(0, options.Count - 1) * OptionSpacing;
            UIFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(rowWidth, OptionHeight));
            UIFactory.HorizontalLayout(row.gameObject, OptionSpacing);

            Text description = UIFactory.Label(p, "", UITheme.FontBody - 4, UITheme.Parchment);
            UIFactory.Place(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -628f),
                            new Vector2(PickerWidth - 120f, 64f));

            var outlines = new Image[options.Count];
            var cards = new UIButtonView[options.Count];
            int selected = -1;
            UIButtonView start = null;

            Action<int, bool> select = (index, animate) =>
            {
                if (index < 0 || index >= options.Count || options[index] == null || options[index].locked) return;
                selected = index;
                for (int i = 0; i < outlines.Length; i++)
                    if (outlines[i] != null) outlines[i].gameObject.SetActive(i == index);
                DifficultyOption o = options[index];
                description.text = string.IsNullOrEmpty(o.description) ? Difficulty.Description(o.mode) : o.description;
                if (start != null)
                {
                    start.SetColor(DifficultyStyle.ColorOf(o.mode));
                    start.Interactable = true;
                }
                if (animate && cards[index] != null) UITween.Punch(cards[index].transform, 0.05f, 0.2f);
            };

            for (int i = 0; i < options.Count; i++)
            {
                DifficultyOption o = options[i];
                if (o == null) continue;
                int index = i;
                UIButtonView card = OptionCard(row, o, data.isEndless, () =>
                {
                    if (done) return;
                    if (options[index].locked)
                    {
                        string hint = string.IsNullOrEmpty(options[index].lockedHint) ? "Locked" : options[index].lockedHint;
                        Root.ShowToast(Difficulty.DisplayName(options[index].mode) + ": " + hint);
                        UITween.Punch(cards[index].transform, 0.04f, 0.2f);
                        return;
                    }
                    select(index, true);
                }, out outlines[i]);
                cards[i] = card;
                UITween.Scale(card.transform, Vector3.one * 0.7f, Vector3.one, 0.3f, Ease.OutBack, 0.08f + 0.05f * i);
            }

            // Back | Start along the bottom.
            var size = new Vector2(420f, UITheme.ButtonHeight);
            UIButtonView backButton = UIFactory.Button(p, "Back", back, UITheme.Neutral, size);
            UIFactory.Place(backButton.Rect, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 36f), size);
            start = UIFactory.Button(p, data.startLabel ?? "Start", () =>
            {
                if (done || selected < 0) return;
                done = true;
                DifficultyMode mode = options[selected].mode;
                s.Close();
                onStart?.Invoke(mode);
            }, UITheme.Primary, size);
            UIFactory.Place(start.Rect, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20f, 36f), size);
            start.Interactable = false;

            // Preselect the last used mode, else the first open option.
            int initial = -1;
            for (int i = 0; i < options.Count; i++)
                if (options[i] != null && !options[i].locked && options[i].mode == data.selected) { initial = i; break; }
            for (int i = 0; initial < 0 && i < options.Count; i++)
                if (options[i] != null && !options[i].locked) initial = i;
            select(initial, false);
            return s;
        }

        private static UIButtonView OptionCard(Transform parent, DifficultyOption o, bool endless, Action onTap, out Image outline)
        {
            Color accent = DifficultyStyle.ColorOf(o.mode);
            UIButtonView card = UIFactory.Button(parent, null, onTap, o.locked ? UITheme.Neutral : UITheme.PanelLight,
                                                 new Vector2(OptionWidth, OptionHeight), UITheme.FontBody, 30f);
            Transform face = card.Face.transform;

            // Selection ring (shown for the selected card only).
            outline = UIFactory.Outline(face, UITheme.Highlight, 34f, 8f);
            outline.gameObject.SetActive(false);

            // Coloured header with the name.
            Image header = UIFactory.Panel(face, "Header", o.locked ? UITheme.Shade(accent, 0.45f) : accent, 26f, false);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f),
                            new Vector2(OptionWidth - 20f, 84f));
            Text name = UIFactory.FitLabel(header.transform, string.IsNullOrEmpty(o.title) ? Difficulty.DisplayName(o.mode) : o.title,
                                           UITheme.FontLarge - 12, o.locked ? UITheme.TextDim : UITheme.Text, 8f);
            name.fontStyle = FontStyle.Bold;

            // Modifier lines.
            string mods = o.modifiers != null ? string.Join("\n", o.modifiers) : "";
            Text modText = UIFactory.Label(face, mods, UITheme.FontSmall - 4, o.locked ? UITheme.TextDim : UITheme.Text,
                                           TextAnchor.UpperCenter);
            modText.lineSpacing = 1.05f;
            UIFactory.Place(modText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f),
                            new Vector2(OptionWidth - 30f, 190f));
            modText.verticalOverflow = VerticalWrapMode.Truncate;

            // Record or lock along the bottom.
            if (o.locked)
            {
                Text lockText = UIFactory.Label(face, "LOCKED", UITheme.FontBody - 4, UITheme.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Place(lockText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f),
                                new Vector2(OptionWidth - 30f, 44f));
                if (!string.IsNullOrEmpty(o.lockedHint))
                {
                    Text hint = UIFactory.FitLabel(face, o.lockedHint, UITheme.FontSmall - 6, UITheme.Parchment, 0f);
                    UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f),
                                    new Vector2(OptionWidth - 24f, 44f));
                }
            }
            else if (endless)
            {
                Text best = UIFactory.Label(face, o.bestWave > 0 ? "Best: wave " + o.bestWave : "Not played yet",
                                            UITheme.FontSmall - 2, o.bestWave > 0 ? UITheme.Gold : UITheme.TextDim,
                                            TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Place(best.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f),
                                new Vector2(OptionWidth - 30f, 56f));
            }
            else
            {
                Image[] stars = UIFactory.Stars(face, 3, Mathf.Clamp(o.bestStars, 0, 3), 54f, 10f);
                var starRow = (RectTransform)stars[0].transform.parent;
                UIFactory.Place(starRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), starRow.sizeDelta);
            }
            return card;
        }
    }
}
