using System.Collections.Generic;
using TowerDefense.Bosses;
using TowerDefense.Levels;
using UnityEngine;

namespace TowerDefense.Content
{
    /// <summary>
    /// The shipped content, built in code with ScriptableObject.CreateInstance
    /// so the game runs with zero .asset files. Balance notes: Docs/CONTENT.md.
    /// Wave scaling (WaveManager): an enemy's health is the wave curve times
    /// baseHealth / 10, speed is the curve times baseSpeed / 2, gold is the
    /// curve times baseGoldReward / 5. Bosses get curve health times
    /// bossHealthMultiplier and 0.6x curve speed.
    /// </summary>
    public static class DefaultContent
    {
        /// <summary>
        /// Build a fresh catalog. Every call creates new instances.
        /// runtime = true marks them DontUnloadUnusedAsset so scene loads and
        /// Resources.UnloadUnusedAssets never collect them; the Editor baker
        /// passes false so the objects can be saved as plain assets.
        /// </summary>
        public static ContentCatalog Build(bool runtime = true)
        {
            var created = new List<Object>();

            var towers = BuildTowers(created);
            var minions = BuildMinions(created);
            var enemies = BuildEnemies(created, minions);
            var bosses = BuildBosses(created, minions, enemies);

            var byId = new Dictionary<string, EnemyData>();
            foreach (var e in enemies) byId[e.id] = e;
            foreach (var e in minions) byId[e.id] = e;
            foreach (var e in bosses) byId[e.id] = e;
            EnemyData Resolve(string id) => id != null && byId.TryGetValue(id, out var d) ? d : null;

            var campaign = new List<LevelData>();
            foreach (var layout in CampaignLayouts.CreateCampaign())
                campaign.Add(Track(created, LevelFactory.Create(layout, Resolve)));
            var endless = new List<LevelData>();
            foreach (var layout in CampaignLayouts.CreateEndless())
                endless.Add(Track(created, LevelFactory.Create(layout, Resolve)));

            if (runtime)
                foreach (var o in created) o.hideFlags = HideFlags.DontUnloadUnusedAsset;

            return new ContentCatalog(towers, enemies, minions, bosses, campaign, endless);
        }

        // ================================================================ towers

        // Numbers and names live in the engine-free TowerBalance table so the
        // balance rules (cost curve, monotonic DPS) are unit-tested on plain .NET.
        private static List<TowerData> BuildTowers(List<Object> created)
        {
            var list = new List<TowerData>();
            foreach (TowerBalanceEntry entry in TowerBalance.Create())
                list.Add(Tower(created, entry));
            return list;
        }

        private static TowerData Tower(List<Object> created, TowerBalanceEntry e)
        {
            var t = Track(created, ScriptableObject.CreateInstance<TowerData>());
            t.name = e.Id;
            t.id = e.Id;
            t.towerName = e.Name;
            t.description = e.Description;
            t.damageType = e.DamageType;
            t.canTargetGround = e.CanTargetGround;
            t.canTargetFlying = e.CanTargetFlying;
            t.slowMultiplier = e.SlowMultiplier;
            t.slowDuration = e.SlowDuration;
            t.poisonDuration = e.PoisonDuration;
            t.pathAName = e.PathAName;
            t.pathADescription = e.PathADescription;
            t.pathBName = e.PathBName;
            t.pathBDescription = e.PathBDescription;
            t.ApplyUpgradeSpec(e.Spec);
            return t;
        }

        // ================================================================ enemies

        private static List<EnemyData> BuildMinions(List<Object> created)
        {
            return new List<EnemyData>
            {
                Enemy(created, ContentIds.Slimeling, "Slimeling", "A wobbling blob shed by a dying ooze.",
                      hp: 5f, speed: 2.4f, gold: 2, leak: 1, melee: 1f, interval: 1f, ArmorType.None, EnemyMoveType.Ground,
                      unlock: 1, tint: 0xA6E05A, scale: 0.5f),
                Enemy(created, ContentIds.Skeleton, "Skeleton", "Raised by the Necromancer. Brittle but relentless.",
                      hp: 9f, speed: 1.9f, gold: 2, leak: 1, melee: 2f, interval: 1f, ArmorType.Light, EnemyMoveType.Ground,
                      unlock: 1, tint: 0xE0DCCB, scale: 0.8f),
                Enemy(created, ContentIds.Spiderling, "Spiderling", "Skittering brood of the Broodmother.",
                      hp: 4f, speed: 3.0f, gold: 1, leak: 1, melee: 1f, interval: 0.8f, ArmorType.None, EnemyMoveType.Ground,
                      unlock: 1, tint: 0x3A2F2F, scale: 0.5f),
            };
        }

        private static List<EnemyData> BuildEnemies(List<Object> created, List<EnemyData> minions)
        {
            EnemyData slimeling = Find(minions, ContentIds.Slimeling);

            var grunt = Enemy(created, ContentIds.Grunt, "Goblin", "Weak and numerous. Arrows make short work of them.",
                hp: 10f, speed: 2.0f, gold: 5, leak: 1, melee: 2f, interval: 1f, ArmorType.None, EnemyMoveType.Ground,
                unlock: 1, tint: 0x6BA34A, scale: 0.8f);

            var runner = Enemy(created, ContentIds.Runner, "Worg Rider", "Fast and fragile. Slow it down or it slips past.",
                hp: 7f, speed: 3.4f, gold: 5, leak: 1, melee: 2f, interval: 0.8f, ArmorType.None, EnemyMoveType.Ground,
                unlock: 2, tint: 0x8C6F55, scale: 0.85f);

            var bat = Enemy(created, ContentIds.Bat, "Gloom Bat", "Flies over blockers. Artillery can't hit it.",
                hp: 8f, speed: 2.8f, gold: 6, leak: 1, melee: 0f, interval: 1f, ArmorType.None, EnemyMoveType.Flying,
                unlock: 3, tint: 0x5A4870, scale: 0.7f);

            var brute = Enemy(created, ContentIds.Brute, "Orc Brute", "Lightly armored bruiser. Goes berserk when wounded.",
                hp: 22f, speed: 1.7f, gold: 9, leak: 2, melee: 5f, interval: 1.2f, ArmorType.Light, EnemyMoveType.Ground,
                unlock: 4, tint: 0x4E7A3A, scale: 1.1f);
            brute.abilities.Add(Enrage(created, "Berserk", threshold: 0.3f, speed: 1.3f, melee: 1.5f, rate: 1.2f));

            var shaman = Enemy(created, ContentIds.Shaman, "Goblin Shaman", "Heals nearby allies. Kill it first.",
                hp: 12f, speed: 1.9f, gold: 10, leak: 1, melee: 2f, interval: 1.2f, ArmorType.None, EnemyMoveType.Ground,
                unlock: 5, tint: 0x3FA6A0, scale: 0.85f);
            shaman.abilities.Add(HealAura(created, "Healing Totem", radius: 2.5f, interval: 2.5f, percent: 0.08f));

            var knight = Enemy(created, ContentIds.Knight, "Dark Knight", "Heavy plate shrugs off arrows and shells. Use magic.",
                hp: 30f, speed: 1.5f, gold: 12, leak: 2, melee: 6f, interval: 1.1f, ArmorType.Heavy, EnemyMoveType.Ground,
                unlock: 6, tint: 0x5B6577, scale: 1.1f);

            var splitter = Enemy(created, ContentIds.Splitter, "Bog Ooze", "Bursts into three slimelings when killed.",
                hp: 18f, speed: 1.6f, gold: 8, leak: 1, melee: 3f, interval: 1.2f, ArmorType.None, EnemyMoveType.Ground,
                unlock: 7, tint: 0x7FBF3F, scale: 1.0f);
            splitter.abilities.Add(Split(created, "Burst", slimeling, count: 3, fraction: 0.3f, reward: 0.5f));

            var warder = Enemy(created, ContentIds.Warder, "Cultist Warder", "Raises a ward that blocks all damage for a moment.",
                hp: 16f, speed: 1.8f, gold: 11, leak: 1, melee: 3f, interval: 1f, ArmorType.Light, EnemyMoveType.Ground,
                unlock: 8, tint: 0xB0457A, scale: 0.9f);
            warder.abilities.Add(Shield(created, "Dark Ward", delay: 2f, cooldown: 7f, duration: 1.5f));

            var troll = Enemy(created, ContentIds.Troll, "Cave Troll", "Regenerates when left alone. Poison stops it.",
                hp: 35f, speed: 1.4f, gold: 14, leak: 2, melee: 7f, interval: 1.4f, ArmorType.Light, EnemyMoveType.Ground,
                unlock: 9, tint: 0x7C8F6A, scale: 1.25f);
            troll.abilities.Add(Regenerate(created, "Troll Blood", percentPerSecond: 0.04f, delay: 2f));

            var wyvern = Enemy(created, ContentIds.Wyvern, "Wyvern", "Armored flyer. Archers and mages must bring it down.",
                hp: 24f, speed: 2.2f, gold: 12, leak: 2, melee: 0f, interval: 1f, ArmorType.Light, EnemyMoveType.Flying,
                unlock: 10, tint: 0xB5532F, scale: 1.15f);

            return new List<EnemyData> { grunt, runner, bat, brute, shaman, knight, splitter, warder, troll, wyvern };
        }

        private static List<EnemyData> BuildBosses(List<Object> created, List<EnemyData> minions, List<EnemyData> enemies)
        {
            EnemyData skeleton = Find(minions, ContentIds.Skeleton);
            EnemyData spiderling = Find(minions, ContentIds.Spiderling);
            EnemyData bat = Find(enemies, ContentIds.Bat);

            var warlord = Boss(created, ContentIds.Warlord, "Goblin Warlord",
                "Charges down the road every few seconds and flies into a rage when wounded.",
                healthMult: 14f, goldMult: 15, ArmorType.Light, EnemyMoveType.Ground, leak: 5, melee: 15f, tint: 0xC4552E, scale: 1.8f);
            warlord.abilities.Add(Charge(created, "War Charge", delay: 3f, cooldown: 6f, duration: 1.2f, speed: 2.5f));
            warlord.abilities.Add(Enrage(created, "Bloodlust", threshold: 0.4f, speed: 1.4f, melee: 2f, rate: 1.5f));

            var necromancer = Boss(created, ContentIds.Necromancer, "Necromancer",
                "Raises skeletons as it walks and hides behind a bone ward.",
                healthMult: 12f, goldMult: 15, ArmorType.None, EnemyMoveType.Ground, leak: 5, melee: 8f, tint: 0x4B3A6B, scale: 1.6f);
            necromancer.abilities.Add(Summon(created, "Raise Dead", skeleton, count: 3, delay: 4f, cooldown: 8f, health: 1f, maxAlive: 9));
            necromancer.abilities.Add(Shield(created, "Bone Ward", delay: 3f, cooldown: 9f, duration: 2f));

            var broodmother = Boss(created, ContentIds.Broodmother, "Broodmother",
                "A heavily armored spider queen. Lays spiderlings on the move and bursts into a swarm on death.",
                healthMult: 16f, goldMult: 18, ArmorType.Heavy, EnemyMoveType.Ground, leak: 6, melee: 12f, tint: 0x4A2C3A, scale: 2.0f);
            broodmother.abilities.Add(Summon(created, "Lay Eggs", spiderling, count: 4, delay: 3f, cooldown: 7f, health: 1f, maxAlive: 12));
            broodmother.abilities.Add(Split(created, "Brood Burst", spiderling, count: 6, fraction: 0.05f, reward: 0.5f));

            var drake = Boss(created, ContentIds.Drake, "Ember Drake",
                "Armored flying boss: artillery can't reach it. Calls bats and enrages at half health.",
                healthMult: 18f, goldMult: 20, ArmorType.Heavy, EnemyMoveType.Flying, leak: 8, melee: 0f, tint: 0xD9502B, scale: 2.2f);
            drake.abilities.Add(Summon(created, "Call the Swarm", bat, count: 2, delay: 5f, cooldown: 10f, health: 0.8f, maxAlive: 6));
            drake.abilities.Add(Enrage(created, "Inferno", threshold: 0.5f, speed: 1.35f, melee: 1f, rate: 1f));

            return new List<EnemyData> { warlord, necromancer, broodmother, drake };
        }

        private static EnemyData Enemy(List<Object> created, string id, string name, string description,
                                       float hp, float speed, int gold, int leak, float melee, float interval,
                                       ArmorType armor, EnemyMoveType move, int unlock, uint tint, float scale)
        {
            var e = Track(created, ScriptableObject.CreateInstance<EnemyData>());
            e.name = id;
            e.id = id;
            e.enemyName = name;
            e.description = description;
            e.tint = LevelFactory.ToColor(tint);
            e.visualScale = scale;
            e.baseHealth = hp;
            e.baseSpeed = speed;
            e.baseGoldReward = gold;
            e.damageToBase = leak;
            e.meleeDamage = melee;
            e.attackInterval = interval;
            e.armor = armor;
            e.moveType = move;
            e.unlockWave = unlock;
            e.isBoss = false;
            e.abilities = new List<EnemyAbilityData>();
            return e;
        }

        private static EnemyData Boss(List<Object> created, string id, string name, string description,
                                      float healthMult, int goldMult, ArmorType armor, EnemyMoveType move,
                                      int leak, float melee, uint tint, float scale)
        {
            // baseHealth/baseSpeed are not used for bosses by WaveManager (curve x multiplier, 0.6x speed),
            // but keep sensible values for tooltips and any custom spawner.
            var b = Enemy(created, id, name, description, hp: 10f * healthMult, speed: 1.2f, gold: 5 * goldMult, leak: leak,
                          melee: melee, interval: 1f, armor, move, unlock: 1, tint: tint, scale: scale);
            b.isBoss = true;
            b.bossHealthMultiplier = healthMult;
            b.bossGoldMultiplier = goldMult;
            return b;
        }

        private static EnemyData Find(List<EnemyData> list, string id)
        {
            foreach (var e in list) if (e.id == id) return e;
            return null;
        }

        // ================================================================ abilities

        private static T Ability<T>(List<Object> created, string name, string description) where T : EnemyAbilityData
        {
            var a = Track(created, ScriptableObject.CreateInstance<T>());
            a.name = name;
            a.abilityName = name;
            a.description = description;
            return a;
        }

        private static EnrageAbility Enrage(List<Object> created, string name, float threshold, float speed, float melee, float rate)
        {
            var a = Ability<EnrageAbility>(created, name, $"Below {threshold:P0} health: {speed:0.##}x speed, {melee:0.##}x melee damage.");
            a.healthThreshold = threshold;
            a.speedMultiplier = speed;
            a.meleeDamageMultiplier = melee;
            a.attackRateMultiplier = rate;
            return a;
        }

        private static ChargeAbility Charge(List<Object> created, string name, float delay, float cooldown, float duration, float speed)
        {
            var a = Ability<ChargeAbility>(created, name, $"Every {cooldown:0.#}s, dashes at {speed:0.#}x speed for {duration:0.#}s.");
            a.initialDelay = delay;
            a.cooldown = cooldown;
            a.duration = duration;
            a.speedMultiplier = speed;
            return a;
        }

        private static SummonAbility Summon(List<Object> created, string name, EnemyData minion, int count, float delay, float cooldown,
                                            float health, int maxAlive)
        {
            string minionName = minion != null ? minion.enemyName : "minions";
            var a = Ability<SummonAbility>(created, name, $"Every {cooldown:0.#}s, summons {count} {minionName}.");
            a.minion = minion;
            a.countPerCast = count;
            a.initialDelay = delay;
            a.cooldown = cooldown;
            a.healthScale = health;
            a.rewardScale = 0.5f;
            a.maxAlive = maxAlive;
            return a;
        }

        private static HealAuraAbility HealAura(List<Object> created, string name, float radius, float interval, float percent)
        {
            var a = Ability<HealAuraAbility>(created, name, $"Every {interval:0.#}s, heals nearby allies for {percent:P0} of their health.");
            a.radius = radius;
            a.interval = interval;
            a.healPercent = percent;
            a.includeSelf = false;
            a.healBosses = false;
            return a;
        }

        private static ShieldAbility Shield(List<Object> created, string name, float delay, float cooldown, float duration)
        {
            var a = Ability<ShieldAbility>(created, name, $"Every {cooldown:0.#}s, immune to damage for {duration:0.#}s.");
            a.initialDelay = delay;
            a.cooldown = cooldown;
            a.duration = duration;
            return a;
        }

        private static SplitOnDeathAbility Split(List<Object> created, string name, EnemyData child, int count, float fraction, float reward)
        {
            string childName = child != null ? child.enemyName : "minions";
            var a = Ability<SplitOnDeathAbility>(created, name, $"On death, splits into {count} {childName}.");
            a.child = child;
            a.count = count;
            a.childHealthFraction = fraction;
            a.rewardScale = reward;
            return a;
        }

        private static RegenerateAbility Regenerate(List<Object> created, string name, float percentPerSecond, float delay)
        {
            var a = Ability<RegenerateAbility>(created, name,
                $"After {delay:0.#}s without damage, regenerates {percentPerSecond:P0} health per second.");
            a.healPercentPerSecond = percentPerSecond;
            a.regenDelay = delay;
            return a;
        }

        private static T Track<T>(List<Object> created, T obj) where T : Object
        {
            if (obj != null) created.Add(obj);
            return obj;
        }
    }
}
