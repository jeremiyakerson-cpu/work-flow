using UnityEngine;

/// <summary>
/// The single most "Kingdom Rush" system: one controllable hero that the
/// player drops on the path, repositions to plug leaks, and pops an active
/// ability on cooldown. Dies -> goes on a respawn timer instead of being gone
/// forever.
/// </summary>
public class HeroUnit : MonoBehaviour, IBlockable
{
    [Header("Stats")]
    public float maxHealth = 200f;
    public float attackDamage = 15f;
    public float attackInterval = 0.8f;
    public float attackRange = 1.5f;
    public DamageType damageType = DamageType.Physical;

    [Header("Respawn")]
    public float respawnTime = 15f;
    private Vector3 lastPlacedPosition;

    [Header("Active Ability")]
    public float abilityCooldown = 12f;
    public float abilityDamage = 40f;
    public float abilityRadius = 3f;
    private float abilityTimer = 0f;

    private float currentHealth;
    private float attackTimer;
    private Enemy engagedEnemy;
    private bool isDead = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        lastPlacedPosition = transform.position;
    }

    private void Update()
    {
        if (isDead) return;

        abilityTimer -= Time.deltaTime;

        if (engagedEnemy != null)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = attackInterval;
                engagedEnemy.TakeDamage(attackDamage, damageType);
            }
        }
        else
        {
            TryEngageNearbyEnemy();
        }
    }

    private void TryEngageNearbyEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, LayerMask.GetMask("Enemy"));
        foreach (var hit in hits)
        {
            Enemy e = hit.GetComponent<Enemy>();
            if (e == null) continue;
            if (e.TryGetBlocked(transform))
            {
                engagedEnemy = e;
                return;
            }
        }
    }

    /// <summary>
    /// Player-triggered special (wire to a UI button that checks IsAbilityReady()).
    /// Default implementation: AoE burst around the hero. Override per-hero for
    /// unique kits (heal, buff allies, dash-strike, etc).
    /// </summary>
    public void UseAbility()
    {
        if (abilityTimer > 0f) return;
        abilityTimer = abilityCooldown;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, abilityRadius, LayerMask.GetMask("Enemy"));
        foreach (var hit in hits)
        {
            Enemy e = hit.GetComponent<Enemy>();
            e?.TakeDamage(abilityDamage, damageType);
        }
    }

    public bool IsAbilityReady() => abilityTimer <= 0f && !isDead;

    // ---------------- Placement / Repositioning ----------------

    /// <summary>Call when the player drags the hero to a new spot on the path.</summary>
    public void MoveTo(Vector3 position)
    {
        if (isDead) return;
        engagedEnemy?.ReleaseFromBlock();
        engagedEnemy = null;
        lastPlacedPosition = position;
        transform.position = position;
    }

    // ---------------- Damage / Death ----------------

    public void ReceiveMeleeDamage(float amount)
    {
        if (isDead) return;
        currentHealth -= amount;
        if (currentHealth <= 0f) Die();
    }

    private void Die()
    {
        isDead = true;
        engagedEnemy?.ReleaseFromBlock();
        engagedEnemy = null;
        gameObject.SetActive(false); // hide instead of destroy - we respawn this instance
        Invoke(nameof(Respawn), respawnTime);
    }

    private void Respawn()
    {
        currentHealth = maxHealth;
        isDead = false;
        transform.position = lastPlacedPosition;
        gameObject.SetActive(true);
    }

    public float HealthPercent() => currentHealth / maxHealth;
    public bool IsDead => isDead;
}
