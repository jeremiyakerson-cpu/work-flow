using TowerDefense.Pooling;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Barricade presentation: crack overlays appear as health drops, a short
    /// shake on each hit, a debris puff on <see cref="Barricade.Broken"/>.
    /// Pool-safe: state resets on every enable/spawn.
    /// </summary>
    public sealed class BarricadeVisual : MonoBehaviour, IPoolable
    {
        [SerializeField] private Barricade barricade;
        [SerializeField] private SortingGroup group;
        [SerializeField] private Transform body;
        [SerializeField] private SpriteRenderer cracksLight;
        [SerializeField] private SpriteRenderer cracksHeavy;

        private const float ShakeDuration = 0.18f;

        private float lastHealth = 1f;
        private float shake;

        internal void Setup(Barricade owner, SortingGroup sortingGroup, Transform bodyRoot, SpriteRenderer light, SpriteRenderer heavy)
        {
            barricade = owner;
            group = sortingGroup;
            body = bodyRoot;
            cracksLight = light;
            cracksHeavy = heavy;
        }

        private void OnEnable() => ResetState();

        private void OnDisable()
        {
            if (barricade != null) barricade.Broken -= OnBroken;
        }

        void IPoolable.OnSpawnedFromPool() => ResetState();

        void IPoolable.OnReturnedToPool() { }

        private void ResetState()
        {
            // Broken is cleared by the barricade after it fires, so (re)subscribe on every spawn.
            if (barricade != null)
            {
                barricade.Broken -= OnBroken;
                barricade.Broken += OnBroken;
            }
            lastHealth = 1f;
            shake = 0f;
            if (cracksLight != null) cracksLight.enabled = false;
            if (cracksHeavy != null) cracksHeavy.enabled = false;
            if (body != null) body.localPosition = Vector3.zero;
            if (group != null) group.sortingOrder = SortingOrders.ForY(SortingOrders.Units, transform.position.y);
        }

        private void OnBroken(Barricade b)
        {
            if (GameplayFx.CanSpawn) GameplayFx.DeathPuff(transform.position + new Vector3(0f, 0.25f, 0f), Palette.Dust, 1.2f);
        }

        private void LateUpdate()
        {
            if (barricade == null) return;
            float hp = barricade.HealthPercent();
            if (hp < lastHealth - 1e-4f) shake = ShakeDuration;
            lastHealth = hp;
            VisualBuilder.SetVisible(cracksLight, hp < 0.67f);
            VisualBuilder.SetVisible(cracksHeavy, hp < 0.34f);

            if (body == null) return;
            if (shake > 0f)
            {
                shake -= Time.deltaTime;
                float k = Mathf.Max(0f, shake / ShakeDuration);
                body.localPosition = new Vector3(Mathf.Sin(shake * 90f) * 0.05f * k, 0f, 0f);
            }
            else if (body.localPosition != Vector3.zero) body.localPosition = Vector3.zero;
        }
    }
}
