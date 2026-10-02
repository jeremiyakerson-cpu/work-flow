using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Enemy presentation: walk bob (or hover for flyers), faces its movement
    /// direction, white hit flash on <see cref="Enemy.Damaged"/>, blue tint while
    /// slowed and green while poisoned, a health bar once damaged, wing flaps,
    /// boss aura pulse, and y-sorting in the Units band. Death puffs and gold
    /// popups are spawned by <see cref="GameplayFx"/> from Enemy.AnyDied.
    /// </summary>
    public sealed class EnemyVisual : MonoBehaviour
    {
        [SerializeField] private Enemy enemy;
        [SerializeField] private SortingGroup group;
        [SerializeField] private Transform bodyPivot;   // bobs and flips
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer flash;
        [SerializeField] private SpriteRenderer shadow;
        [SerializeField] private Transform wingBack;
        [SerializeField] private Transform wingFront;
        [SerializeField] private SpriteRenderer aura;
        [SerializeField] private HealthBar healthBar;
        [SerializeField] private float hoverHeight;
        [SerializeField] private float bobAmplitude = 0.06f;
        [SerializeField] private float bobFrequency = 9f;
        [SerializeField] private bool alwaysShowHealth;

        private const float FlashDuration = 0.12f;

        private Vector3 lastPosition;
        private float facing = 1f;
        private float phase;
        private float flashTimer;
        private bool damaged;
        private int sortOrder = int.MinValue;
        private Color shownTint = new Color(-1f, 0f, 0f, 0f);
        private Vector3 pivotScale = Vector3.one;

        internal void Setup(Enemy owner, SortingGroup sortingGroup, Transform pivot, SpriteRenderer bodyRenderer, SpriteRenderer flashRenderer,
                            SpriteRenderer shadowRenderer, Transform backWing, Transform frontWing, SpriteRenderer auraRenderer,
                            HealthBar bar, float hover, bool showHealthAlways)
        {
            enemy = owner;
            group = sortingGroup;
            bodyPivot = pivot;
            body = bodyRenderer;
            flash = flashRenderer;
            shadow = shadowRenderer;
            wingBack = backWing;
            wingFront = frontWing;
            aura = auraRenderer;
            healthBar = bar;
            hoverHeight = hover;
            alwaysShowHealth = showHealthAlways;
        }

        private void OnEnable()
        {
            if (enemy != null) enemy.Damaged += OnDamaged;
            lastPosition = transform.position;
            phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            flashTimer = 0f;
            damaged = false;
            sortOrder = int.MinValue;
            shownTint = new Color(-1f, 0f, 0f, 0f);
            if (bodyPivot != null) pivotScale = new Vector3(Mathf.Abs(bodyPivot.localScale.x), bodyPivot.localScale.y, 1f);
            if (flash != null) flash.enabled = false;
            if (healthBar != null) { healthBar.Invalidate(); healthBar.SetVisible(alwaysShowHealth); }
        }

        private void OnDisable()
        {
            if (enemy != null) enemy.Damaged -= OnDamaged;
        }

        private void OnDamaged(Enemy e, float amount)
        {
            damaged = true;
            // Poison ticks every frame; only flash on real hits.
            if (e.IsPoisoned && amount < e.maxHealth * 0.04f) return;
            flashTimer = FlashDuration;
        }

        private void LateUpdate()
        {
            if (enemy == null) return;
            float dt = Time.deltaTime;
            Vector3 p = transform.position;
            Vector3 delta = p - lastPosition;
            lastPosition = p;
            bool moving = delta.sqrMagnitude > 1e-7f && !enemy.IsBlocked;

            // Face the direction of travel (sprites are drawn facing +x).
            if (Mathf.Abs(delta.x) > 0.0005f) facing = delta.x >= 0f ? 1f : -1f;

            phase += dt * bobFrequency * (moving ? 1f : 0.35f);
            if (bodyPivot != null)
            {
                float y = hoverHeight > 0f
                    ? hoverHeight + Mathf.Sin(phase * 0.5f) * 0.07f
                    : Mathf.Abs(Mathf.Sin(phase)) * bobAmplitude;
                float squash = hoverHeight > 0f ? 1f : 1f + (Mathf.Abs(Mathf.Sin(phase)) - 0.5f) * 0.06f;
                bodyPivot.localPosition = new Vector3(0f, y, 0f);
                bodyPivot.localScale = new Vector3(pivotScale.x * facing / squash, pivotScale.y * squash, 1f);
            }

            if (wingBack != null || wingFront != null)
            {
                float flap = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(phase * 1.6f));
                if (wingBack != null) wingBack.localScale = new Vector3(0.9f, flap * 0.9f, 1f);
                if (wingFront != null) wingFront.localScale = new Vector3(1f, flap, 1f);
            }

            if (aura != null)
            {
                float a = 0.45f + 0.2f * Mathf.Sin(Time.time * 4f);
                VisualBuilder.SetAlpha(aura, a);
            }

            // Status tint: slowed blue, poisoned green (both: a blend).
            Color tint = Color.white;
            if (enemy.IsSlowed) tint = Palette.SlowTint;
            if (enemy.IsPoisoned) tint = enemy.IsSlowed ? Color.Lerp(Palette.SlowTint, Palette.PoisonTint, 0.5f) : Palette.PoisonTint;
            if (body != null && tint != shownTint)
            {
                shownTint = tint;
                body.color = tint;
            }

            if (flash != null)
            {
                bool on = flashTimer > 0f;
                if (on) flashTimer -= dt;
                VisualBuilder.SetVisible(flash, on);
                if (on) VisualBuilder.SetAlpha(flash, 0.85f * Mathf.Clamp01(flashTimer / FlashDuration));
            }

            if (healthBar != null)
            {
                float hp = enemy.HealthPercent();
                bool show = alwaysShowHealth || damaged || hp < 0.999f;
                healthBar.SetVisible(show);
                if (show) healthBar.SetFraction(hp);
            }

            int order = SortingOrders.ForY(SortingOrders.Units, p.y);
            if (order != sortOrder && group != null)
            {
                sortOrder = order;
                group.sortingOrder = order;
            }
        }
    }
}
