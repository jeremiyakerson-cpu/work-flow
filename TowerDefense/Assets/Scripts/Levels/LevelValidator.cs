using System.Collections.Generic;
using System.Text;
using TowerDefense.Bosses;
using UnityEngine;

namespace TowerDefense.Levels
{
    /// <summary>
    /// Validates LevelData assets: every geometry/economy rule from the
    /// engine-free LayoutValidator, plus checks that need the real EnemyData
    /// (boss flags, unlock waves, ability references, prefabs). Usable at
    /// runtime (e.g. a debug check before loading a level) and from the
    /// "Tower Defense/Content/Validate Levels" Editor menu.
    /// </summary>
    public static class LevelValidator
    {
        /// <param name="requirePrefabs">Also require EnemyData.prefab on every spawnable enemy.
        /// Leave false for raw content; pass true once Visuals has assigned runtime templates.</param>
        public static List<LevelIssue> Validate(LevelData level, LayoutRules rules = null, bool requirePrefabs = false)
        {
            if (level == null)
                return new List<LevelIssue> { new LevelIssue(IssueSeverity.Error, "<null>", "level.null", "LevelData is null.") };

            var issues = LayoutValidator.Validate(LevelFactory.ToLayout(level), rules);
            string id = string.IsNullOrEmpty(level.id) ? level.name : level.id;
            void Err(string code, string msg) => issues.Add(new LevelIssue(IssueSeverity.Error, id, code, msg));
            void Warn(string code, string msg) => issues.Add(new LevelIssue(IssueSeverity.Warning, id, code, msg));

            if (level.worldSize.x > 0f && level.worldSize.y > 0f &&
                Mathf.Abs(level.worldSize.x / level.worldSize.y - 16f / 9f) > 0.01f)
                Warn("world.aspect", $"World size {level.worldSize.x}x{level.worldSize.y} is not 16:9; the camera will letterbox.");

            // Regular pool
            bool anyFromWaveOne = false;
            if (level.enemyPool != null)
            {
                foreach (var e in level.enemyPool)
                {
                    if (e == null) continue; // reported by LayoutValidator as pool.enemy-null
                    if (e.isBoss) Warn("pool.boss-in-enemy-pool", $"'{e.id}' is a boss but sits in the regular enemy pool.");
                    if (e.unlockWave <= 1) anyFromWaveOne = true;
                    if (level.wavesToWin > 0 && e.unlockWave > level.wavesToWin)
                        Warn("pool.never-unlocks", $"'{e.id}' unlocks at wave {e.unlockWave} but the level ends at wave {level.wavesToWin}.");
                    CheckEnemy(e, requirePrefabs, Err, new HashSet<EnemyData>());
                }
                if (level.enemyPool.Count > 0 && !anyFromWaveOne)
                    Warn("pool.nothing-at-wave-1", "No enemy in the pool unlocks at wave 1: WaveManager falls back to enemyPool[0].");
            }

            // Boss pool
            if (level.bossPool != null)
            {
                foreach (var b in level.bossPool)
                {
                    if (b == null) continue;
                    if (!b.isBoss) Err("pool.not-a-boss", $"'{b.id}' is in the boss pool but isBoss is false.");
                    if (b.bossHealthMultiplier <= 0f) Err("boss.health", $"Boss '{b.id}' has bossHealthMultiplier {b.bossHealthMultiplier}.");
                    CheckEnemy(b, requirePrefabs, Err, new HashSet<EnemyData>());
                }
            }

            return issues;
        }

        /// <summary>Validate several levels, plus duplicate-id detection.</summary>
        public static List<LevelIssue> ValidateAll(IEnumerable<LevelData> levels, LayoutRules rules = null, bool requirePrefabs = false)
        {
            var all = new List<LevelIssue>();
            var seen = new HashSet<string>();
            foreach (var level in levels)
            {
                all.AddRange(Validate(level, rules, requirePrefabs));
                if (level != null && !string.IsNullOrEmpty(level.id) && !seen.Add(level.id))
                    all.Add(new LevelIssue(IssueSeverity.Error, level.id, "id.duplicate", $"Level id '{level.id}' is used more than once."));
            }
            return all;
        }

        /// <summary>Log every issue (errors as LogError). Returns true when there are no errors.</summary>
        public static bool Log(List<LevelIssue> issues, string context = "Level validation")
        {
            int errors = 0, warnings = 0;
            foreach (var i in issues)
            {
                if (i.IsError) { errors++; Debug.LogError($"{context}: {i}"); }
                else { warnings++; Debug.LogWarning($"{context}: {i}"); }
            }
            if (errors == 0 && warnings == 0) Debug.Log($"{context}: all levels valid.");
            else Debug.Log($"{context}: {errors} error(s), {warnings} warning(s).");
            return errors == 0;
        }

        /// <summary>One line per issue, for dialogs and test output.</summary>
        public static string Summarize(List<LevelIssue> issues, int maxLines = 30)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var i in issues)
            {
                if (n++ >= maxLines) { sb.AppendLine($"... and {issues.Count - maxLines} more (see Console)."); break; }
                sb.AppendLine(i.ToString());
            }
            return sb.ToString();
        }

        // Enemy-level checks, recursing into summoned/split children (cycle-safe).
        private static void CheckEnemy(EnemyData e, bool requirePrefabs, System.Action<string, string> err, HashSet<EnemyData> visited)
        {
            if (e == null || !visited.Add(e)) return;
            if (string.IsNullOrEmpty(e.id)) err("enemy.id", $"EnemyData '{e.name}' has no id.");
            if (e.baseHealth <= 0f) err("enemy.health", $"'{e.id}' baseHealth {e.baseHealth} must be > 0.");
            if (e.baseSpeed <= 0f) err("enemy.speed", $"'{e.id}' baseSpeed {e.baseSpeed} must be > 0.");
            if (requirePrefabs && e.prefab == null) err("enemy.prefab", $"'{e.id}' has no prefab/runtime template: WaveManager cannot spawn it.");
            if (e.abilities == null) return;

            for (int i = 0; i < e.abilities.Count; i++)
            {
                var a = e.abilities[i];
                if (a == null) { err("ability.null", $"'{e.id}' ability slot {i} is empty."); continue; }
                if (a is SummonAbility s)
                {
                    if (s.minion == null) err("ability.summon-minion", $"'{e.id}' {a.abilityName}: no minion assigned.");
                    else if (s.minion.isBoss) err("ability.summon-boss", $"'{e.id}' {a.abilityName}: summons a boss.");
                    else CheckEnemy(s.minion, requirePrefabs, err, visited);
                }
                else if (a is SplitOnDeathAbility split)
                {
                    if (split.child == null) err("ability.split-child", $"'{e.id}' {a.abilityName}: no child assigned.");
                    else if (split.child == e) err("ability.split-self", $"'{e.id}' {a.abilityName}: splits into itself (infinite).");
                    else CheckEnemy(split.child, requirePrefabs, err, visited);
                }
            }
        }
    }
}
