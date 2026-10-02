using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>Gentle scale/alpha breathing (portal glow, spawn markers).</summary>
    public sealed class Pulse : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private float speed = 2f;
        [SerializeField] private float scaleAmount = 0.06f;
        [SerializeField] private float minAlpha = 0.6f;
        [SerializeField] private float maxAlpha = 1f;

        private Vector3 baseScale;
        private float phase;

        public void Setup(SpriteRenderer renderer, float pulseSpeed, float scale, float alphaMin, float alphaMax)
        {
            target = renderer;
            speed = pulseSpeed;
            scaleAmount = scale;
            minAlpha = alphaMin;
            maxAlpha = alphaMax;
            baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            baseScale = transform.localScale;
            phase = UnityEngine.Random.Range(0f, 6.28f);
        }

        private void Update()
        {
            phase += Time.deltaTime * speed;
            float s = 0.5f + 0.5f * Mathf.Sin(phase);
            transform.localScale = baseScale * (1f + scaleAmount * s);
            if (target != null) VisualBuilder.SetAlpha(target, Mathf.Lerp(minAlpha, maxAlpha, s));
        }
    }
}
