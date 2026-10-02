using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.UI
{
    /// <summary>Easing curves for <see cref="UITween"/>.</summary>
    public enum Ease { Linear, InQuad, OutQuad, InOutQuad, OutCubic, InCubic, OutBack, OutElastic }

    /// <summary>A running tween. Keep the handle to Kill() it early.</summary>
    public sealed class Tween
    {
        internal object owner;
        internal float duration, delay, elapsed;
        internal Ease ease;
        internal Action<float> update;
        internal Action complete;
        internal bool killed;

        public bool IsActive => !killed;

        /// <summary>Stop without calling onComplete.</summary>
        public void Kill() => killed = true;
    }

    /// <summary>
    /// Tiny unscaled-time tween engine for UI (works while Time.timeScale is 0).
    /// One hidden runner ticks every active tween; tweens owned by a destroyed
    /// UnityEngine.Object stop automatically. Tweens allocate only when created
    /// (on events), never per frame.
    /// </summary>
    public static class UITween
    {
        internal static UITweenRunner runner;

        /// <summary>Generic 0..1 tween; <paramref name="onUpdate"/> receives the eased value.</summary>
        public static Tween To(object owner, float duration, Action<float> onUpdate, Ease ease = Ease.OutCubic,
                               float delay = 0f, Action onComplete = null)
        {
            var t = new Tween
            {
                owner = owner,
                duration = Mathf.Max(0.0001f, duration),
                delay = Mathf.Max(0f, delay),
                ease = ease,
                update = onUpdate,
                complete = onComplete,
            };
            EnsureRunner().Add(t);
            if (t.delay <= 0f) onUpdate?.Invoke(Evaluate(ease, 0f));
            return t;
        }

        /// <summary>Wait then call back (unscaled).</summary>
        public static Tween Delay(object owner, float seconds, Action onComplete) =>
            To(owner, seconds, null, Ease.Linear, 0f, onComplete);

        public static Tween Scale(Transform target, Vector3 from, Vector3 to, float duration, Ease ease = Ease.OutBack,
                                  float delay = 0f, Action onComplete = null)
        {
            if (target == null) return null;
            Kill(target);
            if (delay > 0f) target.localScale = from;
            return To(target, duration, v =>
            {
                if (target != null) target.localScale = Vector3.LerpUnclamped(from, to, v);
            }, ease, delay, onComplete);
        }

        /// <summary>Quick scale pop that settles at scale 1 (button feedback, counters).</summary>
        public static Tween Punch(Transform target, float amount = 0.18f, float duration = 0.3f)
        {
            if (target == null) return null;
            Kill(target);
            Vector3 baseScale = Vector3.one;
            return To(target, duration, v =>
            {
                if (target == null) return;
                float s = 1f + amount * Mathf.Sin(v * Mathf.PI) * (1f - v * 0.5f);
                target.localScale = baseScale * s;
            }, Ease.Linear, 0f, () => { if (target != null) target.localScale = baseScale; });
        }

        public static Tween Fade(CanvasGroup group, float from, float to, float duration, Ease ease = Ease.OutQuad,
                                 float delay = 0f, Action onComplete = null)
        {
            if (group == null) return null;
            Kill(group);
            if (delay > 0f) group.alpha = from;
            return To(group, duration, v =>
            {
                if (group != null) group.alpha = Mathf.LerpUnclamped(from, to, v);
            }, ease, delay, onComplete);
        }

        public static Tween Move(RectTransform target, Vector2 from, Vector2 to, float duration, Ease ease = Ease.OutCubic,
                                 float delay = 0f, Action onComplete = null)
        {
            if (target == null) return null;
            Kill(target);
            if (delay > 0f) target.anchoredPosition = from;
            return To(target, duration, v =>
            {
                if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, v);
            }, ease, delay, onComplete);
        }

        public static Tween Color(UnityEngine.UI.Graphic target, Color from, Color to, float duration,
                                  Ease ease = Ease.OutQuad, float delay = 0f, Action onComplete = null)
        {
            if (target == null) return null;
            Kill(target);
            return To(target, duration, v =>
            {
                if (target != null) target.color = UnityEngine.Color.LerpUnclamped(from, to, v);
            }, ease, delay, onComplete);
        }

        /// <summary>Stop every tween owned by <paramref name="owner"/>.</summary>
        public static void Kill(object owner, bool complete = false)
        {
            if (runner == null || owner == null) return;
            runner.Kill(owner, complete);
        }

        public static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case Ease.OutElastic:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    const float c4 = 2f * Mathf.PI / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                default: return t;
            }
        }

        private static UITweenRunner EnsureRunner()
        {
            if (runner == null)
            {
                var go = new GameObject("[UITween]");
                go.hideFlags = HideFlags.HideInHierarchy;
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<UITweenRunner>();
            }
            return runner;
        }
    }
}
