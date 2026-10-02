using UnityEngine;

namespace TowerDefense.UI
{
    /// <summary>
    /// World-space SpriteRenderer ring owned by the UI: tower range previews and
    /// the selected-hero marker. Uses the generated ring sprite and the default
    /// sprite material (no shader lookups). Animates in unscaled time.
    /// </summary>
    public sealed class WorldIndicator : MonoBehaviour
    {
        /// <summary>Sorting order for range rings (above towers/enemies, below UI).</summary>
        public static int RangeSortingOrder = 900;
        /// <summary>Sorting order for the hero marker (meant to sit near the ground).</summary>
        public static int MarkerSortingOrder = 5;

        private SpriteRenderer sr;
        private Transform follow;
        private float baseScale = 1f;
        private float shownScale;
        private Color color;
        private bool pulse;

        public bool IsVisible => sr != null && sr.enabled;

        public static WorldIndicator Create(string name, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(name);
            // Lives as long as the (persistent) UI that owns it; the owner destroys it.
            DontDestroyOnLoad(go);
            var ind = go.AddComponent<WorldIndicator>();
            ind.sr = go.AddComponent<SpriteRenderer>();
            ind.sr.sprite = sprite;
            ind.sr.sortingOrder = sortingOrder;
            ind.sr.enabled = false;
            return ind;
        }

        /// <summary>Range ring of <paramref name="range"/> world units around <paramref name="position"/>.</summary>
        public void ShowRange(Vector3 position, float range, Color tint)
        {
            follow = null;
            pulse = false;
            transform.position = new Vector3(position.x, position.y, 0f);
            SetScale(Mathf.Max(0.01f, range) / UISprites.WorldRingEdgeRadius, tint);
        }

        /// <summary>Marker that follows <paramref name="target"/> with a gentle pulse.</summary>
        public void Follow(Transform target, float diameter, Color tint)
        {
            follow = target;
            pulse = true;
            if (target != null) transform.position = target.position;
            SetScale(diameter, tint);
        }

        public void Hide()
        {
            follow = null;
            UITween.Kill(this);
            if (sr != null) sr.enabled = false;
            shownScale = 0f;
        }

        private void SetScale(float scale, Color tint)
        {
            color = tint;
            sr.color = tint;
            float from = sr.enabled && shownScale > 0f ? shownScale : scale * 0.8f;
            baseScale = scale;
            sr.enabled = true;
            UITween.Kill(this);
            UITween.To(this, 0.18f, v =>
            {
                shownScale = Mathf.LerpUnclamped(from, baseScale, v);
                transform.localScale = new Vector3(shownScale, shownScale, 1f);
            }, Ease.OutBack);
        }

        private void LateUpdate()
        {
            if (sr == null || !sr.enabled) return;
            if (follow != null)
            {
                Vector3 p = follow.position;
                transform.position = new Vector3(p.x, p.y, 0f);
            }
            else if (pulse)
            {
                Hide(); // followed object was destroyed
                return;
            }
            if (pulse)
            {
                float a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
                sr.color = UITheme.WithAlpha(color, color.a * a);
            }
        }
    }
}
