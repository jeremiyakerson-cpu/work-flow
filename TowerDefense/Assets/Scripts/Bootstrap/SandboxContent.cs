using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Bootstrap
{
    /// <summary>
    /// SANDBOX DATA ONLY. A tiny self-contained catalog (1 level, 3 towers,
    /// 4 enemies + 1 boss) so the visuals can be exercised without the real
    /// content. The shipping catalog is built by the Levels/Content workstream;
    /// nothing here should be referenced by game code.
    /// </summary>
    public sealed class SandboxContent
    {
        public LevelData Level { get; private set; }
        public List<TowerData> Towers { get; } = new List<TowerData>();
        public List<EnemyData> Enemies { get; } = new List<EnemyData>();
        public List<EnemyData> Bosses { get; } = new List<EnemyData>();

        /// <summary>Slot indices that get a tower at start: (slot index, tower index).</summary>
        public static readonly (int slot, int tower)[] PrebuiltTowers = { (1, 0), (2, 1), (6, 2) };

        private readonly List<ScriptableObject> created = new List<ScriptableObject>();

        public IEnumerable<EnemyData> AllEnemies
        {
            get
            {
                foreach (var e in Enemies) yield return e;
                foreach (var b in Bosses) yield return b;
            }
        }

        public static SandboxContent Create()
        {
            var c = new SandboxContent();

            // --- Towers ------------------------------------------------------------
            c.Towers.Add(c.MakeTower("sandbox_archer", "Archer Post (sandbox)", DamageType.Physical, range: 4.5f, rate: 1.3f, dmg: 6f, cost: 70));
            c.Towers.Add(c.MakeTower("sandbox_mage", "Mage Spire (sandbox)", DamageType.Magic, range: 4f, rate: 0.7f, dmg: 15f, cost: 100));
            TowerData artillery = c.MakeTower("sandbox_artillery", "Bombard (sandbox)", DamageType.Physical, range: 4.6f, rate: 0.45f, dmg: 14f, cost: 120);
            artillery.splashRadius = 1.2f;
            artillery.canTargetFlying = false;
            artillery.pathAName = "Big Bertha";
            artillery.pathBName = "Rapid Mortar";
            c.Towers.Add(artillery);

            // --- Enemies -----------------------------------------------------------
            c.Enemies.Add(c.MakeEnemy("sandbox_grunt", "Grunt", ArmorType.None, EnemyMoveType.Ground, hp: 10f, speed: 2f, gold: 5,
                                  tint: new Color(0.9f, 0.5f, 0.25f), scale: 1f, unlock: 1));
            c.Enemies.Add(c.MakeEnemy("sandbox_runner", "Runner", ArmorType.Light, EnemyMoveType.Ground, hp: 7f, speed: 3f, gold: 4,
                                  tint: new Color(0.42f, 0.74f, 0.32f), scale: 0.9f, unlock: 2));
            c.Enemies.Add(c.MakeEnemy("sandbox_bat", "Bat", ArmorType.None, EnemyMoveType.Flying, hp: 8f, speed: 2.6f, gold: 6,
                                  tint: new Color(0.6f, 0.38f, 0.85f), scale: 0.9f, unlock: 2));
            c.Enemies.Add(c.MakeEnemy("sandbox_brute", "Brute", ArmorType.Heavy, EnemyMoveType.Ground, hp: 28f, speed: 1.4f, gold: 10,
                                  tint: new Color(0.45f, 0.53f, 0.74f), scale: 1.2f, unlock: 3));
            EnemyData ogre = c.MakeEnemy("sandbox_ogre", "Ogre Warlord", ArmorType.Heavy, EnemyMoveType.Ground, hp: 20f, speed: 1.6f, gold: 5,
                                     tint: new Color(0.8f, 0.24f, 0.22f), scale: 1.9f, unlock: 1);
            ogre.isBoss = true;
            ogre.bossHealthMultiplier = 8f;
            ogre.bossGoldMultiplier = 15;
            ogre.damageToBase = 5;
            ogre.meleeDamage = 12f;
            c.Bosses.Add(ogre);

            // --- Level -------------------------------------------------------------
            LevelData level = c.Make<LevelData>("Sandbox Meadow");
            level.id = "sandbox_meadow";
            level.displayName = "Sandbox Meadow";
            level.description = "Visuals sandbox level (not part of the campaign).";
            level.worldSize = new Vector2(32f, 18f);
            level.paths = new List<PathDefinition>
            {
                Path(new Vector2(-1, 14), new Vector2(7, 14), new Vector2(7, 8), new Vector2(15, 8), new Vector2(15, 13),
                     new Vector2(24, 13), new Vector2(24, 6), new Vector2(33, 6)),
                Path(new Vector2(12, -1), new Vector2(12, 3), new Vector2(24, 3), new Vector2(24, 6), new Vector2(33, 6)),
            };
            level.buildSlots = new List<Vector2>
            {
                new Vector2(4f, 11.2f), new Vector2(9.8f, 11f), new Vector2(11f, 5.4f), new Vector2(18f, 10.4f), new Vector2(18.6f, 5.6f),
                new Vector2(21f, 10.2f), new Vector2(27f, 9.2f), new Vector2(28f, 3.2f), new Vector2(4f, 16.6f), new Vector2(15.6f, 0.8f),
                new Vector2(20.5f, 15.8f),
            };
            level.heroStart = new Vector2(11f, 8f);
            level.startingGold = 260;
            level.startingLives = 20;
            level.wavesToWin = 0; // endless
            level.difficultyMultiplier = 1f;
            level.enemyPool = new List<EnemyData>(c.Enemies);
            level.bossPool = new List<EnemyData>(c.Bosses);
            level.bossEveryNWaves = 4;
            level.groundColor = new Color(0.45f, 0.64f, 0.34f);
            level.pathColor = new Color(0.84f, 0.71f, 0.49f);
            level.accentColor = new Color(0.27f, 0.5f, 0.24f);
            c.Level = level;
            return c;
        }

        /// <summary>Destroy every ScriptableObject this catalog created.</summary>
        public void Destroy()
        {
            foreach (var so in created)
                if (so != null) Object.Destroy(so);
            created.Clear();
        }

        private T Make<T>(string name) where T : ScriptableObject
        {
            T so = ScriptableObject.CreateInstance<T>();
            so.name = name;
            created.Add(so);
            return so;
        }

        private TowerData MakeTower(string id, string name, DamageType type, float range, float rate, float dmg, int cost)
        {
            TowerData t = Make<TowerData>(name);
            t.id = id;
            t.towerName = name;
            t.description = "Sandbox tower.";
            t.damageType = type;
            t.range = range;
            t.fireRate = rate;
            t.damage = dmg;
            t.baseCost = cost;
            t.level2Cost = Mathf.RoundToInt(cost * 0.6f);
            t.pathACost = Mathf.RoundToInt(cost * 1.1f);
            t.pathBCost = Mathf.RoundToInt(cost * 1.1f);
            t.level4Cost = Mathf.RoundToInt(cost * 1.8f);
            return t;
        }

        private EnemyData MakeEnemy(string id, string name, ArmorType armor, EnemyMoveType move, float hp, float speed, int gold,
                                Color tint, float scale, int unlock)
        {
            EnemyData e = Make<EnemyData>(name);
            e.id = id;
            e.enemyName = name;
            e.description = "Sandbox enemy.";
            e.armor = armor;
            e.moveType = move;
            e.baseHealth = hp;
            e.baseSpeed = speed;
            e.baseGoldReward = gold;
            e.tint = tint;
            e.visualScale = scale;
            e.unlockWave = unlock;
            return e;
        }

        private static PathDefinition Path(params Vector2[] points) => new PathDefinition { points = new List<Vector2>(points) };
    }
}
