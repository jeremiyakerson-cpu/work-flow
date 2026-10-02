using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Levels
{
    /// <summary>
    /// Converts between the engine-free LevelLayout (authored in
    /// CampaignLayouts, validated on plain .NET) and the LevelData
    /// ScriptableObject the game runs on.
    /// </summary>
    public static class LevelFactory
    {
        /// <summary>
        /// Build a runtime LevelData (ScriptableObject.CreateInstance, no .asset
        /// needed). resolveEnemy maps an enemy/boss id to its EnemyData; unknown
        /// ids are skipped with a warning.
        /// </summary>
        public static LevelData Create(LevelLayout layout, Func<string, EnemyData> resolveEnemy)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var level = ScriptableObject.CreateInstance<LevelData>();
            Apply(layout, level, resolveEnemy);
            return level;
        }

        /// <summary>Overwrite every field of an existing LevelData from a layout.</summary>
        public static void Apply(LevelLayout layout, LevelData level, Func<string, EnemyData> resolveEnemy)
        {
            level.name = layout.Id;
            level.id = layout.Id;
            level.displayName = layout.DisplayName;
            level.description = layout.Description;
            level.baseLevelId = layout.BaseLevelId ?? string.Empty;

            level.worldSize = new Vector2(layout.Width, layout.Height);
            level.paths = new List<PathDefinition>(layout.Paths.Count);
            foreach (var pts in layout.Paths)
            {
                var def = new PathDefinition { points = new List<Vector2>(pts.Length) };
                foreach (var p in pts) def.points.Add(ToVector(p));
                level.paths.Add(def);
            }
            level.buildSlots = new List<Vector2>(layout.BuildSlots.Count);
            foreach (var s in layout.BuildSlots) level.buildSlots.Add(ToVector(s));
            level.heroStart = ToVector(layout.HeroStart);

            level.startingGold = layout.StartingGold;
            level.startingLives = layout.StartingLives;
            level.wavesToWin = layout.WavesToWin;
            level.difficultyMultiplier = layout.DifficultyMultiplier;

            level.enemyPool = Resolve(layout.EnemyPool, resolveEnemy, layout.Id);
            level.bossPool = Resolve(layout.BossPool, resolveEnemy, layout.Id);
            level.bossEveryNWaves = layout.BossEveryNWaves;

            level.groundColor = ToColor(layout.GroundColor);
            level.pathColor = ToColor(layout.PathColor);
            level.accentColor = ToColor(layout.AccentColor);
        }

        /// <summary>
        /// Engine-free view of a LevelData (for LayoutValidator). Enemy refs
        /// become their ids; a null entry becomes a null id (reported as such).
        /// </summary>
        public static LevelLayout ToLayout(LevelData level)
        {
            var layout = new LevelLayout
            {
                Id = level.id,
                DisplayName = level.displayName,
                Description = level.description,
                Width = level.worldSize.x,
                Height = level.worldSize.y,
                HeroStart = ToPoint(level.heroStart),
                StartingGold = level.startingGold,
                StartingLives = level.startingLives,
                WavesToWin = level.wavesToWin,
                DifficultyMultiplier = level.difficultyMultiplier,
                BossEveryNWaves = level.bossEveryNWaves,
                BaseLevelId = string.IsNullOrEmpty(level.baseLevelId) ? null : level.baseLevelId,
            };

            if (level.paths != null)
            {
                foreach (var def in level.paths)
                {
                    var pts = new List<LayoutPoint>();
                    if (def != null && def.points != null)
                        foreach (var p in def.points) pts.Add(ToPoint(p));
                    layout.Paths.Add(pts.ToArray());
                }
            }
            if (level.buildSlots != null)
                foreach (var s in level.buildSlots) layout.BuildSlots.Add(ToPoint(s));
            if (level.enemyPool != null)
                foreach (var e in level.enemyPool) layout.EnemyPool.Add(e != null ? e.id : null);
            if (level.bossPool != null)
                foreach (var e in level.bossPool) layout.BossPool.Add(e != null ? e.id : null);
            return layout;
        }

        public static Vector2 ToVector(LayoutPoint p) => new Vector2(p.X, p.Y);
        public static LayoutPoint ToPoint(Vector2 v) => new LayoutPoint(v.x, v.y);

        /// <summary>0xRRGGBB to an opaque Color.</summary>
        public static Color ToColor(uint rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        private static List<EnemyData> Resolve(List<string> ids, Func<string, EnemyData> resolveEnemy, string levelId)
        {
            var list = new List<EnemyData>(ids != null ? ids.Count : 0);
            if (ids == null || resolveEnemy == null) return list;
            foreach (var id in ids)
            {
                EnemyData data = resolveEnemy(id);
                if (data != null) list.Add(data);
                else Debug.LogWarning($"Level '{levelId}': unknown enemy id '{id}' skipped.");
            }
            return list;
        }
    }
}
