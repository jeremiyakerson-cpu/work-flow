using System.Collections.Generic;
using TowerDefense.Core;

namespace TowerDefense.Content
{
    /// <summary>
    /// One shipped tower's numbers and names, engine-free so the balance rules
    /// can be unit-tested on plain .NET (Tools/ContentTests). DefaultContent
    /// copies each entry into a TowerData (TowerData.ApplyUpgradeSpec).
    /// </summary>
    public sealed class TowerBalanceEntry
    {
        public string Id;
        public string Name;
        public string Description;
        public DamageType DamageType;
        public bool CanTargetGround = true;
        public bool CanTargetFlying = true;
        public float SlowMultiplier = 0.5f;
        public float SlowDuration = 1.5f;
        public float PoisonDuration = 3f;
        public string PathAName, PathADescription;
        public string PathBName, PathBDescription;
        /// <summary>Base stats and the whole upgrade curve (L2, L3, specializations A/B).</summary>
        public TowerUpgradeSpec Spec;
    }

    /// <summary>
    /// The shipped tower balance (Docs/CONTENT.md has the tables). Upgrade model:
    /// build (L1) -> two linear upgrades (L2, L3) -> one of two elite
    /// specializations (L4, the capstone). Cost curve: L2 &lt; L3 &lt; specialization,
    /// each a visible power jump (DPS roughly x1.55, x1.55, then x1.6-2).
    /// </summary>
    public static class TowerBalance
    {
        /// <summary>Fresh entries for the five shipped towers, in shop order.</summary>
        public static List<TowerBalanceEntry> Create()
        {
            var list = new List<TowerBalanceEntry>();

            // Archer: cheap, fast, single target, hits air. Falls off against heavy armor.
            var archer = Tower(ContentIds.Archer, "Archer Tower",
                "Fast volleys of arrows. Cheap, reliable, hits flyers. Weak against heavy armor.",
                DamageType.Physical, range: 4.5f, fireRate: 1.4f, damage: 4f, cost: 70);
            Level2(archer, cost: 80, dmg: 1.35f, range: 1.08f, rate: 1.15f);
            Level3(archer, cost: 110, dmg: 1.35f, range: 1.08f, rate: 1.15f);
            BranchA(archer, "Marksmen", "Longbow snipers: heavy arrows and the longest reach in the kingdom.",
                    cost: 160, dmg: 2.0f, range: 1.2f);
            BranchB(archer, "Volley Rangers", "Rapid volleys that shred swarms and flyers.",
                    cost: 150, rate: 1.85f);
            list.Add(archer);

            // Mage: slow, heavy magic bolts that ignore armor and deal +25% to heavy armor.
            var mage = Tower(ContentIds.Mage, "Mage Tower",
                "Arcane bolts ignore armor and scorch heavily armored foes. Slow to fire.",
                DamageType.Magic, range: 4.0f, fireRate: 0.6f, damage: 11f, cost: 100);
            Level2(mage, cost: 110, dmg: 1.4f, range: 1.08f, rate: 1.1f);
            Level3(mage, cost: 150, dmg: 1.4f, range: 1.08f, rate: 1.1f);
            BranchA(mage, "Archmage", "Devastating arcane blasts with extra reach; melts armored knights.",
                    cost: 220, dmg: 2.0f, range: 1.15f);
            BranchB(mage, "Arcane Barrage", "A torrent of rapid bolts that never lets up.",
                    cost: 210, rate: 1.9f);
            list.Add(mage);

            // Artillery: splash, ground only. Clears packs; can't touch flyers.
            var artillery = Tower(ContentIds.Artillery, "Artillery",
                "Lobs explosive shells that hit everything in the blast. Cannot target flyers.",
                DamageType.Physical, range: 4.2f, fireRate: 0.4f, damage: 14f, cost: 120);
            artillery.Spec.splashRadius = 1.5f;
            artillery.CanTargetFlying = false;
            Level2(artillery, cost: 130, dmg: 1.4f, range: 1.06f, rate: 1.1f, splash: 0.15f);
            Level3(artillery, cost: 180, dmg: 1.4f, range: 1.06f, rate: 1.1f, splash: 0.15f);
            BranchA(artillery, "Big Bertha", "Colossal shells: crushing damage and a huge blast radius.",
                    cost: 260, dmg: 2.0f, range: 1.1f, splash: 0.6f);
            BranchB(artillery, "Mortar Battery", "A rapid barrage of shells that keeps packs pinned down.",
                    cost: 250, rate: 1.8f, splash: 0.1f);
            list.Add(artillery);

            // Frost: low damage, strong slow. Force multiplier for every other tower.
            var frost = Tower(ContentIds.Frost, "Frost Spire",
                "Freezing shards slow enemies to a crawl, buying time for your other towers.",
                DamageType.Magic, range: 3.8f, fireRate: 1.0f, damage: 2.5f, cost: 90);
            frost.Spec.appliesSlow = true;
            frost.SlowMultiplier = 0.55f;
            frost.SlowDuration = 1.6f;
            Level2(frost, cost: 100, dmg: 1.4f, range: 1.08f, rate: 1.1f);
            Level3(frost, cost: 130, dmg: 1.4f, range: 1.08f, rate: 1.1f);
            BranchA(frost, "Glacier Spire", "Long-range ice lances that hit hard for a frost tower.",
                    cost: 190, dmg: 2.2f, range: 1.2f);
            BranchB(frost, "Blizzard", "Freezing gusts that slow every enemy caught in the storm.",
                    cost: 180, dmg: 1.1f, rate: 1.3f, splash: 1.2f);
            list.Add(frost);

            // Alchemist: poison flasks with a small splash. Poison ignores armor and stops regeneration.
            var alchemist = Tower(ContentIds.Alchemist, "Alchemist Lab",
                "Hurls poison flasks: damage over time that ignores armor and stops trolls regenerating.",
                DamageType.Poison, range: 4.0f, fireRate: 0.8f, damage: 3f, cost: 100);
            alchemist.Spec.splashRadius = 1.0f;
            alchemist.Spec.appliesPoison = true;
            alchemist.Spec.poisonDps = 4f;
            alchemist.PoisonDuration = 4f;
            // Used by the Acid Rain specialization (pathBAppliesSlow): sticky acid, mild slow.
            alchemist.SlowMultiplier = 0.75f;
            alchemist.SlowDuration = 1f;
            Level2(alchemist, cost: 110, dmg: 1.35f, range: 1.08f, rate: 1.1f, poison: 1.4f, splash: 0.1f);
            Level3(alchemist, cost: 150, dmg: 1.35f, range: 1.08f, rate: 1.1f, poison: 1.4f, splash: 0.1f);
            BranchA(alchemist, "Plague Doctor", "A virulent plague: poison twice as deadly.",
                    cost: 220, dmg: 1.3f, range: 1.1f, poison: 2.0f);
            BranchB(alchemist, "Acid Rain", "Wide corrosive splashes that burn and slow whole packs.",
                    cost: 210, rate: 1.5f, poison: 1.4f, splash: 0.5f, slow: true);
            list.Add(alchemist);

            return list;
        }

        private static TowerBalanceEntry Tower(string id, string name, string description, DamageType type,
                                               float range, float fireRate, float damage, int cost)
        {
            return new TowerBalanceEntry
            {
                Id = id,
                Name = name,
                Description = description,
                DamageType = type,
                Spec = TowerUpgradeSpec.Neutral(range, fireRate, damage, cost),
            };
        }

        private static void Level2(TowerBalanceEntry t, int cost, float dmg, float range, float rate,
                                   float poison = 1f, float splash = 0f)
        {
            t.Spec.level2Cost = cost;
            t.Spec.level2DamageMult = dmg;
            t.Spec.level2RangeMult = range;
            t.Spec.level2FireRateMult = rate;
            t.Spec.level2PoisonMult = poison;
            t.Spec.level2SplashBonus = splash;
        }

        private static void Level3(TowerBalanceEntry t, int cost, float dmg, float range, float rate,
                                   float poison = 1f, float splash = 0f)
        {
            t.Spec.level3Cost = cost;
            t.Spec.level3DamageMult = dmg;
            t.Spec.level3RangeMult = range;
            t.Spec.level3FireRateMult = rate;
            t.Spec.level3PoisonMult = poison;
            t.Spec.level3SplashBonus = splash;
        }

        private static void BranchA(TowerBalanceEntry t, string name, string description, int cost,
                                    float dmg = 1f, float range = 1f, float rate = 1f, float poison = 1f,
                                    float splash = 0f, bool slow = false)
        {
            t.PathAName = name;
            t.PathADescription = description;
            t.Spec.pathACost = cost;
            t.Spec.pathADamageMult = dmg;
            t.Spec.pathARangeMult = range;
            t.Spec.pathAFireRateMult = rate;
            t.Spec.pathAPoisonMult = poison;
            t.Spec.pathASplashBonus = splash;
            t.Spec.pathAAppliesSlow = slow;
        }

        private static void BranchB(TowerBalanceEntry t, string name, string description, int cost,
                                    float dmg = 1f, float range = 1f, float rate = 1f, float poison = 1f,
                                    float splash = 0f, bool slow = false)
        {
            t.PathBName = name;
            t.PathBDescription = description;
            t.Spec.pathBCost = cost;
            t.Spec.pathBDamageMult = dmg;
            t.Spec.pathBRangeMult = range;
            t.Spec.pathBFireRateMult = rate;
            t.Spec.pathBPoisonMult = poison;
            t.Spec.pathBSplashBonus = splash;
            t.Spec.pathBAppliesSlow = slow;
        }
    }
}
