using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefense.Visuals
{
    /// <summary>Small helpers for assembling sprite hierarchies from code.</summary>
    public static class VisualBuilder
    {
        /// <summary>Empty child at a local position.</summary>
        public static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        /// <summary>SpriteRenderer child using the shared sprite material.</summary>
        public static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order, Vector3 localPosition, float scale = 1f)
        {
            Transform t = Child(parent, name, localPosition);
            if (scale != 1f) t.localScale = new Vector3(scale, scale, 1f);
            return AddRenderer(t.gameObject, sprite, order, Color.white);
        }

        /// <summary>SpriteRenderer child with a colour tint.</summary>
        public static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order, Vector3 localPosition, float scale, Color color)
        {
            SpriteRenderer r = Sprite(parent, name, sprite, order, localPosition, scale);
            r.color = color;
            return r;
        }

        /// <summary>Add a SpriteRenderer to an existing object.</summary>
        public static SpriteRenderer AddRenderer(GameObject go, Sprite sprite, int order, Color color)
        {
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sharedMaterial = SpriteFactory.SpriteMaterial;
            r.sortingOrder = order;
            r.color = color;
            return r;
        }

        /// <summary>SortingGroup so a multi-sprite object sorts as one unit at <paramref name="order"/>.</summary>
        public static SortingGroup Group(GameObject go, int order)
        {
            var g = go.AddComponent<SortingGroup>();
            g.sortingOrder = order;
            return g;
        }

        /// <summary>Set alpha without touching RGB; skips the write when unchanged.</summary>
        public static void SetAlpha(SpriteRenderer r, float a)
        {
            Color c = r.color;
            if (Mathf.Approximately(c.a, a)) return;
            c.a = a;
            r.color = c;
        }

        /// <summary>Enable/disable a renderer only when the state changes.</summary>
        public static void SetVisible(Renderer r, bool visible)
        {
            if (r != null && r.enabled != visible) r.enabled = visible;
        }
    }
}
