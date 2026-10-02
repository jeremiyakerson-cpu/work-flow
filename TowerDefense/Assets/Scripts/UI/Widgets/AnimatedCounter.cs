using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// Numeric label that rolls to new values, flashes green/red and pops.
    /// Text is only rewritten when the displayed integer changes (cached strings).
    /// </summary>
    public sealed class AnimatedCounter : MonoBehaviour
    {
        public Text Label;
        public Transform PunchTarget;
        public float duration = 0.45f;
        public bool flashOnChange = true;

        private int target;
        private float shown;
        private int rendered = int.MinValue;
        private bool initialized;
        private Color baseColor;

        public int Value => target;

        public void SetImmediate(int value)
        {
            UITween.Kill(this);
            if (Label != null && !initialized) baseColor = Label.color;
            initialized = true;
            target = value;
            shown = value;
            Render();
        }

        public void SetValue(int value)
        {
            if (!initialized) { SetImmediate(value); return; }
            if (value == target) return;

            float from = shown;
            bool up = value > target;
            target = value;
            UITween.Kill(this);
            UITween.To(this, duration, t =>
            {
                shown = Mathf.Lerp(from, target, t);
                Render();
            }, Ease.OutCubic);

            if (flashOnChange && Label != null)
            {
                UITween.Color(Label, up ? UITheme.TextGood : UITheme.TextBad, baseColor, 0.6f, Ease.InQuad);
                UITween.Punch(PunchTarget != null ? PunchTarget : Label.transform, up ? 0.14f : 0.22f, 0.3f);
            }
        }

        private void Render()
        {
            int v = Mathf.RoundToInt(shown);
            if (v == rendered || Label == null) return;
            rendered = v;
            Label.text = NumberCache.Get(v);
        }
    }
}
