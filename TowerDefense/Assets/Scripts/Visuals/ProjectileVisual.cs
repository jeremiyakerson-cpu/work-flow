using TowerDefense.Visuals.Pure;
using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Projectile presentation: faces its velocity (arrows, shards), spins
    /// (shells, flasks) or pulses (orbs), and for splash projectiles plays an
    /// impact effect when the projectile is destroyed. Projectile.cs moves the
    /// transform; this only reads the position delta.
    /// </summary>
    public sealed class ProjectileVisual : MonoBehaviour
    {
        [SerializeField] private Transform spriteRoot;
        [SerializeField] private ProjectileArtKind kind;
        [SerializeField] private bool faceVelocity = true;
        [SerializeField] private float spinSpeed;
        [SerializeField] private float pulse;
        [SerializeField] private float impactRadius;
        [SerializeField] private Color impactColor = Color.white;

        private Vector3 lastPosition;
        private float age;

        internal void Setup(Transform sprite, ProjectileArtKind artKind, bool face, float spin, float pulseAmount, float splashRadius, Color color)
        {
            spriteRoot = sprite;
            kind = artKind;
            faceVelocity = face;
            spinSpeed = spin;
            pulse = pulseAmount;
            impactRadius = splashRadius;
            impactColor = color;
        }

        private void OnEnable()
        {
            lastPosition = transform.position;
            age = 0f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            age += dt;
            Vector3 p = transform.position;
            Vector3 v = p - lastPosition;
            lastPosition = p;

            if (faceVelocity && v.sqrMagnitude > 1e-8f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
            if (spriteRoot == null) return;
            if (spinSpeed != 0f) spriteRoot.Rotate(0f, 0f, spinSpeed * dt);
            if (pulse > 0f)
            {
                float s = 1f + pulse * Mathf.Sin(age * 30f);
                spriteRoot.localScale = new Vector3(s, s, 1f);
            }
        }

        private void OnDestroy()
        {
            // Splash projectiles burst where they vanish (on impact, or when the target died mid-flight).
            if (impactRadius <= 0f || !GameplayFx.CanSpawnFrom(this)) return;
            if (kind == ProjectileArtKind.Flask) GameplayFx.Splash(transform.position, impactRadius, impactColor);
            else GameplayFx.Explosion(transform.position, impactRadius, impactColor);
        }
    }
}
