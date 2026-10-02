using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.UI
{
    /// <summary>Ticks <see cref="UITween"/> tweens in unscaled time. Created automatically; do not add manually.</summary>
    public sealed class UITweenRunner : MonoBehaviour
    {
        private readonly List<Tween> active = new List<Tween>(64);

        internal void Add(Tween t) => active.Add(t);

        internal void Kill(object owner, bool complete)
        {
            for (int i = 0; i < active.Count; i++)
            {
                Tween t = active[i];
                if (t.killed || !ReferenceEquals(t.owner, owner)) continue;
                t.killed = true;
                if (complete)
                {
                    t.update?.Invoke(UITween.Evaluate(t.ease, 1f));
                    t.complete?.Invoke();
                }
            }
        }

        private void Update()
        {
            // Clamp so a long hitch (app resume) doesn't skip whole animations.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            int i = 0;
            while (i < active.Count)
            {
                Tween t = active[i];
                if (!t.killed && t.owner is UnityEngine.Object uo && uo == null) t.killed = true;

                if (!t.killed)
                {
                    if (t.delay > 0f)
                    {
                        t.delay -= dt;
                        if (t.delay <= 0f) t.update?.Invoke(UITween.Evaluate(t.ease, 0f));
                    }
                    else
                    {
                        t.elapsed += dt;
                        float p = Mathf.Clamp01(t.elapsed / t.duration);
                        t.update?.Invoke(UITween.Evaluate(t.ease, p));
                        if (p >= 1f)
                        {
                            t.killed = true;
                            t.complete?.Invoke();
                        }
                    }
                }

                if (t.killed)
                {
                    // Swap-remove; tweens added by callbacks land at the end and are still visited.
                    int last = active.Count - 1;
                    active[i] = active[last];
                    active.RemoveAt(last);
                }
                else
                {
                    i++;
                }
            }
        }

        private void OnDestroy()
        {
            if (UITween.runner == this) UITween.runner = null;
        }
    }
}
