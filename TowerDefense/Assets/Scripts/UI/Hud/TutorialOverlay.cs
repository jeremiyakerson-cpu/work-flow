using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>First-level hints, completed by doing the thing they describe.</summary>
    public enum TutorialStep { Build, MoveHero, CallWave }

    /// <summary>
    /// Non-blocking hint card at the bottom centre for the first level: "Tap a
    /// build spot", "Tap your hero, then the road", "Call the next wave early".
    /// Each step completes when GameHud reports the matching action; the player
    /// can skip at any time. Only the card itself catches touches.
    /// </summary>
    public sealed class TutorialOverlay
    {
        public event Action Finished;

        public bool IsActive { get; private set; }
        public TutorialStep Current => steps.Count > 0 && index < steps.Count ? steps[index] : TutorialStep.CallWave;

        private readonly RectTransform root;
        private readonly CanvasGroup group;
        private readonly Text counter;
        private readonly Text message;
        private readonly List<TutorialStep> steps = new List<TutorialStep>(3);
        private int index;

        public TutorialOverlay(RectTransform parent)
        {
            Image card = UIFactory.Panel(parent, "Tutorial", UITheme.WithAlpha(UITheme.Parchment, 0.97f), 30f, true);
            root = card.rectTransform;
            UIFactory.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, UITheme.Margin), new Vector2(980f, 170f));
            UIFactory.Outline(card.transform, UITheme.PanelBorder, 30f, 0f);

            counter = UIFactory.Label(card.transform, "", UITheme.FontSmall, UITheme.Shade(UITheme.TextDark, 1.6f), TextAnchor.UpperLeft, FontStyle.Bold, false);
            UIFactory.Place(counter.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -16f), new Vector2(200f, 40f));

            message = UIFactory.Label(card.transform, "", UITheme.FontBody + 2, UITheme.TextDark, TextAnchor.MiddleLeft, FontStyle.Bold, false);
            UIFactory.Place(message.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, -12f), new Vector2(700f, 110f));

            UIButtonView skip = UIFactory.Button(card.transform, "Skip", Skip, UITheme.Neutral, new Vector2(200f, UITheme.MinTouch), UITheme.FontBody);
            UIFactory.Place(skip.Rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(200f, UITheme.MinTouch));

            group = UIFactory.Group(card.gameObject);
            root.gameObject.SetActive(false);
        }

        /// <summary>Begin the hint sequence (the hero step is left out for hero-less levels).</summary>
        public void Begin(bool includeHero)
        {
            steps.Clear();
            steps.Add(TutorialStep.Build);
            if (includeHero) steps.Add(TutorialStep.MoveHero);
            steps.Add(TutorialStep.CallWave);
            index = 0;
            IsActive = true;
            root.gameObject.SetActive(true);
            ShowCurrent();
        }

        /// <summary>Report that the player did <paramref name="step"/>; advances if it is the current hint.</summary>
        public void Complete(TutorialStep step)
        {
            if (!IsActive || steps[index] != step) return;
            index++;
            if (index >= steps.Count) Finish();
            else ShowCurrent();
        }

        public void Skip()
        {
            if (IsActive) Finish();
        }

        /// <summary>Hide without raising Finished (level teardown).</summary>
        public void Hide()
        {
            IsActive = false;
            UITween.Kill(group);
            root.gameObject.SetActive(false);
        }

        private void ShowCurrent()
        {
            counter.text = (index + 1) + " / " + steps.Count;
            message.text = HintText(steps[index]);
            UITween.Fade(group, 0f, 1f, UITheme.NormalAnim);
            UITween.Move(root, new Vector2(0f, UITheme.Margin - 40f), new Vector2(0f, UITheme.Margin), 0.35f, Ease.OutBack);
        }

        private void Finish()
        {
            IsActive = false;
            UITween.Fade(group, group.alpha, 0f, UITheme.NormalAnim, Ease.InQuad, 0f, () =>
            {
                if (root != null) root.gameObject.SetActive(false);
            });
            Finished?.Invoke();
        }

        private static string HintText(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Build: return "Tap a build spot to place a tower.";
                case TutorialStep.MoveHero: return "Drag your hero onto the road, or tap the hero and then the road.";
                default: return "Tap the pulsing wave button to call the next wave early for bonus gold.";
            }
        }
    }
}
