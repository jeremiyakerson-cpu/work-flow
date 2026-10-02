using System.Collections.Generic;
using TowerDefense.Content;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.ContentEditor
{
    /// <summary>
    /// Writes the code-built default content (DefaultContent) as .asset files
    /// under Assets/Content/Generated so designers can tune it in the
    /// Inspector. Abilities are saved as sub-assets of their enemy. The game
    /// does not need these files; load them via ContentCatalog.FromAsset.
    /// Re-baking replaces the generated files (tweaks there are overwritten).
    /// </summary>
    public static class ContentBaker
    {
        public const string Root = "Assets/Content/Generated";

        [MenuItem("Tower Defense/Content/Bake Default Content to Assets")]
        private static void BakeMenu()
        {
            if (AssetDatabase.IsValidFolder(Root) &&
                !EditorUtility.DisplayDialog("Bake Default Content",
                    $"Regenerate every asset in {Root}?\nChanges made to those assets will be lost.", "Bake", "Cancel"))
                return;

            var catalog = Bake();
            EditorUtility.DisplayDialog("Bake Default Content",
                $"Wrote {catalog.towers.Count} towers, {catalog.enemies.Count + catalog.minions.Count} enemies, " +
                $"{catalog.bosses.Count} bosses and {catalog.campaignLevels.Count + catalog.endlessLevels.Count} levels to {Root}.", "OK");
            Selection.activeObject = catalog;
        }

        /// <summary>Non-interactive bake (CI / batch mode). Returns the saved catalog asset.</summary>
        public static ContentCatalogAsset Bake()
        {
            var content = DefaultContent.Build(runtime: false);

            EnsureFolder(Root);
            string towersDir = EnsureFolder(Root + "/Towers");
            string enemiesDir = EnsureFolder(Root + "/Enemies");
            string minionsDir = EnsureFolder(Root + "/Minions");
            string bossesDir = EnsureFolder(Root + "/Bosses");
            string levelsDir = EnsureFolder(Root + "/Levels");
            string endlessDir = EnsureFolder(Root + "/Levels/Endless");

            var catalog = ScriptableObject.CreateInstance<ContentCatalogAsset>();
            catalog.name = "ContentCatalog";

            // Enemies first: levels and abilities reference them. Cross-references
            // between the new objects resolve once everything is persistent (SaveAssets).
            foreach (var e in content.Minions) catalog.minions.Add(SaveEnemy(e, minionsDir));
            foreach (var e in content.Enemies) catalog.enemies.Add(SaveEnemy(e, enemiesDir));
            foreach (var e in content.Bosses) catalog.bosses.Add(SaveEnemy(e, bossesDir));
            foreach (var t in content.Towers) catalog.towers.Add(Save(t, $"{towersDir}/{t.id}.asset"));
            foreach (var l in content.CampaignLevels) catalog.campaignLevels.Add(Save(l, $"{levelsDir}/{l.id}.asset"));
            foreach (var l in content.EndlessLevels) catalog.endlessLevels.Add(Save(l, $"{endlessDir}/{l.id}.asset"));
            Save(catalog, $"{Root}/ContentCatalog.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Content baked to {Root}.");
            return catalog;
        }

        private static EnemyData SaveEnemy(EnemyData enemy, string dir)
        {
            Save(enemy, $"{dir}/{enemy.id}.asset");
            if (enemy.abilities != null)
            {
                foreach (var ability in enemy.abilities)
                {
                    if (ability == null) continue;
                    ability.hideFlags = HideFlags.None;
                    AssetDatabase.AddObjectToAsset(ability, enemy);
                    EditorUtility.SetDirty(ability);
                }
            }
            EditorUtility.SetDirty(enemy);
            return enemy;
        }

        private static T Save<T>(T obj, string path) where T : Object
        {
            obj.hideFlags = HideFlags.None;
            // Replace, don't merge: sub-assets (abilities) of a stale file would otherwise linger.
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
            EditorUtility.SetDirty(obj);
            return obj;
        }

        /// <summary>Create every missing folder along an "Assets/..." path.</summary>
        public static string EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return path;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            return path;
        }
    }
}
