using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Barricade presentation: crack overlays appear as health drops, a short
    /// shake on each hit, debris puff when it breaks.
    /// </summary>
    public sealed class BarricadeVisual : MonoBehaviour
    {
        [SerializeField] private Barricade barricade;
        [SerializeField] private SortingGroup group;
        [SerializeField] private Transform body;
        [SerializeField] private SpriteRenderer cracksLight;
        [SerializeField] private SpriteRenderer cracksHeavy;

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

        private void OnEnable()
        {
            lastHealth = 1f;
            shake = 0f;
            if (cracksLight != null) cracksLight.enabled = false;
            if (cracksHeavy != null) cracksHeavy.enabled = false;
            if (group != null) group.sortingOrder = SortingOrders.ForY(SortingOrders.Units, transform.position.y);
        }

        private void LateUpdate()
        {
            if (barricade == null) return;
            float hp = barricade.HealthPercent();
            if (hp < lastHealth - 1e-4f) shake = 0.18f;
            lastHealth = hp;
            VisualBuilder.SetVisible(cracksLight, hp < 0.67f);
            VisualBuilder.SetVisible(cracksHeavy, hp < 0.34f);

            if (body != null)
            {
                if (shake > 0f)
                {
                    shake -= Time.deltaTime;
                    body.localPosition = new Vector3(Mathf.Sin(shake * 90f) * 0.05f * (shake / 0.18f), 0f, 0f);
                }
                else body.localPosition = Vector3.zero;
            }
        }

        private void OnDestroy()
        {
            if (GameplayFx.CanSpawnFrom(this)) GameplayFx.DeathPuff(transform.position, Palette.Dust, 1.2f);
        }
    }
}
