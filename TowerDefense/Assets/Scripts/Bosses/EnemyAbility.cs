using UnityEngine;

/// <summary>
/// Data half of a boss/elite ability (charge, summon, enrage, shield...).
/// Lives in EnemyData.abilities. Enemy.Init calls Attach once per spawn;
/// Attach adds an EnemyAbilityBehaviour to the enemy's GameObject.
/// </summary>
public abstract class EnemyAbilityData : ScriptableObject
{
    public string abilityName = "Ability";

    /// <summary>Add and bind this ability's runtime component on the spawned enemy.</summary>
    public abstract EnemyAbilityBehaviour Attach(Enemy enemy);
}

/// <summary>
/// Runtime half of an ability. Enemy destroys every EnemyAbilityBehaviour on
/// its GameObject when it dies, leaks, or returns to a pool, so abilities never
/// leak onto a recycled enemy.
/// </summary>
public abstract class EnemyAbilityBehaviour : MonoBehaviour
{
    protected Enemy Owner { get; private set; }

    public virtual void Bind(Enemy owner, EnemyAbilityData data)
    {
        Owner = owner;
    }
}
