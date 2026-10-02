using System.Collections.Generic;

namespace TowerDefense.Levels
{
    /// <summary>
    /// Engine-free description of one hand-authored map: the same information
    /// as a LevelData asset, but with plain floats and content ids instead of
    /// Unity types. CampaignLayouts authors these; LevelFactory turns them into
    /// LevelData at runtime; LayoutValidator checks them on plain .NET.
    /// </summary>
    public sealed class LevelLayout
    {
        public string Id;
        public string DisplayName;
        public string Description;

        public float Width = 32f;
        public float Height = 18f;
        /// <summary>Each path: spawn first, exit last.</summary>
        public List<LayoutPoint[]> Paths = new List<LayoutPoint[]>();
        public List<LayoutPoint> BuildSlots = new List<LayoutPoint>();
        public LayoutPoint HeroStart;

        public int StartingGold = 250;
        public int StartingLives = 20;
        /// <summary>0 = endless.</summary>
        public int WavesToWin = 15;
        public float DifficultyMultiplier = 1f;

        /// <summary>Enemy ids (ContentIds). unlockWave on each EnemyData still gates when it appears.</summary>
        public List<string> EnemyPool = new List<string>();
        public List<string> BossPool = new List<string>();
        public int BossEveryNWaves = 5;

        /// <summary>Theme colours as 0xRRGGBB.</summary>
        public uint GroundColor = 0x5C8C4A;
        public uint PathColor = 0xC2A36B;
        public uint AccentColor = 0x406B33;

        /// <summary>For endless variants: the campaign level this one was derived from. Null for campaign maps.</summary>
        public string BaseLevelId;

        public bool IsEndless => WavesToWin == 0;

        /// <summary>Paths as read-only lists (for the geometry helpers).</summary>
        public IReadOnlyList<IReadOnlyList<LayoutPoint>> PathList
        {
            get
            {
                var list = new List<IReadOnlyList<LayoutPoint>>(Paths.Count);
                foreach (var p in Paths) list.Add(p);
                return list;
            }
        }

        /// <summary>Deep copy (geometry arrays and pools are duplicated).</summary>
        public LevelLayout Clone()
        {
            var c = (LevelLayout)MemberwiseClone();
            c.Paths = new List<LayoutPoint[]>(Paths.Count);
            foreach (var p in Paths) c.Paths.Add((LayoutPoint[])p.Clone());
            c.BuildSlots = new List<LayoutPoint>(BuildSlots);
            c.EnemyPool = new List<string>(EnemyPool);
            c.BossPool = new List<string>(BossPool);
            return c;
        }
    }
}
