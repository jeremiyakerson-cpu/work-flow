using System;
using UnityEngine;

/// <summary>
/// Data half of a boss/elite ability (charge, summon, enrage, shield...).
/// Lives in EnemyData.abilities. Enemy.Init calls Attach once per spawn;
/// Attach adds an EnemyAbilityBehaviour to the enemy's GameObject.
/// Concrete abilities live in Bosses/Abilities (namespace TowerDefense.Bosses).
/// </summary>
public abstract class EnemyAbilityData : ScriptableObject
{
    public string abilityName = "Ability";
    [TextArea] public string description;

    /// <summary>Add and bind this ability's runtime component on the spawned enemy.</summary>
    public abstract EnemyAbilityBehaviour Attach(Enemy enemy);

    /// <summary>Shared Attach implementation: AddComponent, then Bind with this data.</summary>
    protected T AttachBehaviour<T>(Enemy enemy) where T : EnemyAbilityBehaviour
    {
        if (enemy == null) return null;
        T behaviour = enemy.gameObject.AddComponent<T>();
        behaviour.Bind(enemy, this);
        return behaviour;
    }
}

/// <summary>
/// Runtime half of an ability. Enemy destroys every EnemyAbilityBehaviour on
/// its GameObject when it dies, leaks, or returns to a pool, so abilities never
/// leak onto a recycled enemy. Behaviours therefore never "undo" their stat
/// changes in OnDestroy: Enemy.Init resets SpeedMultiplier/Invulnerable and
/// the melee stats on every spawn.
/// </summary>
public abstract class EnemyAbilityBehaviour : MonoBehaviour
{
    /// <summary>Any ability fired (charge started, minions summoned, shield raised...). For VFX/audio/UI.</summary>
    public static event Action<EnemyAbilityBehaviour> AnyTriggered;
    public event Action<EnemyAbilityBehaviour> Triggered;

    protected Enemy Owner { get; private set; }
    /// <summary>The enemy this ability runs on.</summary>
    public Enemy OwnerEnemy => Owner;
    public EnemyAbilityData Data { get; private set; }

    /// <summary>True while a timed effect is running (shield up, charging, enraged, regenerating). For visuals.</summary>
    public virtual bool IsActive => false;

    public virtual void Bind(Enemy owner, EnemyAbilityData data)
    {
        Owner = owner;
        Data = data;
    }

    /// <summary>Owner exists and is still alive (abilities stop the moment it dies).</summary>
    protected bool OwnerAlive => Owner != null && !Owner.IsDead;

    protected void RaiseTriggered()
    {
        Triggered?.Invoke(this);
        AnyTriggered?.Invoke(this);
    }
}
