using System;
using TowerDefense.Pooling;
using UnityEngine;

/// <summary>
/// A disposable obstacle the player drops on the path to stall ground enemies
/// for a few seconds while towers pour damage in. Classic KR "barricade" spell.
/// Unlike a Hero it doesn't fight back - it just has HP and breaks.
/// Polls for nearby ground enemies every frame (no trigger callbacks, so it
/// works with any collider/rigidbody setup) and holds up to maxBlocked at once.
/// </summary>
public class Barricade : MonoBehaviour, IBlockable
{
    public float maxHealth = 60f;
    [Tooltip("How many enemies it can hold at once.")]
    [Min(1)] public int maxBlocked = 3;
    [Tooltip("Ground enemies within this radius (and not already past it) get stopped.")]
    public float blockRadius = 0.75f;
    [Tooltip("Seconds before it crumbles on its own. 0 = lasts until broken.")]
    public float lifetime = 0f;

    /// <summary>Raised once when it breaks or expires, just before it's removed.</summary>
    public event Action<Barricade> Broken;

    private struct Hold
    {
        public Enemy enemy;
        public int spawnId;
    }

    private static readonly Enemy[] queryBuffer = new Enemy[EnemyQuery.BufferSize];

    private Hold[] held = new Hold[3];
    private int heldCount;
    private float currentHealth;
    private float age;
    private bool initialized;
    private bool broken;

    public int BlockedCount => heldCount;
    public float CurrentHealth { get { EnsureInit(); return currentHealth; } }
    public bool IsBroken => broken;

    // Health is set lazily (not in Awake) so code that does AddComponent and then
    // sets maxHealth gets the configured value.
    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;
        broken = false;
        age = 0f;
        currentHealth = Mathf.Max(0.01f, maxHealth);
        if (held.Length < maxBlocked) held = new Hold[Mathf.Max(1, maxBlocked)];
    }

    private void Start()
    {
        EnsureInit();
    }

    private void Update()
    {
        EnsureInit();
        if (broken) return;

        if (lifetime > 0f)
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Break();
                return;
            }
        }

        PruneHeld();
        if (heldCount < maxBlocked) GrabEnemies();
    }

    // Drop enemies that died, were recycled, or got released by someone else.
    private void PruneHeld()
    {
        for (int i = heldCount - 1; i >= 0; i--)
        {
            Enemy e = held[i].enemy;
            bool valid = e != null && !e.IsDead && e.SpawnId == held[i].spawnId && e.Blocker == transform;
            if (valid) continue;
            held[i] = held[heldCount - 1];
            held[heldCount - 1] = default;
            heldCount--;
        }
    }

    private void GrabEnemies()
    {
        if (held.Length < maxBlocked) Array.Resize(ref held, maxBlocked);
        Vector3 here = transform.position;
        int n = EnemyQuery.OverlapEnemies(here, blockRadius, queryBuffer);
        for (int i = 0; i < n && heldCount < maxBlocked; i++)
        {
            Enemy e = queryBuffer[i];
            if (e.IsFlying || e.IsBlocked) continue;
            // Only stop enemies still walking towards us, not ones already past.
            Vector2 toUs = new Vector2(here.x - e.transform.position.x, here.y - e.transform.position.y);
            if (Vector2.Dot(e.MoveDirection, toUs) < -0.05f) continue;
            if (e.TryGetBlocked(transform))
                held[heldCount++] = new Hold { enemy = e, spawnId = e.SpawnId };
        }
        Array.Clear(queryBuffer, 0, n);
    }

    public void ReceiveMeleeDamage(float amount)
    {
        EnsureInit();
        if (broken || !(amount > 0f)) return;
        currentHealth -= amount;
        if (currentHealth <= 0f) Break();
    }

    private void Break()
    {
        if (broken) return;
        broken = true;
        ReleaseAll();
        Broken?.Invoke(this);
        Broken = null;
        GameObjectPool.Despawn(gameObject);
    }

    private void ReleaseAll()
    {
        for (int i = 0; i < heldCount; i++)
        {
            Enemy e = held[i].enemy;
            if (e != null && e.SpawnId == held[i].spawnId && e.Blocker == transform) e.ReleaseFromBlock();
            held[i] = default;
        }
        heldCount = 0;
    }

    // Also covers being destroyed or despawned by something else. A pooled
    // barricade re-initialises (full health) on its next spawn.
    private void OnDisable()
    {
        ReleaseAll();
        initialized = false;
    }

    public float HealthPercent()
    {
        EnsureInit();
        return maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, blockRadius);
    }
}
