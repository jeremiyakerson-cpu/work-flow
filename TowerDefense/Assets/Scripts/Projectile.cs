using UnityEngine;

/// <summary>
/// Basic homing projectile. Attach to a projectile prefab and assign it
/// to a Tower's projectilePrefab field. Delivers damage type + on-hit
/// effects (slow/poison) back through the owning Tower on impact.
/// </summary>
public class Projectile : MonoBehaviour
{
    public float speed = 15f;
    private Transform target;
    private float damage;
    private DamageType damageType;
    private Tower owner;

    public void Init(Transform targetEnemy, float dmg, DamageType type, Tower sourceTower)
    {
        target = targetEnemy;
        damage = dmg;
        damageType = type;
        owner = sourceTower;
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 dir = target.position - transform.position;
        float step = speed * Time.deltaTime;

        if (dir.magnitude <= step)
        {
            HitTarget();
            return;
        }

        transform.position += dir.normalized * step;
    }

    private void HitTarget()
    {
        Enemy e = target.GetComponent<Enemy>();
        if (owner != null)
            owner.ApplyHit(e);       // routes through Tower so slow/poison effects apply
        else if (e != null)
            e.TakeDamage(damage, damageType); // fallback if no owner assigned

        Destroy(gameObject);
    }
}
