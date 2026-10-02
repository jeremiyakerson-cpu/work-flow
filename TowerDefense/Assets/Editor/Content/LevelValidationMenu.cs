using System.Collections.Generic;
using TowerDefense.Content;
using TowerDefense.Levels;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.ContentEditor
{
    /// <summary>
    /// "Tower Defense/Content/Validate Levels": runs LevelValidator over the
    /// code-built campaign + endless levels and over every LevelData asset in
    /// the project (baked or hand-made). Issues go to the Console; a dialog
    /// summarises them.
    /// </summary>
    public static class LevelValidationMenu
    {
        [MenuItem("Tower Defense/Content/Validate Levels")]
        private static void ValidateMenu()
        {
            var issues = ValidateEverything();
            int errors = 0;
            foreach (var i in issues) if (i.IsError) errors++;

            string title = errors == 0 ? "Levels valid" : "Level problems found";
            string body = issues.Count == 0
                ? "All levels pass every rule."
                : $"{errors} error(s), {issues.Count - errors} warning(s):\n\n{LevelValidator.Summarize(issues, 20)}";
            EditorUtility.DisplayDialog(title, body, "OK");
        }

        /// <summary>Validate built-in and project levels; logs everything. Usable from batch mode.</summary>
        public static List<LevelIssue> ValidateEverything()
        {
            var issues = new List<LevelIssue>();

            // 1. Built-in content (what the game runs with zero assets).
            var builtIn = DefaultContent.Build(runtime: false);
            var builtInIssues = builtIn.ValidateLevels();
            LevelValidator.Log(builtInIssues, "Built-in levels");
            issues.AddRange(builtInIssues);
            DestroyCatalog(builtIn);

            // 2. LevelData assets in the project (baked copies, designer-made maps).
            var assets = new List<LevelData>();
            string[] guids = AssetDatabase.FindAssets("t:LevelData");
            if (guids != null)
            {
                foreach (var guid in guids)
                {
                    var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (level != null) assets.Add(level);
                }
            }
            if (assets.Count > 0)
            {
                var assetIssues = LevelValidator.ValidateAll(assets);
                LevelValidator.Log(assetIssues, $"LevelData assets ({assets.Count})");
                issues.AddRange(assetIssues);
            }

            return issues;
        }

        // The validation catalog is throwaway: free its ScriptableObjects right away.
        private static void DestroyCatalog(ContentCatalog catalog)
        {
            var objects = new List<Object>();
            objects.AddRange(catalog.Towers);
            foreach (var e in catalog.AllEnemies)
            {
                if (e.abilities != null)
                    foreach (var a in e.abilities) if (a != null) objects.Add(a);
                objects.Add(e);
            }
            foreach (var l in catalog.AllLevels()) objects.Add(l);
            foreach (var o in objects) Object.DestroyImmediate(o);
        }
    }
}
