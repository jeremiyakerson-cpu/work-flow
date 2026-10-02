using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// World-space health bar (background + left-anchored fill) driven by its
    /// owner's visual. Writes renderer state only when values change.
    /// </summary>
    public sealed class HealthBar : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer back;
        [SerializeField] private SpriteRenderer fill;
        [SerializeField] private Transform fillTransform;
        [SerializeField] private float fillWidth = 1f;

        private float shown = -1f;
        private bool visible = true;

        /// <summary>Build a bar under <paramref name="parent"/>; width in world units.</summary>
        public static HealthBar Create(Transform parent, Vector3 localPosition, float width, int order)
        {
            Transform root = VisualBuilder.Child(parent, "HealthBar", localPosition);
            var bar = root.gameObject.AddComponent<HealthBar>();
            bar.back = VisualBuilder.Sprite(root, "Back", SpriteFactory.HealthBarBack, order, Vector3.zero);
            bar.back.transform.localScale = new Vector3(width, 0.7f, 1f);
            // Fill sprite pivots on its left edge: place it at the bar's left and scale x by health.
            float inner = width * 0.92f;
            bar.fill = VisualBuilder.Sprite(root, "Fill", SpriteFactory.HealthBarFill, order + 1, new Vector3(-inner * 0.5f, 0f, 0f));
            bar.fillTransform = bar.fill.transform;
            bar.fillWidth = inner;
            bar.fillTransform.localScale = new Vector3(inner, 0.7f, 1f);
            bar.fill.color = Palette.HealthColor(1f);
            return bar;
        }

        /// <summary>Show or hide both parts.</summary>
        public void SetVisible(bool value)
        {
            if (visible == value && back.enabled == value) return;
            visible = value;
            back.enabled = value;
            fill.enabled = value;
        }

        public bool IsVisible => visible && back != null && back.enabled;

        /// <summary>Update the fill (0..1). Cheap when unchanged.</summary>
        public void SetFraction(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Abs(fraction - shown) < 0.002f) return;
            shown = fraction;
            Vector3 s = fillTransform.localScale;
            s.x = fillWidth * fraction;
            fillTransform.localScale = s;
            fill.color = Palette.HealthColor(fraction);
        }

        /// <summary>Force the next SetFraction to write (after re-enabling).</summary>
        public void Invalidate() => shown = -1f;
    }
}
