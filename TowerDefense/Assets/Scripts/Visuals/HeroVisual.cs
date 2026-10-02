using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Hero presentation: walk bob and facing while moving, health bar when hurt,
    /// a flash plus shockwave ring on <see cref="HeroUnit.AbilityUsed"/>, and a
    /// faded floating ghost while dead. HeroUnit hides every child renderer on
    /// death and re-enables them all on respawn, so this restores its own
    /// overlay states in the Died/Respawned handlers.
    /// </summary>
    public sealed class HeroVisual : MonoBehaviour
    {
        [SerializeField] private HeroUnit hero;
        [SerializeField] private SortingGroup group;
        [SerializeField] private Transform bodyPivot;
        [SerializeField] private SpriteRenderer flash;
        [SerializeField] private SpriteRenderer ghost;
        [SerializeField] private SpriteRenderer selection;
        [SerializeField] private HealthBar healthBar;
        [SerializeField] private float ghostBaseY = 0.4f;

        private const float FlashDuration = 0.25f;

        private Vector3 lastPosition;
        private float facing = 1f;
        private float phase;
        private float flashTimer;
        private int sortOrder = int.MinValue;

        internal void Setup(HeroUnit owner, SortingGroup sortingGroup, Transform pivot, SpriteRenderer flashRenderer,
                            SpriteRenderer ghostRenderer, SpriteRenderer ring, HealthBar bar, float ghostY)
        {
            ghostBaseY = ghostY;
            hero = owner;
            group = sortingGroup;
            bodyPivot = pivot;
            flash = flashRenderer;
            ghost = ghostRenderer;
            selection = ring;
            healthBar = bar;
        }

        private void OnEnable()
        {
            if (hero != null)
            {
                hero.Died += OnDied;
                hero.Respawned += OnRespawned;
                hero.AbilityUsed += OnAbilityUsed;
            }
            lastPosition = transform.position;
            flashTimer = 0f;
            sortOrder = int.MinValue;
            if (ghost != null) ghost.enabled = false;
            if (flash != null) flash.enabled = false;
            if (healthBar != null) healthBar.SetVisible(false);
        }

        private void OnDisable()
        {
            if (hero == null) return;
            hero.Died -= OnDied;
            hero.Respawned -= OnRespawned;
            hero.AbilityUsed -= OnAbilityUsed;
        }

        private void OnDied(HeroUnit h)
        {
            // HeroUnit has just disabled every renderer; show only the ghost.
            if (ghost != null)
            {
                ghost.enabled = true;
                VisualBuilder.SetAlpha(ghost, 0.25f);
            }
            if (GameplayFx.CanSpawn) GameplayFx.DeathPuff(transform.position, Palette.Hero, 1.1f);
        }

        private void OnRespawned(HeroUnit h)
        {
            // HeroUnit re-enabled everything, including overlays that should stay hidden.
            if (ghost != null) ghost.enabled = false;
            if (flash != null) flash.enabled = false;
            if (healthBar != null) { healthBar.Invalidate(); healthBar.SetVisible(false); }
            if (GameplayFx.CanSpawn) GameplayFx.Sparkle(transform.position + new Vector3(0f, 0.4f, 0f), Palette.Hero);
        }

        private void OnAbilityUsed(HeroUnit h)
        {
            flashTimer = FlashDuration;
            if (GameplayFx.CanSpawn) GameplayFx.Shockwave(transform.position, h.abilityRadius, Palette.Hero);
        }

        private void LateUpdate()
        {
            if (hero == null) return;
            float dt = Time.deltaTime;
            Vector3 p = transform.position;
            Vector3 delta = p - lastPosition;
            lastPosition = p;

            if (hero.IsDead)
            {
                if (ghost != null)
                {
                    phase += dt * 3f;
                    ghost.transform.localPosition = new Vector3(0f, ghostBaseY + 0.12f + Mathf.Sin(phase) * 0.08f, 0f);
                    VisualBuilder.SetAlpha(ghost, 0.2f + 0.35f * hero.RespawnPercent);
                }
                UpdateSorting(p.y);
                return;
            }

            if (Mathf.Abs(delta.x) > 0.0005f) facing = delta.x >= 0f ? 1f : -1f;
            bool moving = hero.IsMoving;
            phase += dt * (moving ? 12f : 2.5f);
            if (bodyPivot != null)
            {
                float y = moving ? Mathf.Abs(Mathf.Sin(phase)) * 0.08f : Mathf.Sin(phase) * 0.015f;
                bodyPivot.localPosition = new Vector3(0f, y, 0f);
                bodyPivot.localScale = new Vector3(facing, 1f, 1f);
            }

            if (flash != null)
            {
                bool on = flashTimer > 0f;
                if (on) flashTimer -= dt;
                VisualBuilder.SetVisible(flash, on);
                if (on) VisualBuilder.SetAlpha(flash, 0.9f * Mathf.Clamp01(flashTimer / FlashDuration));
            }

            if (selection != null) VisualBuilder.SetAlpha(selection, 0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f));

            if (healthBar != null)
            {
                float hp = hero.HealthPercent();
                bool show = hp < 0.999f;
                healthBar.SetVisible(show);
                if (show) healthBar.SetFraction(hp);
            }

            UpdateSorting(p.y);
        }

        private void UpdateSorting(float y)
        {
            int order = SortingOrders.ForY(SortingOrders.Units, y);
            if (order == sortOrder || group == null) return;
            sortOrder = order;
            group.sortingOrder = order;
        }
    }
}
