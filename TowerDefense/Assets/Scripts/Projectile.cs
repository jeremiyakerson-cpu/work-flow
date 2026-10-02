using TowerDefense.Pooling;
using UnityEngine;

/// <summary>
/// Tower projectile. Attach to a projectile prefab and assign it to a
/// TowerData's projectilePrefab field. Homes on its target; if the target
/// dies mid-flight it keeps flying to the last known position and still lands
/// there (splash towers still splash; single-target shots hit whoever is
/// standing on the spot). arcHeight > 0 lobs it in a parabola (artillery).
/// The hit (damage, type, splash, slow/poison) is captured at fire time, so a
/// tower sold mid-flight still lands its shot. Pooled via GameObjectPool.
/// </summary>
public class Projectile : MonoBehaviour
{
    public float speed = 15f;
    [Tooltip("Peak height of a lobbed shot in world units. 0 = straight homing shot.")]
    public float arcHeight = 0f;
    [Tooltip("Rotate the sprite along the flight direction (+X = forward).")]
    public bool rotateToVelocity = true;
    [Tooltip("Single-target shots whose target died hit an enemy within this radius of the landing spot.")]
    public float strayHitRadius = 0.4f;
    [Tooltip("Safety net: despawn after this many seconds no matter what.")]
    public float maxLifetime = 6f;

    private Enemy targetEnemy;
    private int targetSpawnId;
    private Vector3 targetPoint;
    private Vector3 startPoint;
    private float elapsed;
    private float flightTime;
    private float activeArc;
    private bool launched;
    private Tower.HitInfo hit;
    private Tower owner;

    /// <summary>Where the projectile will land (tracks the target while it lives).</summary>
    public Vector3 TargetPoint => targetPoint;
    public Tower Owner => owner;

    public void Init(Transform targetEnemy, float dmg, DamageType type, Tower sourceTower)
    {
        owner = sourceTower;
        this.targetEnemy = targetEnemy != null ? targetEnemy.GetComponent<Enemy>() : null;
        targetSpawnId = this.targetEnemy != null ? this.targetEnemy.SpawnId : 0;
        targetPoint = targetEnemy != null ? targetEnemy.position : transform.position;

        if (sourceTower != null)
        {
            hit = sourceTower.CreateHitInfo();
            hit.damage = dmg;
            hit.damageType = type;
        }
        else
        {
            hit = Tower.HitInfo.Simple(dmg, type);
        }

        // Data can turn any projectile into a lobbed one (artillery).
        activeArc = arcHeight;
        if (sourceTower != null && sourceTower.data != null && sourceTower.data.projectileArcHeight > 0f)
            activeArc = sourceTower.data.projectileArcHeight;

        startPoint = transform.position;
        elapsed = 0f;
        flightTime = Mathf.Max(0.05f, Vector3.Distance(startPoint, targetPoint) / Mathf.Max(0.01f, speed));
        launched = true;
    }

    private bool TargetAlive => targetEnemy != null && !targetEnemy.IsDead && targetEnemy.SpawnId == targetSpawnId;

    private void Update()
    {
        if (!launched)
        {
            GameObjectPool.Despawn(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        elapsed += dt;
        if (elapsed > maxLifetime)
        {
            Finish();
            return;
        }

        if (TargetAlive) targetPoint = targetEnemy.transform.position;
        else targetEnemy = null;

        Vector3 before = transform.position;
        if (activeArc > 0f) UpdateArc();
        else UpdateStraight(dt);

        if (launched && rotateToVelocity)
        {
            Vector3 v = transform.position - before;
            if (v.x * v.x + v.y * v.y > 1e-10f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
        }
    }

    private void UpdateStraight(float dt)
    {
        Vector3 dir = targetPoint - transform.position;
        float step = speed * dt;
        float dist = dir.magnitude;
        if (dist <= step)
        {
            transform.position = targetPoint;
            Land();
            return;
        }
        transform.position += dir * (step / dist);
    }

    // Parabola from the launch point to the (moving) landing point over a fixed flight time.
    private void UpdateArc()
    {
        float t = Mathf.Clamp01(elapsed / flightTime);
        Vector3 ground = Vector3.Lerp(startPoint, targetPoint, t);
        ground.y += activeArc * 4f * t * (1f - t);
        transform.position = ground;
        if (t >= 1f)
        {
            transform.position = targetPoint;
            Land();
        }
    }

    private void Land()
    {
        Enemy direct = TargetAlive ? targetEnemy : null;
        if (direct == null && hit.splashRadius <= 0f) direct = FindStrayTarget();
        Tower.ResolveHit(hit, targetPoint, direct);
        Finish();
    }

    // Single-target shot whose target vanished: hit the closest targetable enemy on the spot, if any.
    private Enemy FindStrayTarget()
    {
        if (!(strayHitRadius > 0f)) return null;
        Enemy[] buffer = EnemyQuery.RentBuffer();
        Enemy best = null;
        float bestSqr = float.PositiveInfinity;
        int n = EnemyQuery.OverlapEnemies(targetPoint, strayHitRadius, buffer);
        for (int i = 0; i < n; i++)
        {
            Enemy e = buffer[i];
            if (e.IsFlying ? !hit.canTargetFlying : !hit.canTargetGround) continue;
            Vector3 d = e.transform.position - targetPoint;
            float sqr = d.x * d.x + d.y * d.y;
            if (sqr < bestSqr) { bestSqr = sqr; best = e; }
        }
        EnemyQuery.ReturnBuffer(buffer);
        return best;
    }

    private void Finish()
    {
        launched = false;
        targetEnemy = null;
        owner = null;
        GameObjectPool.Despawn(gameObject);
    }
}
