using TowerDefense.Pooling;
using TowerDefense.Visuals.Pure;
using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Projectile presentation on top of Projectile.cs (which moves the object
    /// and, when rotateToVelocity is set, faces it along its flight): spins
    /// shells and flasks, pulses orbs, clears trails between pooled uses, and
    /// for splash projectiles plays the impact effect where the shot lands
    /// (when it returns to the pool, or is destroyed if it wasn't pooled).
    /// </summary>
    public sealed class ProjectileVisual : MonoBehaviour, IPoolable
    {
        [SerializeField] private Transform spriteRoot;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private ProjectileArtKind kind;
        [SerializeField] private float spinSpeed;
        [SerializeField] private float pulse;
        [SerializeField] private float impactRadius;
        [SerializeField] private Color impactColor = Color.white;

        private float age;
        private bool impacted;
        private Projectile projectile;

        internal void Setup(Transform sprite, TrailRenderer trailRenderer, ProjectileArtKind artKind, float spin, float pulseAmount,
                            float splashRadius, Color color)
        {
            spriteRoot = sprite;
            trail = trailRenderer;
            kind = artKind;
            spinSpeed = spin;
            pulse = pulseAmount;
            impactRadius = splashRadius;
            impactColor = color;
        }

        private void Awake()
        {
            projectile = GetComponent<Projectile>();
        }

        private void OnEnable()
        {
            age = 0f;
            impacted = false;
            if (spriteRoot != null)
            {
                spriteRoot.localRotation = Quaternion.identity;
                spriteRoot.localScale = Vector3.one;
            }
        }

        void IPoolable.OnSpawnedFromPool()
        {
            impacted = false;
            if (trail != null) trail.Clear(); // no streak from the parking spot to the muzzle
        }

        void IPoolable.OnReturnedToPool()
        {
            Impact();
            if (trail != null) trail.Clear();
        }

        private void LateUpdate()
        {
            if (spriteRoot == null) return;
            float dt = Time.deltaTime;
            age += dt;
            if (spinSpeed != 0f) spriteRoot.Rotate(0f, 0f, spinSpeed * dt);
            if (pulse > 0f)
            {
                float s = 1f + pulse * Mathf.Sin(age * 30f);
                spriteRoot.localScale = new Vector3(s, s, 1f);
            }
        }

        private void OnDestroy()
        {
            // Non-pooled fallback (GameObjectPool.Despawn destroys objects it didn't create).
            if (!impacted && GameplayFx.CanSpawnFrom(this)) Impact();
        }

        private void Impact()
        {
            if (impacted) return;
            impacted = true;
            // The live shot's radius grows with tower upgrades; the template value is the level-1 size.
            float radius = projectile != null && projectile.SplashRadius > 0f ? projectile.SplashRadius : impactRadius;
            if (radius <= 0f || !GameplayFx.CanSpawn) return;
            if (kind == ProjectileArtKind.Flask) GameplayFx.Splash(transform.position, radius, impactColor);
            else GameplayFx.Explosion(transform.position, radius, impactColor);
        }
    }
}
