using System;
using System.Collections.Generic;
using TowerDefense.Content;

namespace TowerDefense.Levels
{
    /// <summary>
    /// The hand-authored campaign maps, as engine-free data. World is 32 x 18
    /// (landscape), (0,0) bottom-left. Paths start just off-screen and leave
    /// through an exit edge; build slots sit 2 units beside a path (validator
    /// minimum 1.3) so every slot covers the road without blocking it.
    /// Endless variants are derived from each campaign map (wavesToWin = 0).
    /// </summary>
    public static class CampaignLayouts
    {
        /// <summary>Campaign order. Level N+1 unlocks when level N has at least one star.</summary>
        public static readonly string[] CampaignOrder =
        {
            ContentIds.Meadow, ContentIds.Crossroads, ContentIds.Frostfang, ContentIds.Marsh, ContentIds.Citadel,
        };

        /// <summary>Fresh copies of every campaign layout, in campaign order.</summary>
        public static List<LevelLayout> CreateCampaign()
        {
            return new List<LevelLayout> { Meadow(), Crossroads(), Frostfang(), Marsh(), Citadel() };
        }

        /// <summary>Fresh copies of every endless variant, in campaign order.</summary>
        public static List<LevelLayout> CreateEndless()
        {
            var list = new List<LevelLayout>();
            foreach (var level in CreateCampaign()) list.Add(MakeEndless(level));
            return list;
        }

        /// <summary>Campaign and endless layouts together.</summary>
        public static List<LevelLayout> CreateAll()
        {
            var all = CreateCampaign();
            all.AddRange(CreateEndless());
            return all;
        }

        // ---------------------------------------------------------------- endless model

        /// <summary>Endless difficulty is the campaign map's, times this.</summary>
        public const float EndlessDifficultyScale = 1.15f;
        /// <summary>Endless starts with extra gold (rounded to 10) so the opening build is not the bottleneck.</summary>
        public const float EndlessGoldScale = 1.2f;
        public const int EndlessBossEveryNWaves = 5;

        /// <summary>
        /// Endless variant of a campaign map: same geometry, wavesToWin = 0, the
        /// full enemy roster (each type still gated by its unlockWave) and every
        /// boss, so long runs keep introducing threats while WaveManager's curve
        /// compounds. Id is ContentIds.EndlessIdFor(campaign id).
        /// </summary>
        public static LevelLayout MakeEndless(LevelLayout campaign)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            var e = campaign.Clone();
            e.Id = ContentIds.EndlessIdFor(campaign.Id);
            e.BaseLevelId = campaign.Id;
            e.DisplayName = campaign.DisplayName + " (Endless)";
            e.Description = "Endless: survive as long as you can. " + campaign.Description;
            e.WavesToWin = 0;
            e.DifficultyMultiplier = campaign.DifficultyMultiplier * EndlessDifficultyScale;
            e.StartingGold = (int)Math.Round(campaign.StartingGold * EndlessGoldScale / 10.0) * 10;
            e.EnemyPool = new List<string>(ContentIds.Enemies);
            e.BossPool = new List<string>(ContentIds.Bosses);
            e.BossEveryNWaves = EndlessBossEveryNWaves;
            return e;
        }

        // ---------------------------------------------------------------- helpers

        private static LayoutPoint P(float x, float y) => new LayoutPoint(x, y);

        /// <summary>Path from flat x,y pairs.</summary>
        private static LayoutPoint[] Path(params float[] xy)
        {
            var pts = new LayoutPoint[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new LayoutPoint(xy[i * 2], xy[i * 2 + 1]);
            return pts;
        }

        private static List<LayoutPoint> Slots(params float[] xy) => new List<LayoutPoint>(Path(xy));

        // ---------------------------------------------------------------- 1. tutorial, one path

        /// <summary>Single serpentine road. Teaches building, upgrading and the hero.</summary>
        public static LevelLayout Meadow() => new LevelLayout
        {
            Id = ContentIds.Meadow,
            DisplayName = "Greenleaf Meadow",
            Description = "A lone goblin trail winds through the farmland. Learn to build, upgrade and send your hero.",
            Paths =
            {
                Path(-1f, 13f, 7f, 13f, 7f, 5f, 17f, 5f, 17f, 13f, 25f, 13f, 25f, 7f, 33f, 7f),
            },
            BuildSlots = Slots(
                3f, 11f,   9f, 9f,   5f, 8f,   12f, 7f,   12f, 3f,   15f, 9f,
                19f, 9f,   21f, 11f, 21f, 15f, 23f, 9f,   27f, 10f,  29f, 5f),
            HeroStart = P(12f, 5f),
            StartingGold = 260,
            StartingLives = 20,
            WavesToWin = 10,
            DifficultyMultiplier = 0.85f,
            EnemyPool = { ContentIds.Grunt, ContentIds.Runner, ContentIds.Bat, ContentIds.Brute },
            BossPool = { ContentIds.Warlord },
            BossEveryNWaves = 10,
            GroundColor = 0x5C8C4A, PathColor = 0xC9A86B, AccentColor = 0x3E6B2F,
        };

        // ---------------------------------------------------------------- 2. two paths that merge

        /// <summary>Two roads from the west join at a central crossroads: one hero, one chokepoint.</summary>
        public static LevelLayout Crossroads() => new LevelLayout
        {
            Id = ContentIds.Crossroads,
            DisplayName = "Miller's Crossroads",
            Description = "Two roads converge at the mill. Hold the crossing or be overrun from both sides.",
            Paths =
            {
                Path(-1f, 15f, 9f, 15f, 9f, 9f, 22f, 9f, 22f, 14f, 33f, 14f),
                Path(-1f, 3f, 9f, 3f, 9f, 9f, 22f, 9f, 22f, 14f, 33f, 14f),
            },
            BuildSlots = Slots(
                4f, 13f,   4f, 5f,    11f, 13f,  7f, 12f,   7f, 6f,    11f, 5f,
                14f, 11f,  14f, 7f,   18f, 11f,  18f, 7f,   24f, 11f,  20f, 12f,
                26f, 16f,  27f, 12f,  30f, 16f),
            HeroStart = P(9f, 9f),
            StartingGold = 300,
            StartingLives = 20,
            WavesToWin = 12,
            DifficultyMultiplier = 1f,
            EnemyPool = { ContentIds.Grunt, ContentIds.Runner, ContentIds.Bat, ContentIds.Brute, ContentIds.Shaman, ContentIds.Knight },
            BossPool = { ContentIds.Warlord },
            BossEveryNWaves = 6,
            GroundColor = 0x6E9A4C, PathColor = 0xB8925A, AccentColor = 0x4F7A8C,
        };

        // ---------------------------------------------------------------- 3. two separate spawns

        /// <summary>Two independent roads (north gate and west valley) each with its own exit: split your defence.</summary>
        public static LevelLayout Frostfang() => new LevelLayout
        {
            Id = ContentIds.Frostfang,
            DisplayName = "Frostfang Pass",
            Description = "Raiders pour down from the northern gate and up the frozen valley. Two roads, two breaches.",
            Paths =
            {
                Path(5f, 19f, 5f, 12f, 16f, 12f, 16f, 15f, 26f, 15f, 26f, 12f, 33f, 12f),
                Path(-1f, 4f, 9f, 4f, 9f, 8f, 21f, 8f, 21f, 5f, 33f, 5f),
            },
            BuildSlots = Slots(
                3f, 14f,   7f, 15f,   11f, 10f,  14f, 10f,  18f, 13f,  20f, 17f,
                24f, 13f,  29f, 14f,  29f, 9f,   5f, 6f,    4f, 2f,    11f, 6f,
                17f, 6f,   19f, 10f,  23f, 7f,   27f, 3f),
            HeroStart = P(16f, 12f),
            StartingGold = 340,
            StartingLives = 20,
            WavesToWin = 14,
            DifficultyMultiplier = 1.05f,
            EnemyPool =
            {
                ContentIds.Grunt, ContentIds.Runner, ContentIds.Bat, ContentIds.Brute, ContentIds.Shaman,
                ContentIds.Knight, ContentIds.Splitter, ContentIds.Warder,
            },
            BossPool = { ContentIds.Necromancer },
            BossEveryNWaves = 7,
            GroundColor = 0xDCE6EE, PathColor = 0x9AA7B4, AccentColor = 0x6F8FAF,
        };

        // ---------------------------------------------------------------- 4. three paths

        /// <summary>West, north and south fords all feed one eastern causeway.</summary>
        public static LevelLayout Marsh() => new LevelLayout
        {
            Id = ContentIds.Marsh,
            DisplayName = "Fenwick Fords",
            Description = "Three fords cross the marsh and meet at the old causeway. Every approach needs an answer.",
            Paths =
            {
                Path(-1f, 9f, 10f, 9f, 10f, 12f, 18f, 12f, 18f, 9f, 33f, 9f),
                Path(14f, 19f, 14f, 15f, 22f, 15f, 22f, 9f, 33f, 9f),
                Path(6f, -1f, 6f, 4f, 22f, 4f, 22f, 9f, 33f, 9f),
            },
            BuildSlots = Slots(
                4f, 11f,   4f, 7f,    8f, 6f,    12f, 10f,  12f, 17f,  16f, 10f,
                20f, 11f,  18f, 17f,  24f, 13f,  20f, 13f,  24f, 6f,   12f, 2f,
                12f, 6f,   18f, 6f,   28f, 11f,  28f, 7f),
            HeroStart = P(22f, 9f),
            StartingGold = 380,
            StartingLives = 20,
            WavesToWin = 15,
            DifficultyMultiplier = 1.1f,
            EnemyPool =
            {
                ContentIds.Grunt, ContentIds.Runner, ContentIds.Bat, ContentIds.Brute, ContentIds.Shaman,
                ContentIds.Knight, ContentIds.Splitter, ContentIds.Warder, ContentIds.Troll,
            },
            BossPool = { ContentIds.Broodmother, ContentIds.Necromancer },
            BossEveryNWaves = 5,
            GroundColor = 0x4A5E3A, PathColor = 0x7A6A48, AccentColor = 0x2F4A3F,
        };

        // ---------------------------------------------------------------- 5. finale

        /// <summary>Two braided switchback roads merge at the citadel gate. Every enemy and boss.</summary>
        public static LevelLayout Citadel() => new LevelLayout
        {
            Id = ContentIds.Citadel,
            DisplayName = "Shadow Citadel",
            Description = "The Dark Host marches on the citadel along twin switchbacks. Twenty waves, every horror, no mercy.",
            Paths =
            {
                Path(-1f, 15f, 7f, 15f, 7f, 11f, 14f, 11f, 14f, 15f, 22f, 15f, 22f, 11f, 26f, 11f, 26f, 9f, 33f, 9f),
                Path(-1f, 3f, 7f, 3f, 7f, 7f, 14f, 7f, 14f, 3f, 22f, 3f, 22f, 7f, 26f, 7f, 26f, 9f, 33f, 9f),
            },
            BuildSlots = Slots(
                3f, 13f,   3f, 5f,    9f, 13f,   9f, 5f,    10f, 9f,   12f, 13f,
                16f, 13f,  20f, 13f,  18f, 5f,   20f, 9f,   24f, 13f,  24f, 5f,
                24f, 9f,   29f, 11f,  29f, 7f,   12f, 5f),
            HeroStart = P(26f, 9f),
            StartingGold = 450,
            StartingLives = 15,
            WavesToWin = 20,
            DifficultyMultiplier = 1.2f,
            EnemyPool = new List<string>(ContentIds.Enemies),
            BossPool = new List<string>(ContentIds.Bosses),
            BossEveryNWaves = 5,
            GroundColor = 0x3B3542, PathColor = 0x6D5F57, AccentColor = 0x8E2F3A,
        };
    }
}
