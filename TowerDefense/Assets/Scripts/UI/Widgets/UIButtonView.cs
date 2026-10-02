using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// References to the parts of a factory-built button plus helpers that only
    /// touch the UI when a value actually changes.
    /// </summary>
    public sealed class UIButtonView : MonoBehaviour
    {
        public Button Button;
        /// <summary>Darker base (gives the button depth). Raycast target.</summary>
        public Image Shade;
        /// <summary>Coloured face; Button.targetGraphic.</summary>
        public Image Face;
        public Text Label;
        public Image Icon;
        public Color BaseColor;

        private bool interactableSet = true;

        public RectTransform Rect => (RectTransform)transform;

        public bool Interactable
        {
            get => Button != null && Button.interactable;
            set
            {
                if (Button == null) return;
                if (Button.interactable == value && interactableSet == value) return;
                interactableSet = value;
                Button.interactable = value;
                if (Shade != null) Shade.color = value ? UITheme.Shade(BaseColor, 0.6f) : UITheme.Shade(UITheme.Disabled, 0.6f);
                if (Label != null) Label.color = UITheme.WithAlpha(Label.color, value ? 1f : 0.55f);
                if (Icon != null) Icon.color = UITheme.WithAlpha(Icon.color, value ? 1f : 0.5f);
            }
        }

        public void SetLabel(string text)
        {
            if (Label != null && Label.text != text) Label.text = text;
        }

        public void SetColor(Color color)
        {
            BaseColor = color;
            if (Face != null) Face.color = color;
            if (Shade != null) Shade.color = UITheme.Shade(Interactable ? color : UITheme.Disabled, 0.6f);
        }

        public void SetIcon(Sprite sprite)
        {
            if (Icon == null) return;
            Icon.sprite = sprite;
            Icon.enabled = sprite != null;
        }
    }
}
