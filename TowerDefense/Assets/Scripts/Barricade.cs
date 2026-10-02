using UnityEngine;

/// <summary>
/// A disposable obstacle the player drops on the path to stall ground enemies
/// for a few seconds while towers pour damage in. Classic KR "barricade" spell.
/// Unlike a Hero it doesn't fight back - it just has HP and breaks.
/// </summary>
public class Barricade : MonoBehaviour, IBlockable
{
    public float maxHealth = 60f;
    private float currentHealth;
    private Enemy engagedEnemy;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (engagedEnemy != null) return; // barricades hold one enemy at a time in this simple version

        Enemy e = other.GetComponent<Enemy>();
        if (e != null && e.TryGetBlocked(transform))
        {
            engagedEnemy = e;
        }
    }

    public void ReceiveMeleeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f) Break();
    }

    private void Break()
    {
        engagedEnemy?.ReleaseFromBlock();
        Destroy(gameObject);
    }

    public float HealthPercent() => currentHealth / maxHealth;
}
