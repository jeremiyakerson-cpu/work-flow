using System;
using System.Collections.Generic;

namespace TowerDefense.Levels
{
    public enum IssueSeverity { Warning, Error }

    /// <summary>One validation finding: which level, which rule, and a precise message.</summary>
    public sealed class LevelIssue
    {
        public IssueSeverity Severity;
        public string LevelId;
        /// <summary>Stable rule key, e.g. "slot.too-close-to-path". Tests assert on it.</summary>
        public string Code;
        public string Message;

        public LevelIssue(IssueSeverity severity, string levelId, string code, string message)
        {
            Severity = severity;
            LevelId = levelId;
            Code = code;
            Message = message;
        }

        public bool IsError => Severity == IssueSeverity.Error;

        public override string ToString() => $"[{Severity}] {LevelId}: {Code} - {Message}";
    }

    /// <summary>Tunable thresholds for the layout rules (world units).</summary>
    public sealed class LayoutRules
    {
        public int MinBuildSlots = 10;
        public int MaxBuildSlots = 16;
        /// <summary>A slot centre must be at least this far from every path centreline.</summary>
        public float MinSlotPathClearance = 1.3f;
        /// <summary>A slot further than this from every path is useless (most towers range ~4).</summary>
        public float MaxSlotPathDistance = 3.2f;
        /// <summary>Minimum centre-to-centre distance between two slots (slot footprint ~1.4).</summary>
        public float MinSlotSpacing = 1.6f;
        /// <summary>Slots must sit at least this far inside the play area.</summary>
        public float SlotEdgeMargin = 1f;
        /// <summary>Path ends must be within this distance inside an edge (or anywhere outside it).</summary>
        public float PathEdgeTolerance = 0.5f;
        /// <summary>How far outside the play area a path point may lie (spawns just off-screen).</summary>
        public float MaxPathOutside = 2f;
        public float MinSegmentLength = 0.25f;
        /// <summary>The hero must start on a path (within this distance of a centreline).</summary>
        public float HeroOnPathTolerance = 0.3f;
        public int MinCampaignWaves = 5;
        public int MaxCampaignWaves = 40;

        /// <summary>When set, enemy pool ids must be in this list.</summary>
        public string[] KnownEnemyIds;
        /// <summary>When set, boss pool ids must be in this list.</summary>
        public string[] KnownBossIds;
    }

    /// <summary>
    /// Engine-free level rules. LevelValidator (Unity side) converts LevelData
    /// into LevelLayout and runs this, so the same checks run in the Editor menu,
    /// at runtime, and in the .NET content tests.
    /// </summary>
    public static class LayoutValidator
    {
        public static List<LevelIssue> Validate(LevelLayout level, LayoutRules rules = null)
        {
            rules ??= new LayoutRules();
            var issues = new List<LevelIssue>();
            if (level == null)
            {
                issues.Add(new LevelIssue(IssueSeverity.Error, "<null>", "level.null", "Level is null."));
                return issues;
            }

            string id = string.IsNullOrEmpty(level.Id) ? "<no id>" : level.Id;
            void Err(string code, string msg) => issues.Add(new LevelIssue(IssueSeverity.Error, id, code, msg));
            void Warn(string code, string msg) => issues.Add(new LevelIssue(IssueSeverity.Warning, id, code, msg));

            if (string.IsNullOrEmpty(level.Id)) Err("id.missing", "Level has no id.");
            if (string.IsNullOrEmpty(level.DisplayName)) Warn("name.missing", "Level has no display name.");

            float w = level.Width, h = level.Height;
            if (w <= 0f || h <= 0f)
            {
                Err("world.size", $"World size {w}x{h} must be positive.");
                return issues;
            }

            ValidatePaths(level, rules, w, h, Err);
            var paths = level.PathList;
            bool havePaths = paths.Count > 0;

            ValidateSlots(level, rules, w, h, paths, havePaths, Err, Warn);

            // Hero
            if (!LevelGeometry.InsideBounds(level.HeroStart, w, h, 0f))
                Err("hero.out-of-bounds", $"Hero start {level.HeroStart} is outside the play area.");
            if (havePaths)
            {
                float d = LevelGeometry.DistanceToPaths(level.HeroStart, paths);
                if (d > rules.HeroOnPathTolerance)
                    Err("hero.off-path", $"Hero start {level.HeroStart} is {d:0.00} from the nearest path (max {rules.HeroOnPathTolerance}).");
            }

            // Rules / economy
            if (level.StartingGold <= 0) Err("rules.gold", $"Starting gold {level.StartingGold} must be > 0.");
            if (level.StartingLives <= 0) Err("rules.lives", $"Starting lives {level.StartingLives} must be > 0.");
            if (level.WavesToWin < 0) Err("rules.waves", $"wavesToWin {level.WavesToWin} must be >= 0 (0 = endless).");
            else if (level.WavesToWin > 0 && (level.WavesToWin < rules.MinCampaignWaves || level.WavesToWin > rules.MaxCampaignWaves))
                Warn("rules.waves-range", $"wavesToWin {level.WavesToWin} outside the usual {rules.MinCampaignWaves}-{rules.MaxCampaignWaves}.");
            if (!(level.DifficultyMultiplier > 0f)) Err("rules.difficulty", $"Difficulty multiplier {level.DifficultyMultiplier} must be > 0.");
            if (level.BossEveryNWaves < 0) Err("rules.boss-interval", $"bossEveryNWaves {level.BossEveryNWaves} must be >= 0.");

            // Pools
            ValidatePool(level.EnemyPool, "enemy", rules.KnownEnemyIds, Err);
            if (level.EnemyPool == null || level.EnemyPool.Count == 0)
                Err("pool.enemy-empty", "Enemy pool is empty: WaveManager would spawn nothing.");
            ValidatePool(level.BossPool, "boss", rules.KnownBossIds, Err);
            if (level.BossEveryNWaves > 0 && (level.BossPool == null || level.BossPool.Count == 0))
                Warn("pool.boss-empty", $"bossEveryNWaves = {level.BossEveryNWaves} but the boss pool is empty: no boss waves.");
            if (level.WavesToWin > 0 && level.BossEveryNWaves > level.WavesToWin && level.BossPool != null && level.BossPool.Count > 0)
                Warn("pool.boss-unreachable", $"Boss every {level.BossEveryNWaves} waves never happens in a {level.WavesToWin}-wave level.");

            return issues;
        }

        /// <summary>Validate a set of levels, plus cross-level rules (unique ids).</summary>
        public static List<LevelIssue> ValidateAll(IEnumerable<LevelLayout> levels, LayoutRules rules = null)
        {
            var issues = new List<LevelIssue>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var level in levels)
            {
                issues.AddRange(Validate(level, rules));
                if (level != null && !string.IsNullOrEmpty(level.Id) && !seen.Add(level.Id))
                    issues.Add(new LevelIssue(IssueSeverity.Error, level.Id, "id.duplicate", $"Level id '{level.Id}' is used more than once."));
            }
            return issues;
        }

        public static bool HasErrors(List<LevelIssue> issues)
        {
            foreach (var i in issues) if (i.IsError) return true;
            return false;
        }

        // ---------------------------------------------------------------- paths

        private static void ValidatePaths(LevelLayout level, LayoutRules rules, float w, float h, Action<string, string> err)
        {
            if (level.Paths == null || level.Paths.Count == 0)
            {
                err("path.none", "Level has no paths.");
                return;
            }

            for (int p = 0; p < level.Paths.Count; p++)
            {
                var pts = level.Paths[p];
                if (pts == null || pts.Length < 2)
                {
                    err("path.too-short", $"Path {p} needs at least 2 points (has {(pts == null ? 0 : pts.Length)}).");
                    continue;
                }

                for (int i = 0; i < pts.Length; i++)
                {
                    float outside = LevelGeometry.DistanceOutside(pts[i], w, h);
                    bool endpoint = i == 0 || i == pts.Length - 1;
                    if (endpoint && outside > rules.MaxPathOutside)
                        err("path.far-outside", $"Path {p} point {i} {pts[i]} is {outside:0.00} outside the play area (max {rules.MaxPathOutside}).");
                    else if (!endpoint && outside > 0f)
                        err("path.interior-outside", $"Path {p} point {i} {pts[i]} leaves the play area mid-route.");

                    if (i > 0 && LevelGeometry.Distance(pts[i - 1], pts[i]) < rules.MinSegmentLength)
                        err("path.degenerate-segment", $"Path {p} segment {i - 1}->{i} is shorter than {rules.MinSegmentLength}.");
                }

                float startEdge = LevelGeometry.SignedDistanceToEdge(pts[0], w, h);
                if (startEdge > rules.PathEdgeTolerance)
                    err("path.spawn-not-at-edge", $"Path {p} spawn {pts[0]} is {startEdge:0.00} inside the edge (must start at or beyond the screen edge).");
                float endEdge = LevelGeometry.SignedDistanceToEdge(pts[pts.Length - 1], w, h);
                if (endEdge > rules.PathEdgeTolerance)
                    err("path.exit-not-at-edge", $"Path {p} exit {pts[pts.Length - 1]} is {endEdge:0.00} inside the edge (must end at an exit edge).");
            }
        }

        // ---------------------------------------------------------------- slots

        private static void ValidateSlots(LevelLayout level, LayoutRules rules, float w, float h,
                                          IReadOnlyList<IReadOnlyList<LayoutPoint>> paths, bool havePaths,
                                          Action<string, string> err, Action<string, string> warn)
        {
            var slots = level.BuildSlots ?? new List<LayoutPoint>();
            if (slots.Count < rules.MinBuildSlots || slots.Count > rules.MaxBuildSlots)
                err("slot.count", $"Has {slots.Count} build slots; expected {rules.MinBuildSlots}-{rules.MaxBuildSlots}.");

            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (!LevelGeometry.InsideBounds(s, w, h, rules.SlotEdgeMargin))
                    err("slot.out-of-bounds", $"Slot {i} {s} is not at least {rules.SlotEdgeMargin} inside the {w}x{h} play area.");

                if (havePaths)
                {
                    float d = LevelGeometry.DistanceToPaths(s, paths);
                    if (d < rules.MinSlotPathClearance)
                        err("slot.on-path", $"Slot {i} {s} is {d:0.00} from a path centreline (min {rules.MinSlotPathClearance}).");
                    else if (d > rules.MaxSlotPathDistance)
                        warn("slot.far-from-path", $"Slot {i} {s} is {d:0.00} from the nearest path (max useful {rules.MaxSlotPathDistance}).");
                }

                for (int j = i + 1; j < slots.Count; j++)
                {
                    float sd = LevelGeometry.Distance(s, slots[j]);
                    if (sd < rules.MinSlotSpacing)
                        err("slot.overlap", $"Slots {i} {s} and {j} {slots[j]} are {sd:0.00} apart (min {rules.MinSlotSpacing}).");
                }
            }
        }

        // ---------------------------------------------------------------- pools

        private static void ValidatePool(List<string> pool, string kind, string[] known, Action<string, string> err)
        {
            if (pool == null) return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < pool.Count; i++)
            {
                string e = pool[i];
                if (string.IsNullOrEmpty(e))
                {
                    err($"pool.{kind}-null", $"{kind} pool entry {i} is empty.");
                    continue;
                }
                if (!seen.Add(e)) err($"pool.{kind}-duplicate", $"{kind} pool lists '{e}' twice.");
                if (known != null && Array.IndexOf(known, e) < 0)
                    err($"pool.{kind}-unknown", $"{kind} pool entry '{e}' is not a known {kind} id.");
            }
        }
    }
}
