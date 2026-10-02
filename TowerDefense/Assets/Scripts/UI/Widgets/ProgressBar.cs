using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// Horizontal bar whose fill is a 9-sliced rounded rect driven by anchors (keeps
    /// rounded ends, unlike Image.Type.Filled). Writes only when the value changes.
    /// </summary>
    public sealed class ProgressBar : MonoBehaviour
    {
        public RectTransform Fill;
        public Image FillImage;
        public Image Background;

        private float value = -1f;

        public float Value => value;

        public void SetValue(float v)
        {
            v = Mathf.Clamp01(v);
            if (Mathf.Abs(v - value) < 0.002f) return;
            value = v;
            if (Fill != null) Fill.anchorMax = new Vector2(v, 1f);
            if (FillImage != null) FillImage.enabled = v > 0.002f;
        }

        public void SetColor(Color c)
        {
            if (FillImage != null && FillImage.color != c) FillImage.color = c;
        }
    }
}
