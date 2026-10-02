// Lives in the engine-free TowerDefense.Core assembly so the rules layer can use
// these types, but stays in the global namespace so gameplay scripts are unaffected.

/// <summary>
/// Shared enums used across Tower, Enemy, and Projectile.
/// This is what makes towers feel different from each other (KR-style):
/// physical towers shred light armor but struggle vs heavy armor,
/// magic ignores armor but often does less raw damage, etc.
/// </summary>
public enum DamageType
{
    Physical,   // archers, artillery, melee
    Magic,      // mages - ignores armor
    Poison      // damage over time - ignores armor, doesn't stack (refreshes duration)
}

public enum ArmorType
{
    None,       // normal enemies - take full physical damage
    Light,      // slight physical resistance
    Heavy       // strong physical resistance, weak to magic
}

public enum EnemyMoveType
{
    Ground,     // can be blocked by heroes/barricades
    Flying      // ignores blockers, usually immune to melee-only towers
}

/// <summary>
/// Which enemy in range a tower shoots at. "First" (furthest along its path)
/// is the KR default - it protects the exit.
/// </summary>
public enum TargetPriority
{
    First,      // furthest along the path
    Last,       // least far along the path
    Strongest,  // most current health
    Weakest,    // least current health
    Closest     // nearest to the tower
}
