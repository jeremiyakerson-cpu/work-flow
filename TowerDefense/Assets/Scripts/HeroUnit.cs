using System;
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
    public string heroName = "Hero";
    public float maxHealth = 200f;
    public float attackDamage = 15f;
    public float attackInterval = 0.8f;
    public float attackRange = 1.5f;
    public DamageType damageType = DamageType.Physical;
    [Tooltip("World units per second when walking to a new spot.")]
    public float moveSpeed = 4f;
    [Tooltip("Health regenerated per second while not fighting.")]
    public float regenPerSecond = 5f;

    [Header("Respawn")]
    public float respawnTime = 15f;
    private Vector3 lastPlacedPosition;

    [Header("Active Ability")]
    public string abilityName = "Shockwave";
    public float abilityCooldown = 12f;
    public float abilityDamage = 40f;
    public float abilityRadius = 3f;
    private float abilityTimer = 0f;

    public event Action<HeroUnit> Died;
    public event Action<HeroUnit> Respawned;
    public event Action<HeroUnit> AbilityUsed;

    private float currentHealth;
    private float attackTimer;
    private float respawnTimer;
    private Enemy engagedEnemy;
    private bool isDead = false;
    private bool isMoving;
    private Vector3 moveTarget;

    private void Awake()
    {
        currentHealth = maxHealth;
        lastPlacedPosition = transform.position;
        moveTarget = transform.position;
    }

    private void Update()
    {
        if (isDead)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f) Respawn();
            return;
        }

        abilityTimer -= Time.deltaTime;

        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, moveTarget, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, moveTarget) < 0.02f) isMoving = false;
            return; // walking heroes don't engage - lets the player pull them out of a fight
        }

        if (engagedEnemy != null && (engagedEnemy.IsDead || !engagedEnemy.gameObject.activeInHierarchy))
            engagedEnemy = null;

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
            currentHealth = Mathf.Min(maxHealth, currentHealth + regenPerSecond * Time.deltaTime);
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
                attackTimer = 0f;
                return;
            }
        }
    }

    /// <summary>
    /// Player-triggered special (wire to a UI button that checks IsAbilityReady()).
    /// Default implementation: AoE burst around the hero. Override PerformAbility
    /// per-hero for unique kits (heal, buff allies, dash-strike, etc).
    /// </summary>
    public void UseAbility()
    {
        if (!IsAbilityReady()) return;
        abilityTimer = abilityCooldown;
        PerformAbility();
        AbilityUsed?.Invoke(this);
    }

    protected virtual void PerformAbility()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, abilityRadius, LayerMask.GetMask("Enemy"));
        foreach (var hit in hits)
        {
            Enemy e = hit.GetComponent<Enemy>();
            e?.TakeDamage(abilityDamage, damageType);
        }
    }

    public bool IsAbilityReady() => abilityTimer <= 0f && !isDead;
    /// <summary>0 = just used, 1 = ready. For cooldown fill bars.</summary>
    public float AbilityReadyPercent => abilityCooldown > 0f ? Mathf.Clamp01(1f - abilityTimer / abilityCooldown) : 1f;
    /// <summary>0 = just died, 1 = back. Only meaningful while IsDead.</summary>
    public float RespawnPercent => isDead && respawnTime > 0f ? Mathf.Clamp01(1f - respawnTimer / respawnTime) : 1f;

    // ---------------- Placement / Repositioning ----------------

    /// <summary>Call when the player sends the hero to a new spot on the path. The hero walks there.</summary>
    public void MoveTo(Vector3 position)
    {
        if (isDead) return;
        ReleaseEngaged();
        lastPlacedPosition = position;
        moveTarget = position;
        isMoving = true;
    }

    /// <summary>Instantly place the hero (level start).</summary>
    public void PlaceAt(Vector3 position)
    {
        ReleaseEngaged();
        lastPlacedPosition = position;
        moveTarget = position;
        isMoving = false;
        transform.position = position;
    }

    private void ReleaseEngaged()
    {
        if (engagedEnemy != null) engagedEnemy.ReleaseFromBlock();
        engagedEnemy = null;
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
        isMoving = false;
        ReleaseEngaged();
        respawnTimer = respawnTime;
        // Hide instead of SetActive(false): this component keeps ticking the respawn timer.
        SetPresence(false);
        Died?.Invoke(this);
    }

    private void Respawn()
    {
        currentHealth = maxHealth;
        isDead = false;
        transform.position = lastPlacedPosition;
        moveTarget = lastPlacedPosition;
        SetPresence(true);
        Respawned?.Invoke(this);
    }

    private void SetPresence(bool present)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = present;
        foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = present;
    }

    public float HealthPercent() => currentHealth / maxHealth;
    public bool IsDead => isDead;
    public bool IsMoving => isMoving;
}
