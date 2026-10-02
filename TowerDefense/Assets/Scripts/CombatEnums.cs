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
