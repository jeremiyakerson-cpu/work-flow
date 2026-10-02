using TowerDefense.Input;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// Bottom-left hero portrait (tap = select hero for a move order) with a health
    /// bar and respawn clock, plus the hero's ability button with a radial cooldown.
    /// Polled by GameHud each frame; writes to uGUI only when values change.
    /// </summary>
    public sealed class HeroWidget
    {
        private const float PortraitSize = 150f;
        private const float AbilitySize = 124f;

        public RectTransform Root { get; }

        private readonly UIButtonView portrait;
        private readonly Text initial;
        private readonly RadialCooldown respawnShade;
        private readonly Image selectedRing;
        private readonly ProgressBar health;
        private readonly UIButtonView ability;
        private readonly RadialCooldown abilityShade;
        private readonly Text abilityLabel;

        private HeroUnit hero;
        private TouchInputController input;
        private float lastHealth = -1f;
        private bool wasReady = true;
        private bool wasDead;

        public HeroWidget(RectTransform parent)
        {
            Root = UIFactory.Rect("Hero", parent);
            UIFactory.Place(Root, Vector2.zero, Vector2.zero, new Vector2(UITheme.Margin, UITheme.Margin),
                            new Vector2(PortraitSize + UITheme.Spacing + AbilitySize, PortraitSize + 40f));

            portrait = UIFactory.IconButton(Root, null, OnPortraitTapped, new Color(0.30f, 0.38f, 0.55f, 1f), PortraitSize);
            UIFactory.Place(portrait.Rect, Vector2.zero, Vector2.zero, new Vector2(0f, 34f), new Vector2(PortraitSize, PortraitSize));
            initial = UIFactory.FitLabel(portrait.Face.transform, "H", UITheme.FontTitle - 20, UITheme.Text, 20f);
            initial.fontStyle = FontStyle.Bold;
            respawnShade = UIFactory.RadialShade(portrait.Face.transform, UITheme.CooldownShade);

            selectedRing = UIFactory.Image(portrait.transform, "Selected", UISprites.Ring(0.12f), UITheme.Highlight);
            UIFactory.Stretch(selectedRing.rectTransform, -12f);
            selectedRing.gameObject.SetActive(false);

            health = UIFactory.ProgressBar(Root, new Vector2(PortraitSize, 26f), UITheme.HealthGood, UITheme.Shade(UITheme.Panel, 0.5f));
            UIFactory.Place(health.transform as RectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(PortraitSize, 26f));

            ability = UIFactory.IconButton(Root, UISprites.Star, OnAbilityTapped, new Color(0.70f, 0.45f, 0.15f, 1f), AbilitySize, true, 0.55f);
            UIFactory.Place(ability.Rect, Vector2.zero, Vector2.zero, new Vector2(PortraitSize + UITheme.Spacing, 50f),
                            new Vector2(AbilitySize, AbilitySize));
            abilityShade = UIFactory.RadialShade(ability.Face.transform, UITheme.CooldownShade);
            abilityLabel = UIFactory.Label(ability.transform, "", UITheme.FontSmall - 6, UITheme.TextDim);
            UIFactory.Place(abilityLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f),
                            new Vector2(AbilitySize + 60f, 34f));
            abilityLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        public void Bind(HeroUnit newHero, TouchInputController controller)
        {
            if (hero != null)
            {
                hero.Died -= OnDied;
                hero.AbilityUsed -= OnAbilityUsed;
            }
            hero = newHero;
            input = controller;
            Root.gameObject.SetActive(hero != null);
            if (hero == null) return;

            hero.Died += OnDied;
            hero.AbilityUsed += OnAbilityUsed;
            string n = string.IsNullOrEmpty(hero.heroName) ? "H" : hero.heroName.Substring(0, 1).ToUpperInvariant();
            initial.text = n;
            abilityLabel.text = hero.abilityName;
            lastHealth = -1f;
            wasDead = hero.IsDead;
            wasReady = hero.IsAbilityReady();
            SetSelected(false);
        }

        public void Unbind() => Bind(null, input);

        public void SetSelected(bool selected)
        {
            if (selectedRing.gameObject.activeSelf == selected) return;
            selectedRing.gameObject.SetActive(selected);
            if (selected) UITween.Scale(selectedRing.transform, Vector3.one * 1.25f, Vector3.one, 0.2f, Ease.OutBack);
        }

        public void Tick()
        {
            if (hero == null) return;

            bool dead = hero.IsDead;
            if (dead)
            {
                respawnShade.SetRemaining(1f - hero.RespawnPercent);
                abilityShade.SetRemaining(1f);
                SetHealth(0f);
            }
            else
            {
                respawnShade.SetRemaining(0f);
                abilityShade.SetReadyPercent(hero.AbilityReadyPercent);
                SetHealth(hero.HealthPercent());
            }

            if (wasDead && !dead) UITween.Punch(portrait.transform, 0.2f, 0.35f);
            wasDead = dead;

            bool ready = !dead && hero.IsAbilityReady();
            if (ready && !wasReady) UITween.Punch(ability.transform, 0.22f, 0.35f);
            wasReady = ready;
        }

        private void SetHealth(float v)
        {
            if (Mathf.Abs(v - lastHealth) < 0.004f) return;
            lastHealth = v;
            health.SetValue(v);
            health.SetColor(v > 0.5f ? UITheme.HealthGood : Color.Lerp(UITheme.Health, UITheme.HealthGood, v * 2f));
        }

        private void OnPortraitTapped()
        {
            if (hero == null) return;
            if (hero.IsDead)
            {
                if (UIRoot.Instance != null) UIRoot.Instance.ShowToast(hero.heroName + " is recovering");
                return;
            }
            if (input != null) input.ToggleHeroSelection();
        }

        private void OnAbilityTapped()
        {
            if (hero == null) return;
            if (hero.IsAbilityReady()) hero.UseAbility();
            else UITween.Punch(ability.transform, 0.08f, 0.2f);
        }

        private void OnAbilityUsed(HeroUnit h) => UITween.Punch(ability.transform, 0.15f, 0.25f);

        private void OnDied(HeroUnit h)
        {
            SetSelected(false);
            if (UIRoot.Instance != null) UIRoot.Instance.ShowToast(h.heroName + " has fallen. Respawning...");
        }
    }
}
