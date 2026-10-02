using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// Top-centre announcement that slides down, holds, and slides away
    /// ("BOSS INCOMING", "FINAL WAVE"). Unscaled time; never blocks touches.
    /// </summary>
    public sealed class Banner
    {
        private const float Height = 150f;

        private readonly RectTransform root;
        private readonly Image panel;
        private readonly Text title;
        private readonly Text subtitle;
        private readonly CanvasGroup group;

        public Banner(RectTransform parent)
        {
            panel = UIFactory.Panel(parent, "Banner", UITheme.Danger, 30f, false);
            root = panel.rectTransform;
            UIFactory.Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, Height + 40f), new Vector2(860f, Height));
            UIFactory.Outline(panel.transform, UITheme.Gold, 30f, 0f);
            title = UIFactory.Label(panel.transform, "", UITheme.FontLarge + 8, UITheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(820f, 80f));
            subtitle = UIFactory.Label(panel.transform, "", UITheme.FontBody, UITheme.Parchment);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(820f, 50f));
            group = UIFactory.Group(panel.gameObject);
            group.blocksRaycasts = false;
            group.interactable = false;
            root.gameObject.SetActive(false);
        }

        public void Show(string titleText, string subtitleText, Color color, float hold = 2.4f)
        {
            title.text = titleText;
            subtitle.text = subtitleText;
            panel.color = color;
            root.gameObject.SetActive(true);
            group.alpha = 1f;
            Vector2 hidden = new Vector2(0f, Height + 40f);
            Vector2 shown = new Vector2(0f, -150f);
            UITween.Move(root, hidden, shown, 0.45f, Ease.OutBack, 0f, () =>
                UITween.Move(root, shown, hidden, 0.35f, Ease.InCubic, hold, () =>
                {
                    if (root != null) root.gameObject.SetActive(false);
                }));
            UITween.Punch(title.transform, 0.15f, 0.5f);
        }

        public void Hide()
        {
            UITween.Kill(root);
            root.gameObject.SetActive(false);
        }
    }
}
