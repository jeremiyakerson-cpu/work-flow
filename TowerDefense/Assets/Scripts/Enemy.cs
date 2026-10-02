using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to every enemy prefab. Handles path-following movement, health/armor,
/// being "blocked" by heroes/barricades (the KR chokepoint mechanic), status
/// effects (slow/poison), and notifying the GameManager on death or leak.
/// Base stats normally come from an EnemyData asset via Init(EnemyData, ...).
/// </summary>
public class Enemy : MonoBehaviour
{
    [Header("Runtime Stats (set by WaveManager on spawn)")]
    public float maxHealth = 10f;
    public float moveSpeed = 2f;
    public int goldReward = 5;
    public int damageToBase = 1;
    public ArmorType armor = ArmorType.None;
    public EnemyMoveType moveType = EnemyMoveType.Ground;
    public float meleeDamage = 2f;
    public float attackInterval = 1f;

    private float currentHealth;
    private List<Transform> path;
    private int waypointIndex = 0;

    // --- Blocking (chokepoint) state ---
    private Transform blocker;
    private float attackTimer;

    // --- Status effects ---
    private float slowMultiplier = 1f;
    private float slowTimeRemaining;
    private float poisonDps;
    private float poisonTimeRemaining;

    /// <summary>
    /// Preferred init path: pull identity stats (armor/moveType/meleeDamage)
    /// from the EnemyData asset, apply the WaveManager's scaled health/speed/
    /// reward on top.
    /// </summary>
    public void Init(EnemyData enemyData, List<Transform> waypointPath, float scaledHealth, float scaledSpeed, int scaledReward)
    {
        path = waypointPath;
        maxHealth = scaledHealth;
        currentHealth = scaledHealth;
        moveSpeed = scaledSpeed;
        goldReward = scaledReward;
        damageToBase = enemyData.damageToBase;
        armor = enemyData.armor;
        moveType = enemyData.moveType;
        meleeDamage = enemyData.meleeDamage;
        attackInterval = enemyData.attackInterval;

        if (path != null && path.Count > 0)
            transform.position = path[0].position;
    }

    /// <summary>Legacy/manual init if you're not using EnemyData for a given prefab.</summary>
    public void Init(List<Transform> waypointPath, float health, float speed, int reward, int leakDamage,
                      ArmorType armorType = ArmorType.None, EnemyMoveType move = EnemyMoveType.Ground)
    {
        path = waypointPath;
        maxHealth = health;
        currentHealth = health;
        moveSpeed = speed;
        goldReward = reward;
        damageToBase = leakDamage;
        armor = armorType;
        moveType = move;

        if (path != null && path.Count > 0)
            transform.position = path[0].position;
    }

    private void Update()
    {
        TickStatusEffects();

        if (blocker != null)
        {
            AttackBlocker();
            return;
        }

        if (path == null || waypointIndex >= path.Count) return;

        Transform target = path[waypointIndex];
        float speed = moveSpeed * slowMultiplier;
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) < 0.05f)
        {
            waypointIndex++;
            if (waypointIndex >= path.Count)
                ReachEnd();
        }
    }

    // ---------------- Damage ----------------

    public void TakeDamage(float amount, DamageType type = DamageType.Physical)
    {
        float finalDamage = amount * ResistanceMultiplier(type);
        currentHealth -= finalDamage;
        if (currentHealth <= 0f) Die();
    }

    private float ResistanceMultiplier(DamageType type)
    {
        if (type == DamageType.Magic || type == DamageType.Poison)
            return armor == ArmorType.Heavy ? 1.25f : 1f;

        switch (armor)
        {
            case ArmorType.Light: return 0.85f;
            case ArmorType.Heavy: return 0.55f;
            default: return 1f;
        }
    }

    public void ApplySlow(float multiplier, float duration)
    {
        if (multiplier < slowMultiplier)
        {
            slowMultiplier = multiplier;
            slowTimeRemaining = duration;
        }
        else
        {
            slowTimeRemaining = Mathf.Max(slowTimeRemaining, duration);
        }
    }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        poisonDps = Mathf.Max(poisonDps, damagePerSecond);
        poisonTimeRemaining = duration;
    }

    private void TickStatusEffects()
    {
        if (slowTimeRemaining > 0f)
        {
            slowTimeRemaining -= Time.deltaTime;
            if (slowTimeRemaining <= 0f) slowMultiplier = 1f;
        }

        if (poisonTimeRemaining > 0f)
        {
            poisonTimeRemaining -= Time.deltaTime;
            TakeDamage(poisonDps * Time.deltaTime, DamageType.Poison);
        }
    }

    // ---------------- Blocking (chokepoints) ----------------

    public bool TryGetBlocked(Transform blockerTransform)
    {
        if (moveType == EnemyMoveType.Flying) return false;
        if (blocker != null) return false;

        blocker = blockerTransform;
        attackTimer = 0f;
        return true;
    }

    public void ReleaseFromBlock()
    {
        blocker = null;
    }

    private void AttackBlocker()
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = attackInterval;
            IBlockable target = blocker.GetComponent<IBlockable>();
            target?.ReceiveMeleeDamage(meleeDamage);
        }
    }

    private void Die()
    {
        GameManager.Instance.AddGold(goldReward);
        GameManager.Instance.OnEnemyKilled(this);
        Destroy(gameObject);
    }

    private void ReachEnd()
    {
        GameManager.Instance.DamageBase(damageToBase);
        GameManager.Instance.OnEnemyLeaked(this);
        Destroy(gameObject);
    }

    public float HealthPercent() => currentHealth / maxHealth;
}

/// <summary>
/// Anything an enemy can be blocked by and fight against (heroes, barricades).
/// </summary>
public interface IBlockable
{
    void ReceiveMeleeDamage(float amount);
}
