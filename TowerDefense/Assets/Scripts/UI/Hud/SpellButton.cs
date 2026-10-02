using System;
using TowerDefense.Input;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// One spell button: radial cooldown (game time), tap to enter path targeting
    /// on the input controller, tap again to cancel. On a valid target it spawns
    /// the definition's template and/or runs its cast callback.
    /// </summary>
    public sealed class SpellButton
    {
        public const float Size = 124f;

        public SpellDefinition Definition { get; }
        public UIButtonView View { get; }

        /// <summary>Raised after the spell is cast at a world position.</summary>
        public event Action<SpellButton, Vector3> Cast;
        /// <summary>Raised just before the button asks the input controller for a target.</summary>
        public event Action<SpellButton> TargetingStarted;

        private readonly RadialCooldown shade;
        private readonly Image activeRing;
        private readonly Action<Vector3> onConfirm;
        private readonly Action onCancel;
        private TouchInputController input;
        private float remaining;
        private bool targeting;
        private bool wasReady;

        public bool IsReady => remaining <= 0f;

        public SpellButton(RectTransform parent, SpellDefinition def, TouchInputController controller)
        {
            Definition = def;
            input = controller;
            View = UIFactory.IconButton(parent, null, OnTapped, def.color, Size);
            View.Label = UIFactory.FitLabel(View.Face.transform, def.glyph, UITheme.FontLarge, UITheme.Text, 18f);
            View.Label.fontStyle = FontStyle.Bold;
            shade = UIFactory.RadialShade(View.Face.transform, UITheme.CooldownShade);

            activeRing = UIFactory.Image(View.transform, "Active", UISprites.Ring(0.12f), UITheme.Highlight);
            UIFactory.Stretch(activeRing.rectTransform, -12f);
            activeRing.gameObject.SetActive(false);

            Text name = UIFactory.Label(View.transform, def.displayName, UITheme.FontSmall - 6, UITheme.TextDim);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(Size + 60f, 34f));
            name.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Delegates cached once so targeting doesn't allocate per use.
            onConfirm = OnTargetConfirmed;
            onCancel = OnTargetCancelled;

            remaining = Mathf.Max(0f, def.initialCooldown);
            wasReady = IsReady;
            shade.SetRemaining(def.cooldown > 0f ? remaining / def.cooldown : 0f);
        }

        public void SetInput(TouchInputController controller) => input = controller;

        /// <summary>Advance the cooldown by game time (pass Time.deltaTime).</summary>
        public void Tick(float dt)
        {
            if (remaining > 0f)
            {
                remaining = Mathf.Max(0f, remaining - dt);
                shade.SetRemaining(Definition.cooldown > 0f ? remaining / Definition.cooldown : 0f);
            }
            bool ready = IsReady;
            if (ready && !wasReady) UITween.Punch(View.transform, 0.22f, 0.35f);
            wasReady = ready;
        }

        /// <summary>Reset to the initial cooldown (level restart).</summary>
        public void ResetCooldown()
        {
            remaining = Mathf.Max(0f, Definition.initialCooldown);
            shade.SetRemaining(Definition.cooldown > 0f ? remaining / Definition.cooldown : 0f);
        }

        public void CancelTargeting()
        {
            if (targeting && input != null) input.CancelTargeting();
            SetTargeting(false);
        }

        private void OnTapped()
        {
            if (input == null) return;
            if (targeting) { input.CancelTargeting(); return; }
            if (!IsReady)
            {
                UITween.Punch(View.transform, 0.08f, 0.2f);
                return;
            }
            TargetingStarted?.Invoke(this);
            SetTargeting(true);
            input.BeginTargeting(Definition.pathTolerance, onConfirm, onCancel);
        }

        private void OnTargetConfirmed(Vector3 position)
        {
            SetTargeting(false);
            if (Definition.template != null)
                UnityEngine.Object.Instantiate(Definition.template, position, Quaternion.identity);
            Definition.cast?.Invoke(position);
            remaining = Definition.cooldown;
            shade.SetRemaining(1f);
            wasReady = false;
            Cast?.Invoke(this, position);
        }

        private void OnTargetCancelled() => SetTargeting(false);

        private void SetTargeting(bool on)
        {
            targeting = on;
            if (activeRing.gameObject.activeSelf != on) activeRing.gameObject.SetActive(on);
        }
    }
}
