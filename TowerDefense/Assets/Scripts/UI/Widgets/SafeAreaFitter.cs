using System;
using UnityEngine;

namespace TowerDefense.UI
{
    /// <summary>
    /// Fits its RectTransform to Screen.safeArea (notch, rounded corners, home
    /// indicator). Re-applies when the safe area or resolution changes. Put it on a
    /// full-screen child of the canvas and parent safe content under it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>Editor testing: a non-zero rect overrides Screen.safeArea (pixels).</summary>
        public static Rect SimulatedSafeArea;

        /// <summary>Raised after any fitter applied a new safe area.</summary>
        public static event Action Changed;

        private RectTransform rt;
        private Rect lastSafe;
        private int lastW, lastH;

        /// <summary>Current safe area in screen pixels (honours the simulated override).</summary>
        public static Rect CurrentSafeArea =>
            SimulatedSafeArea.width > 0f && SimulatedSafeArea.height > 0f ? SimulatedSafeArea : Screen.safeArea;

        private void Awake()
        {
            rt = (RectTransform)transform;
            Apply();
        }

        private void OnEnable() => Apply();

        private void Update()
        {
            Rect safe = CurrentSafeArea;
            if (!Same(safe, lastSafe) || Screen.width != lastW || Screen.height != lastH) Apply();
        }

        private static bool Same(Rect a, Rect b) =>
            a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;

        private void Apply()
        {
            if (rt == null) rt = (RectTransform)transform;
            Rect safe = CurrentSafeArea;
            int w = Screen.width, h = Screen.height;
            lastSafe = safe;
            lastW = w;
            lastH = h;
            if (w <= 0 || h <= 0 || safe.width <= 0f || safe.height <= 0f) return;

            rt.anchorMin = new Vector2(safe.xMin / w, safe.yMin / h);
            rt.anchorMax = new Vector2(safe.xMax / w, safe.yMax / h);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Changed?.Invoke();
        }
    }
}
