using System;
using System.Collections.Generic;
using TowerDefense.Levels;
using UnityEngine;

namespace TowerDefense.Content
{
    /// <summary>
    /// Every tower, enemy, boss and level the game ships with, looked up by
    /// stable id. ContentCatalog.Default is built in code (no .asset files);
    /// FromAsset wraps a baked ContentCatalogAsset instead.
    /// Prefabs are NOT assigned here: Visuals builds runtime templates from the
    /// data fields and assigns TowerData.towerPrefab / projectilePrefab and
    /// EnemyData.prefab (iterate Towers and AllEnemies).
    /// </summary>
    public sealed class ContentCatalog
    {
        private static ContentCatalog defaultCatalog;

        private readonly List<TowerData> towers;
        private readonly List<EnemyData> enemies;
        private readonly List<EnemyData> minions;
        private readonly List<EnemyData> bosses;
        private readonly List<EnemyData> allEnemies;
        private readonly List<LevelData> campaignLevels;
        private readonly List<LevelData> endlessLevels;
        private readonly List<string> campaignOrder;

        private readonly Dictionary<string, TowerData> towerById = new Dictionary<string, TowerData>();
        private readonly Dictionary<string, EnemyData> enemyById = new Dictionary<string, EnemyData>();
        private readonly Dictionary<string, LevelData> levelById = new Dictionary<string, LevelData>();
        private readonly Dictionary<string, LevelData> endlessByBaseId = new Dictionary<string, LevelData>();

        /// <summary>Shared catalog built from DefaultContent on first use. Same instances for the whole session.</summary>
        public static ContentCatalog Default => defaultCatalog ??= DefaultContent.Build();

        /// <summary>Drop the cached default (next access rebuilds). Also runs on play-mode entry for fast-enter-play-mode setups.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetDefault() => defaultCatalog = null;

        /// <summary>Use a baked catalog asset (e.g. designer-tuned) as the default for this session.</summary>
        public static void SetDefault(ContentCatalog catalog) => defaultCatalog = catalog;

        public ContentCatalog(IEnumerable<TowerData> towers, IEnumerable<EnemyData> enemies, IEnumerable<EnemyData> minions,
                              IEnumerable<EnemyData> bosses, IEnumerable<LevelData> campaignLevels, IEnumerable<LevelData> endlessLevels)
        {
            this.towers = NonNull(towers);
            this.enemies = NonNull(enemies);
            this.minions = NonNull(minions);
            this.bosses = NonNull(bosses);
            this.campaignLevels = NonNull(campaignLevels);
            this.endlessLevels = NonNull(endlessLevels);

            foreach (var t in this.towers) Index(towerById, t.id, t, "tower");
            allEnemies = new List<EnemyData>();
            allEnemies.AddRange(this.enemies);
            allEnemies.AddRange(this.minions);
            allEnemies.AddRange(this.bosses);
            foreach (var e in allEnemies) Index(enemyById, e.id, e, "enemy");

            campaignOrder = new List<string>(this.campaignLevels.Count);
            foreach (var l in this.campaignLevels)
            {
                Index(levelById, l.id, l, "level");
                campaignOrder.Add(l.id);
            }
            foreach (var l in this.endlessLevels)
            {
                Index(levelById, l.id, l, "level");
                string baseId = string.IsNullOrEmpty(l.baseLevelId) ? ContentIds.BaseLevelId(l.id) : l.baseLevelId;
                if (!string.IsNullOrEmpty(baseId)) endlessByBaseId[baseId] = l;
            }
        }

        /// <summary>Wrap a baked ContentCatalogAsset.</summary>
        public static ContentCatalog FromAsset(ContentCatalogAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            return new ContentCatalog(asset.towers, asset.enemies, asset.minions, asset.bosses, asset.campaignLevels, asset.endlessLevels);
        }

        // ---------------------------------------------------------------- lists

        /// <summary>Buildable towers, in shop order.</summary>
        public IReadOnlyList<TowerData> Towers => towers;
        /// <summary>Regular enemies (wave pools).</summary>
        public IReadOnlyList<EnemyData> Enemies => enemies;
        /// <summary>Ability-only enemies (summons, split children). Still need prefabs.</summary>
        public IReadOnlyList<EnemyData> Minions => minions;
        public IReadOnlyList<EnemyData> Bosses => bosses;
        /// <summary>Enemies + minions + bosses: everything WaveManager may spawn. Assign a prefab to each.</summary>
        public IReadOnlyList<EnemyData> AllEnemies => allEnemies;
        /// <summary>Campaign levels in play order.</summary>
        public IReadOnlyList<LevelData> CampaignLevels => campaignLevels;
        /// <summary>Endless variants (wavesToWin = 0), campaign order.</summary>
        public IReadOnlyList<LevelData> EndlessLevels => endlessLevels;
        /// <summary>Campaign level ids in play order (feed to CampaignProgression).</summary>
        public IReadOnlyList<string> CampaignOrder => campaignOrder;

        /// <summary>Campaign then endless levels.</summary>
        public IEnumerable<LevelData> AllLevels()
        {
            foreach (var l in campaignLevels) yield return l;
            foreach (var l in endlessLevels) yield return l;
        }

        // ---------------------------------------------------------------- lookups

        public TowerData GetTower(string id) => id != null && towerById.TryGetValue(id, out var t) ? t : null;
        /// <summary>Any enemy, minion or boss by id.</summary>
        public EnemyData GetEnemy(string id) => id != null && enemyById.TryGetValue(id, out var e) ? e : null;
        /// <summary>Campaign or endless level by id.</summary>
        public LevelData GetLevel(string id) => id != null && levelById.TryGetValue(id, out var l) ? l : null;

        public bool TryGetTower(string id, out TowerData tower) => (tower = GetTower(id)) != null;
        public bool TryGetEnemy(string id, out EnemyData enemy) => (enemy = GetEnemy(id)) != null;
        public bool TryGetLevel(string id, out LevelData level) => (level = GetLevel(id)) != null;

        /// <summary>Endless variant of a campaign level (or null).</summary>
        public LevelData GetEndlessVariant(string campaignLevelId) =>
            campaignLevelId != null && endlessByBaseId.TryGetValue(campaignLevelId, out var l) ? l : null;

        /// <summary>Campaign level at a 0-based position, or null.</summary>
        public LevelData GetCampaignLevel(int index) =>
            index >= 0 && index < campaignLevels.Count ? campaignLevels[index] : null;

        public int CampaignIndexOf(string levelId) => campaignOrder.IndexOf(ContentIds.BaseLevelId(levelId));

        // ---------------------------------------------------------------- progression / validation

        /// <summary>
        /// Unlock rules over this catalog's campaign order. starLookup comes from
        /// the save system (levelId -> 0..3 stars).
        /// </summary>
        public CampaignProgression CreateProgression(Func<string, int> starLookup,
                                                     int requiredStarsToAdvance = 1, int requiredStarsForEndless = 1)
        {
            return new CampaignProgression(campaignOrder, starLookup, requiredStarsToAdvance, requiredStarsForEndless);
        }

        /// <summary>Run LevelValidator over every level in the catalog.</summary>
        public List<LevelIssue> ValidateLevels(bool requirePrefabs = false)
        {
            var rules = new LayoutRules();
            return LevelValidator.ValidateAll(AllLevels(), rules, requirePrefabs);
        }

        // ---------------------------------------------------------------- helpers

        private static List<T> NonNull<T>(IEnumerable<T> items) where T : UnityEngine.Object
        {
            var list = new List<T>();
            if (items == null) return list;
            foreach (var item in items)
                if (item != null) list.Add(item);
            return list;
        }

        private static void Index<T>(Dictionary<string, T> map, string id, T item, string kind)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"ContentCatalog: a {kind} has no id and cannot be looked up.");
                return;
            }
            if (map.ContainsKey(id))
            {
                Debug.LogWarning($"ContentCatalog: duplicate {kind} id '{id}', keeping the first.");
                return;
            }
            map[id] = item;
        }
    }
}
