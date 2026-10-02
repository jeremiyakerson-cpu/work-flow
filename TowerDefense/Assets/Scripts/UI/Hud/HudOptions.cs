using System;
using System.Collections.Generic;
using TowerDefense.Input;
using UnityEngine;

namespace TowerDefense.UI
{
    /// <summary>Per-level HUD configuration passed to <see cref="GameHud.Bind"/>.</summary>
    public sealed class HudOptions
    {
        /// <summary>Input controller (defaults to TouchInputController.Instance).</summary>
        public TouchInputController input;
        /// <summary>Spell buttons, left to right (e.g. SpellDefinition.Barricade(template)).</summary>
        public IReadOnlyList<SpellDefinition> spells;
        /// <summary>Persistence-backed settings for the pause menu's Settings button (optional).</summary>
        public IUiSettings settings;
        /// <summary>Pause menu "Restart" (null hides the button). Called after the player confirms.</summary>
        public Action onRestart;
        /// <summary>Pause menu "Quit to menu" (null hides the button). Called after the player confirms.</summary>
        public Action onQuitToMenu;
        /// <summary>Show the first-level hint overlay.</summary>
        public bool showTutorial;
        /// <summary>Called when the tutorial finishes or is skipped (persist the flag).</summary>
        public Action onTutorialFinished;
        /// <summary>Put the next-wave button at the first path's spawn point (KR style) instead of the corner.</summary>
        public bool nextWaveAtSpawn = true;
        /// <summary>Raised after the build menu builds a tower (audio/VFX hook).</summary>
        public Action<TowerPlacement, TowerData> onTowerBuilt;
        /// <summary>Raised after a tower is upgraded, branched or sold through the menu.</summary>
        public Action<TowerPlacement> onTowerChanged;
        /// <summary>Raised when the player calls a wave early, with the bonus gold.</summary>
        public Action<int> onWaveCalledEarly;
    }

    /// <summary>
    /// A cooldown spell placed on the path (KR-style barricade, reinforcements...).
    /// Either instantiates <see cref="template"/> at the chosen path point or runs
    /// <see cref="cast"/>. Cooldowns tick in game time (pause / fast-forward aware).
    /// </summary>
    public sealed class SpellDefinition
    {
        public string id = "spell";
        public string displayName = "Spell";
        /// <summary>One or two characters drawn on the button.</summary>
        public string glyph = "S";
        public Color color = new Color(0.55f, 0.40f, 0.25f, 1f);
        public float cooldown = 20f;
        /// <summary>Cooldown when the level starts (0 = ready immediately).</summary>
        public float initialCooldown = 0f;
        /// <summary>World units from a path a tap may land (0 = controller default).</summary>
        public float pathTolerance = 0f;
        /// <summary>Runtime template instantiated at the target point (see ARCHITECTURE "Runtime templates").</summary>
        public GameObject template;
        /// <summary>Custom cast instead of (or in addition to) the template.</summary>
        public Action<Vector3> cast;
        /// <summary>Hint shown while choosing a target.</summary>
        public string targetingHint = "Tap the road";

        /// <summary>Classic barricade: drop <paramref name="barricadeTemplate"/> on the road.</summary>
        public static SpellDefinition Barricade(GameObject barricadeTemplate, float cooldownSeconds = 18f)
        {
            return new SpellDefinition
            {
                id = "barricade",
                displayName = "Barricade",
                glyph = "B",
                color = new Color(0.58f, 0.42f, 0.24f, 1f),
                cooldown = cooldownSeconds,
                template = barricadeTemplate,
                targetingHint = "Tap the road to drop a barricade",
            };
        }
    }
}
