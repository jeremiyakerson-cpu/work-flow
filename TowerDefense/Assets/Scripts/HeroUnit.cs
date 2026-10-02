using System;
using UnityEngine;

/// <summary>
/// The single most "Kingdom Rush" system: one controllable hero that the
/// player drops on the path, repositions to plug leaks, and pops an active
/// ability on cooldown. Dies -> goes on a respawn timer instead of being gone
/// forever. Blocks one ground enemy at a time; if every enemy in reach is
/// already held (by a barricade, say) it helps fight one instead of idling.
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

    private static readonly Enemy[] engageBuffer = new Enemy[EnemyQuery.BufferSize];

    private float currentHealth;
    private bool healthInitialized;
    private float attackTimer;
    private float respawnTimer;
    private Enemy engagedEnemy;
    private int engagedSpawnId;
    private bool ownsBlock;
    private bool isDead = false;
    private bool isMoving;
    private Vector3 moveTarget;

    /// <summary>The enemy the hero is fighting, or null.</summary>
    public Enemy EngagedEnemy => EngagedValid() ? engagedEnemy : null;
    public float CurrentHealth { get { EnsureHealth(); return currentHealth; } }
    /// <summary>Unit direction the hero walks (or faces its opponent). For sprite flipping.</summary>
    public Vector2 FacingDirection { get; private set; } = Vector2.right;

    private void Awake()
    {
        lastPlacedPosition = transform.position;
        moveTarget = transform.position;
    }

    // Health is initialised lazily (not in Awake) so code that does AddComponent
    // and then configures maxHealth gets the configured value.
    private void EnsureHealth()
    {
        if (healthInitialized) return;
        healthInitialized = true;
        currentHealth = maxHealth;
    }

    private void Start()
    {
        EnsureHealth();
    }

    private void Update()
    {
        EnsureHealth();
        if (isDead)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f) Respawn();
            return;
        }

        abilityTimer -= Time.deltaTime;

        if (isMoving)
        {
            Vector3 before = transform.position;
            transform.position = Vector3.MoveTowards(before, moveTarget, moveSpeed * Time.deltaTime);
            Face(transform.position - before);
            if (Vector3.Distance(transform.position, moveTarget) < 0.02f) isMoving = false;
            return; // walking heroes don't engage - lets the player pull them out of a fight
        }

        if (engagedEnemy != null && !EngagedValid())
            ClearEngaged();

        if (engagedEnemy != null)
        {
            Face(engagedEnemy.transform.position - transform.position);
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = Mathf.Max(0.05f, attackInterval);
                engagedEnemy.TakeDamage(attackDamage, damageType);
            }
        }
        else
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + regenPerSecond * Time.deltaTime);
            TryEngageNearbyEnemy();
        }
    }

    // Still the same living enemy, still in reach, and (if we were blocking it) still held by us.
    private bool EngagedValid()
    {
        Enemy e = engagedEnemy;
        if (e == null || e.IsDead || e.SpawnId != engagedSpawnId || !e.gameObject.activeInHierarchy) return false;
        if (ownsBlock) return e.Blocker == transform;
        // Assisting: drop it once it walks away (whoever held it let go).
        Vector3 d = e.transform.position - transform.position;
        float reach = attackRange * 1.25f;
        return d.x * d.x + d.y * d.y <= reach * reach;
    }

    private void TryEngageNearbyEnemy()
    {
        Vector3 here = transform.position;
        int n = EnemyQuery.OverlapEnemies(here, attackRange, engageBuffer);
        Enemy free = null, held = null;
        float freeSqr = float.PositiveInfinity, heldSqr = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            Enemy e = engageBuffer[i];
            if (e.IsFlying) continue;
            Vector3 d = e.transform.position - here;
            float sqr = d.x * d.x + d.y * d.y;
            if (!e.IsBlocked) { if (sqr < freeSqr) { freeSqr = sqr; free = e; } }
            else if (sqr < heldSqr) { heldSqr = sqr; held = e; }
        }
        Array.Clear(engageBuffer, 0, n);

        if (free != null && free.TryGetBlocked(transform)) Engage(free, true);
        else if (held != null) Engage(held, false);
    }

    private void Engage(Enemy e, bool blocking)
    {
        engagedEnemy = e;
        engagedSpawnId = e.SpawnId;
        ownsBlock = blocking;
        attackTimer = 0f;
    }

    private void ClearEngaged()
    {
        engagedEnemy = null;
        engagedSpawnId = 0;
        ownsBlock = false;
    }

    private void Face(Vector3 delta)
    {
        if (delta.x * delta.x + delta.y * delta.y > 1e-8f) FacingDirection = new Vector2(delta.x, delta.y).normalized;
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
        // Rented buffer: kills during the loop can trigger callbacks that query again.
        Enemy[] buffer = EnemyQuery.RentBuffer();
        try
        {
            int n = EnemyQuery.OverlapEnemies(transform.position, abilityRadius, buffer);
            for (int i = 0; i < n; i++) buffer[i].TakeDamage(abilityDamage, damageType);
        }
        finally
        {
            EnemyQuery.ReturnBuffer(buffer);
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
        // Only let go of an enemy we're actually holding (not one we were assisting on,
        // and not a recycled object that is now a different enemy).
        if (ownsBlock && engagedEnemy != null && engagedEnemy.SpawnId == engagedSpawnId && engagedEnemy.Blocker == transform)
            engagedEnemy.ReleaseFromBlock();
        ClearEngaged();
    }

    // ---------------- Damage / Death ----------------

    public void ReceiveMeleeDamage(float amount)
    {
        EnsureHealth();
        if (isDead || !(amount > 0f)) return;
        currentHealth -= amount;
        if (currentHealth <= 0f) Die();
    }

    private void Die()
    {
        isDead = true;
        isMoving = false;
        currentHealth = 0f;
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

    private void OnDisable()
    {
        ReleaseEngaged();
    }

    public float HealthPercent()
    {
        EnsureHealth();
        return maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    }
    public bool IsDead => isDead;
    public bool IsMoving => isMoving;
}
