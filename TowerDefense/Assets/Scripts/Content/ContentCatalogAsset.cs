using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Content
{
    /// <summary>
    /// Serialized form of a ContentCatalog, written by the
    /// "Tower Defense/Content/Bake Default Content to Assets" menu so designers
    /// can tweak numbers in the Inspector. Load it with ContentCatalog.FromAsset;
    /// the game itself does not need it (ContentCatalog.Default is built in code).
    /// </summary>
    [CreateAssetMenu(fileName = "ContentCatalog", menuName = "Tower Defense/Content Catalog")]
    public class ContentCatalogAsset : ScriptableObject
    {
        public List<TowerData> towers = new List<TowerData>();
        [Tooltip("Enemies that appear in wave pools.")]
        public List<EnemyData> enemies = new List<EnemyData>();
        [Tooltip("Enemies only spawned by abilities (summons, splits).")]
        public List<EnemyData> minions = new List<EnemyData>();
        public List<EnemyData> bosses = new List<EnemyData>();
        [Tooltip("Campaign levels in play order.")]
        public List<LevelData> campaignLevels = new List<LevelData>();
        [Tooltip("Endless variants (wavesToWin = 0), same order as the campaign.")]
        public List<LevelData> endlessLevels = new List<LevelData>();
    }
}
