using UnityEngine;

namespace TowerDefense.Bootstrap
{
    /// <summary>
    /// SANDBOX ONLY. Plays the sandbox by itself so every visual gets exercised
    /// without input: builds towers on empty slots, upgrades and branches them,
    /// walks the hero between two posts, fires the hero ability when enemies
    /// are near, and restarts a few seconds after game over.
    /// </summary>
    public sealed class SandboxAutoPilot : MonoBehaviour
    {
        [SerializeField] private float decisionInterval = 1.6f;
        [SerializeField] private float heroPatrolInterval = 14f;
        [SerializeField] private float restartDelay = 3f;

        private SandboxBootstrap sandbox;
        private float decisionTimer;
        private float patrolTimer;
        private float gameOverTimer;
        private int nextTower;
        private bool heroAtStart = true;

        internal void Setup(SandboxBootstrap owner) => sandbox = owner;

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (sandbox == null || gm == null) return;

            if (gm.IsGameOver || gm.IsVictory)
            {
                gameOverTimer += Time.unscaledDeltaTime;
                if (gameOverTimer >= restartDelay) SandboxBootstrap.Restart();
                return;
            }

            float dt = Time.deltaTime;
            decisionTimer += dt;
            patrolTimer += dt;

            HeroUnit hero = sandbox.Hero;
            if (hero != null && !hero.IsDead && patrolTimer >= heroPatrolInterval)
            {
                patrolTimer = 0f;
                heroAtStart = !heroAtStart;
                Vector2 start = sandbox.Content.Level.heroStart;
                hero.MoveTo(heroAtStart ? new Vector3(start.x, start.y, 0f) : new Vector3(15f, 10.5f, 0f));
            }

            if (decisionTimer < decisionInterval) return;
            decisionTimer = 0f;

            WaveManager waves = WaveManager.Instance;
            if (hero != null && hero.IsAbilityReady() && waves != null && waves.EnemiesAlive >= 3) hero.UseAbility();
            if (!TryBuild(gm)) TryUpgrade(gm);
        }

        private bool TryBuild(GameManager gm)
        {
            var towers = sandbox.Content.Towers;
            TowerData pick = towers[nextTower % towers.Count];
            if (!gm.CanAfford(pick.baseCost)) return false;
            foreach (var slot in sandbox.Slots)
            {
                if (slot == null || slot.IsOccupied) continue;
                if (slot.TryBuild(pick)) { nextTower++; return true; }
            }
            return false;
        }

        private void TryUpgrade(GameManager gm)
        {
            int i = 0;
            foreach (var slot in sandbox.Slots)
            {
                i++;
                Tower t = slot != null ? slot.GetBuiltTower() : null;
                if (t == null || !t.CanUpgrade()) continue;
                switch (t.upgradeLevel)
                {
                    case 1:
                        if (gm.CanAfford(t.NextUpgradeCost()) && t.Upgrade()) return;
                        break;
                    case 2:
                        var path = (i & 1) == 0 ? Tower.UpgradePath.PathA : Tower.UpgradePath.PathB;
                        if (gm.CanAfford(t.BranchCost(path)) && t.ChooseBranch(path)) return;
                        break;
                    case 3:
                        if (gm.CanAfford(t.NextUpgradeCost()) && t.UpgradeFinal()) return;
                        break;
                }
            }
        }
    }
}
