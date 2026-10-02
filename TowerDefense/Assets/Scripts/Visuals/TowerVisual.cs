using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Presentation for a built tower: turret tracks <see cref="Tower.CurrentTarget"/>,
    /// recoils and flashes when this tower fires, shows level pips, tints its
    /// pennants with the chosen branch colour, grows slightly per level and
    /// pops in when built. Pure presentation: never touches combat state.
    /// </summary>
    public sealed class TowerVisual : MonoBehaviour
    {
        [SerializeField] private Tower tower;
        [SerializeField] private SortingGroup group;
        [SerializeField] private Transform body;          // scaled for pop-in and level growth
        [SerializeField] private Transform turret;        // rotates toward the target
        [SerializeField] private Transform turretSprite;  // slides back on recoil
        [SerializeField] private SpriteRenderer muzzle;
        [SerializeField] private SpriteRenderer accent;
        [SerializeField] private SpriteRenderer[] pips;
        [SerializeField] private bool rotateTurret = true;
        [SerializeField] private float turnSpeed = 540f;
        [SerializeField] private float recoilDistance = 0.12f;
        [SerializeField] private float idleAngle = 90f;

        private const float PopDuration = 0.35f;
        private const float RecoilDuration = 0.16f;
        private const float MuzzleDuration = 0.08f;
        private const float PulseDuration = 0.4f;

        private float popTimer;
        private float recoilTimer;
        private float muzzleTimer;
        private float pulseTimer;
        private float angle;
        private int shownLevel = -1;
        private Tower.UpgradePath shownPath = (Tower.UpgradePath)(-1);
        private Vector3 turretRestPosition;

        /// <summary>Wire references (template builder only).</summary>
        internal void Setup(Tower owner, SortingGroup sortingGroup, Transform bodyRoot, Transform turretPivot, Transform turretVisual,
                            SpriteRenderer muzzleFlash, SpriteRenderer accentRenderer, SpriteRenderer[] levelPips, bool rotates)
        {
            tower = owner;
            group = sortingGroup;
            body = bodyRoot;
            turret = turretPivot;
            turretSprite = turretVisual;
            muzzle = muzzleFlash;
            accent = accentRenderer;
            pips = levelPips;
            rotateTurret = rotates;
        }

        private void OnEnable()
        {
            Tower.AnyFired += OnAnyFired;
            if (tower != null) tower.Upgraded += OnUpgraded;
            popTimer = 0f;
            recoilTimer = 0f;
            muzzleTimer = 0f;
            pulseTimer = 0f;
            angle = idleAngle;
            shownLevel = -1;
            shownPath = (Tower.UpgradePath)(-1);
            if (turretSprite != null) turretRestPosition = turretSprite.localPosition;
            if (muzzle != null) muzzle.enabled = false;
            if (body != null) body.localScale = new Vector3(0.3f, 0.3f, 1f);
            UpdateSorting();
        }

        private void OnDisable()
        {
            Tower.AnyFired -= OnAnyFired;
            if (tower != null) tower.Upgraded -= OnUpgraded;
        }

        private void Start()
        {
            // Position is final once the slot has parented us; Init has run by now.
            UpdateSorting();
            RefreshLevel();
        }

        private void OnAnyFired(Tower t)
        {
            if (t != tower) return;
            recoilTimer = RecoilDuration;
            muzzleTimer = MuzzleDuration;
            if (rotateTurret && t.CurrentTarget != null) angle = AngleTo(t.CurrentTarget.position);
        }

        private void OnUpgraded(Tower t)
        {
            pulseTimer = PulseDuration;
            RefreshLevel();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            // Build/upgrade feedback is UI-like: keep it moving even if the game is paused.
            float udt = Time.unscaledDeltaTime;

            if (tower != null && (tower.upgradeLevel != shownLevel || tower.chosenPath != shownPath)) RefreshLevel();

            // Pop-in with overshoot, then a gentle pulse on upgrade.
            if (body != null)
            {
                float levelScale = 1f + 0.06f * Mathf.Max(0, (tower != null ? tower.upgradeLevel : 1) - 1);
                float s = levelScale;
                if (popTimer < PopDuration)
                {
                    popTimer += udt;
                    s *= EaseOutBack(Mathf.Clamp01(popTimer / PopDuration));
                }
                else if (pulseTimer > 0f)
                {
                    pulseTimer -= udt;
                    s *= 1f + 0.12f * Mathf.Sin(Mathf.Clamp01(1f - pulseTimer / PulseDuration) * Mathf.PI);
                }
                Vector3 cur = body.localScale;
                if (!Mathf.Approximately(cur.x, s)) body.localScale = new Vector3(s, s, 1f);
            }

            if (turret != null && rotateTurret)
            {
                Transform target = tower != null ? tower.CurrentTarget : null;
                if (target != null) angle = Mathf.MoveTowardsAngle(angle, AngleTo(target.position), turnSpeed * dt);
                turret.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (turretSprite != null)
            {
                float k = recoilTimer > 0f ? recoilTimer / RecoilDuration : 0f;
                if (recoilTimer > 0f) recoilTimer -= dt;
                turretSprite.localPosition = turretRestPosition + new Vector3(-recoilDistance * k * k, 0f, 0f);
            }

            if (muzzle != null)
            {
                bool on = muzzleTimer > 0f;
                if (on) muzzleTimer -= dt;
                VisualBuilder.SetVisible(muzzle, on);
                if (on) VisualBuilder.SetAlpha(muzzle, Mathf.Clamp01(muzzleTimer / MuzzleDuration));
            }
        }

        private float AngleTo(Vector3 worldTarget)
        {
            Vector3 d = worldTarget - turret.position;
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        private void RefreshLevel()
        {
            if (tower == null) return;
            shownLevel = tower.upgradeLevel;
            shownPath = tower.chosenPath;
            if (pips != null)
                for (int i = 0; i < pips.Length; i++)
                    if (pips[i] != null) pips[i].enabled = i < shownLevel;
            if (accent != null)
            {
                bool branched = shownPath != Tower.UpgradePath.None;
                accent.enabled = branched || shownLevel >= 2;
                accent.color = Palette.BranchColor(shownPath, new Color(0.95f, 0.92f, 0.85f, 1f));
            }
        }

        private void UpdateSorting()
        {
            if (group != null) group.sortingOrder = SortingOrders.ForY(SortingOrders.Towers, transform.position.y);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
