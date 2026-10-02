using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// Clock-wipe cooldown overlay (Image.Type.Filled, Radial360 from the top).
    /// The shaded wedge shows the remaining fraction; hidden at 0.
    /// The shared pattern for hero abilities, spells and the next-wave timer.
    /// </summary>
    public sealed class RadialCooldown : MonoBehaviour
    {
        public Image Shade;

        private float remaining = -1f;

        public float Remaining => remaining;
        public bool IsReady => remaining <= 0.001f;

        /// <summary>0 = ready (no shade), 1 = just started.</summary>
        public void SetRemaining(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Abs(fraction - remaining) < 0.002f) return;
            remaining = fraction;
            if (Shade == null) return;
            Shade.fillAmount = fraction;
            Shade.enabled = fraction > 0.001f;
        }

        /// <summary>Convenience for "percent ready" values (HeroUnit.AbilityReadyPercent).</summary>
        public void SetReadyPercent(float ready01) => SetRemaining(1f - ready01);
    }
}
