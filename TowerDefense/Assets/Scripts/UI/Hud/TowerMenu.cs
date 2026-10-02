using System;
using System.Collections.Generic;
using TowerDefense.Input;
using TowerDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// Kingdom Rush style radial menu around a tapped build slot. Empty slot: a
    /// ring of tower buttons (cost tags, greyed when unaffordable). Built slot:
    /// upgrade / branch / final buttons, level pips, sell and a target-priority
    /// cycle. First tap selects and explains (tooltip + range preview), a second
    /// tap on the same button confirms. Follows the slot on screen, stays inside
    /// the safe area and closes on pan/zoom, empty taps, hero selection, pause.
    /// </summary>
    public sealed class TowerMenu : MonoBehaviour
    {
        private const float RingRadius = 178f;
        private const float ItemSize = 128f;
        private const float TooltipWidth = 540f;

        /// <summary>A tower was built from this menu.</summary>
        public event Action<TowerPlacement, TowerData> TowerBuilt;
        /// <summary>A tower was upgraded, branched, re-targeted or sold from this menu.</summary>
        public event Action<TowerPlacement> TowerChanged;
        public event Action<bool> OpenChanged;

        public bool IsOpen { get; private set; }
        public TowerPlacement CurrentSlot => slot;

        private enum ItemKind { Build, Upgrade, Branch, Final, Max, Sell, Priority }

        private sealed class Item
        {
            public ItemKind kind;
            public UIButtonView view;
            public Text costLabel;
            public Image checkBadge;
            public int cost;
            public string title;
            public string body;
            public float previewRange;
            public Action execute;
        }

        private GameManager gm;
        private IReadOnlyList<TowerData> towers;
        private TouchInputController input;
        private RectTransform layer;
        private RectTransform root;
        private RectTransform itemsRoot;
        private CanvasGroup group;
        private RectTransform tooltip;
        private Text tooltipTitle;
        private Text tooltipBody;
        private RectTransform pipsRoot;
        private readonly Image[] pips = new Image[4];
        private WorldIndicator rangeRing;

        private readonly List<Item> items = new List<Item>(8);
        private TowerPlacement slot;
        private Tower tower;
        private int selected = -1;
        private bool bound;

        // ------------------------------------------------------------------ lifecycle

        public static TowerMenu Create(RectTransform layer)
        {
            RectTransform rt = UIFactory.Rect("TowerMenu", layer);
            UIFactory.Center(rt, Vector2.zero, new Vector2(2f * RingRadius + ItemSize, 2f * RingRadius + ItemSize));
            var menu = rt.gameObject.AddComponent<TowerMenu>();
            menu.layer = layer;
            menu.root = rt;
            menu.BuildStatic();
            rt.gameObject.SetActive(false);
            return menu;
        }

        public void Bind(GameManager gameManager, IReadOnlyList<TowerData> towerList, TouchInputController controller)
        {
            Unbind();
            gm = gameManager;
            towers = towerList;
            input = controller;

            TowerPlacement.SlotTapped += OnSlotTapped;
            TowerPlacement.AnySlotChanged += OnAnySlotChanged;
            if (gm != null)
            {
                gm.GoldChanged += OnGoldChanged;
                gm.PausedChanged += OnPausedChanged;
                gm.GameOverTriggered += Close;
                gm.VictoryTriggered += Close;
            }
            if (input != null)
            {
                input.EmptyTapped += OnEmptyTapped;
                input.CameraGestureStarted += Close;
                input.HeroSelectionChanged += OnModeChanged;
                input.TargetingChanged += OnModeChanged;
            }
            bound = true;
        }

        public void Unbind()
        {
            if (!bound) return;
            bound = false;
            TowerPlacement.SlotTapped -= OnSlotTapped;
            TowerPlacement.AnySlotChanged -= OnAnySlotChanged;
            if (gm != null)
            {
                gm.GoldChanged -= OnGoldChanged;
                gm.PausedChanged -= OnPausedChanged;
                gm.GameOverTriggered -= Close;
                gm.VictoryTriggered -= Close;
            }
            if (input != null)
            {
                input.EmptyTapped -= OnEmptyTapped;
                input.CameraGestureStarted -= Close;
                input.HeroSelectionChanged -= OnModeChanged;
                input.TargetingChanged -= OnModeChanged;
            }
            CloseImmediate();
            gm = null;
            input = null;
            towers = null;
        }

        private void OnDestroy()
        {
            Unbind();
            if (rangeRing != null) Destroy(rangeRing.gameObject);
        }

        private void BuildStatic()
        {
            group = UIFactory.Group(gameObject);

            Image ring = UIFactory.Image(root, "Ring", UISprites.Ring(0.2f), new Color(0f, 0f, 0f, 0.38f));
            UIFactory.Center(ring.rectTransform, Vector2.zero, new Vector2(2f * RingRadius + 48f, 2f * RingRadius + 48f));
            Image ringEdge = UIFactory.Image(root, "RingEdge", UISprites.Ring(0.04f), UITheme.WithAlpha(UITheme.Parchment, 0.5f));
            UIFactory.Center(ringEdge.rectTransform, Vector2.zero, new Vector2(2f * RingRadius + 48f, 2f * RingRadius + 48f));

            pipsRoot = UIFactory.Rect("Pips", root);
            UIFactory.Center(pipsRoot, new Vector2(0f, -RingRadius * 0.36f), new Vector2(4 * 26f + 3 * 10f, 26f));
            for (int i = 0; i < pips.Length; i++)
            {
                Image pip = UIFactory.Image(pipsRoot, "Pip" + i, UISprites.Circle, UITheme.StarOff);
                UIFactory.Place(pip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 36f, 0f), new Vector2(26f, 26f));
                pips[i] = pip;
            }

            itemsRoot = UIFactory.Stretch(UIFactory.Rect("Items", root));

            Image tip = UIFactory.Panel(root, "Tooltip", UITheme.Panel, 24f, false);
            tooltip = tip.rectTransform;
            UIFactory.Outline(tip.transform, UITheme.PanelBorder, 24f, 0f);
            tooltipTitle = UIFactory.Label(tip.transform, "", UITheme.FontBody, UITheme.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Place(tooltipTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(TooltipWidth - 48f, 52f));
            tooltipBody = UIFactory.Label(tip.transform, "", UITheme.FontSmall, UITheme.Text, TextAnchor.UpperLeft);
            UIFactory.Place(tooltipBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -74f), new Vector2(TooltipWidth - 48f, 200f));
            tooltip.gameObject.SetActive(false);

            rangeRing = WorldIndicator.Create("[RangeRing]", UISprites.WorldRing, WorldIndicator.RangeSortingOrder);
        }

        // ------------------------------------------------------------------ event handlers

        private void OnSlotTapped(TowerPlacement tapped)
        {
            if (tapped == null || gm == null || gm.IsPaused || gm.IsGameOver || gm.IsVictory) return;
            if (IsOpen && tapped == slot) { Close(); return; }
            Open(tapped);
        }

        private void OnAnySlotChanged(TowerPlacement changed)
        {
            if (IsOpen && changed == slot) Close();
        }

        private void OnGoldChanged(int gold)
        {
            if (IsOpen) RefreshAffordability();
        }

        private void OnPausedChanged(bool paused)
        {
            if (paused) Close();
        }

        private void OnEmptyTapped(Vector3 world) => Close();

        private void OnModeChanged(bool active)
        {
            if (active) Close();
        }

        // ------------------------------------------------------------------ open / close

        /// <summary>Open the build or upgrade ring for <paramref name="target"/>.</summary>
        public void Open(TowerPlacement target)
        {
            if (target == null) return;
            if (input != null) input.CancelModes();

            slot = target;
            tower = slot.GetBuiltTower();
            ClearItems();
            selected = -1;

            if (!slot.IsOccupied) BuildRing();
            else UpgradeRing();

            RefreshAffordability();
            RefreshPips();
            tooltip.gameObject.SetActive(false);

            if (tower != null) rangeRing.ShowRange(slot.transform.position, tower.Range, UITheme.RangeEdge);
            else rangeRing.Hide();

            bool wasOpen = IsOpen;
            IsOpen = true;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            UpdatePosition();

            UITween.Kill(group);
            group.alpha = 1f;
            group.blocksRaycasts = true;
            UITween.Scale(root, Vector3.one * 0.55f, Vector3.one, 0.26f, Ease.OutBack);
            for (int i = 0; i < items.Count; i++)
                UITween.Scale(items[i].view.transform, Vector3.zero, Vector3.one, 0.24f, Ease.OutBack, 0.03f * i);
            if (!wasOpen) OpenChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            slot = null;
            tower = null;
            selected = -1;
            rangeRing.Hide();
            group.blocksRaycasts = false;
            UITween.Scale(root, root.localScale, Vector3.one * 0.6f, 0.14f, Ease.InQuad);
            UITween.Fade(group, group.alpha, 0f, 0.14f, Ease.InQuad, 0f, () =>
            {
                if (!IsOpen && root != null) root.gameObject.SetActive(false);
            });
            OpenChanged?.Invoke(false);
        }

        private void CloseImmediate()
        {
            bool was = IsOpen;
            IsOpen = false;
            slot = null;
            tower = null;
            selected = -1;
            if (rangeRing != null) rangeRing.Hide();
            if (root != null) root.gameObject.SetActive(false);
            if (was) OpenChanged?.Invoke(false);
        }

        private void LateUpdate()
        {
            if (!IsOpen) return;
            if (slot == null) { CloseImmediate(); return; }
            UpdatePosition();
        }

        private Camera Cam => input != null ? input.Cam : Camera.main;

        private void UpdatePosition()
        {
            if (slot == null || !UIRoot.WorldToLayer(layer, slot.transform.position, Cam, out Vector2 local)) return;

            Rect safe = UIRoot.SafeRectIn(layer);
            float m = RingRadius + ItemSize * 0.5f + 12f;
            local.x = ClampInside(local.x, safe.xMin + m, safe.xMax - m);
            local.y = ClampInside(local.y, safe.yMin + m + 24f, safe.yMax - m);
            root.anchoredPosition = local;

            if (tooltip.gameObject.activeSelf)
            {
                // Tooltip on the side with more room, kept inside the safe area vertically.
                float side = RingRadius + ItemSize * 0.5f + 24f + TooltipWidth * 0.5f;
                bool right = safe.xMax - local.x >= local.x - safe.xMin;
                float x = right ? side : -side;
                float half = tooltip.sizeDelta.y * 0.5f;
                float y = ClampInside(0f, safe.yMin - local.y + half + 10f, safe.yMax - local.y - half - 10f);
                tooltip.anchoredPosition = new Vector2(x, y);
            }
        }

        private static float ClampInside(float v, float min, float max) =>
            min > max ? (min + max) * 0.5f : Mathf.Clamp(v, min, max);

        // ------------------------------------------------------------------ ring contents

        private void BuildRing()
        {
            int n = towers != null ? towers.Count : 0;
            for (int i = 0; i < n; i++)
            {
                TowerData data = towers[i];
                if (data == null) continue;
                float angle = n == 1 ? 90f : 90f + 180f / n - i * 360f / n;
                Item item = AddItem(ItemKind.Build, angle, data.icon, Abbrev(data.towerName), UITheme.ForDamage(data.damageType),
                                    data.baseCost, data.icon != null);
                item.title = data.towerName;
                item.body = DescribeTower(data);
                item.previewRange = data.range;
                TowerData captured = data;
                item.execute = () =>
                {
                    TowerPlacement s = slot;
                    if (s != null && s.TryBuild(captured))
                    {
                        Close();
                        TowerBuilt?.Invoke(s, captured);
                    }
                };
            }
            pipsRoot.gameObject.SetActive(false);
        }

        private void UpgradeRing()
        {
            pipsRoot.gameObject.SetActive(tower != null);
            if (tower != null && tower.data != null)
            {
                TowerData d = tower.data;
                int level = tower.upgradeLevel;
                if (level == 1)
                {
                    Item up = AddItem(ItemKind.Upgrade, 90f, UISprites.Triangle, null, UITheme.Primary, tower.NextUpgradeCost(), false, 90f);
                    up.title = "Upgrade to level 2";
                    TowerStats next = tower.PreviewStats(2, Tower.UpgradePath.None);
                    up.body = Delta(next);
                    up.previewRange = next.Range;
                    up.execute = () => { if (tower != null && tower.Upgrade()) AfterChange(true); };
                }
                else if (level == 2 && tower.chosenPath == Tower.UpgradePath.None)
                {
                    TowerStats a = tower.PreviewStats(3, Tower.UpgradePath.PathA);
                    TowerStats b = tower.PreviewStats(3, Tower.UpgradePath.PathB);
                    AddBranch(Tower.UpgradePath.PathA, 135f, d.pathAName, Delta(a), a.Range);
                    AddBranch(Tower.UpgradePath.PathB, 45f, d.pathBName, Delta(b), b.Range);
                }
                else if (level == 3)
                {
                    Item fin = AddItem(ItemKind.Final, 90f, UISprites.Star, null, UITheme.Gold, tower.NextUpgradeCost(), false);
                    string branch = tower.chosenPath == Tower.UpgradePath.PathA ? d.pathAName : d.pathBName;
                    fin.title = "Master " + branch;
                    TowerStats max = tower.PreviewStats(4, tower.chosenPath);
                    fin.body = Delta(max);
                    fin.previewRange = max.Range;
                    fin.execute = () => { if (tower != null && tower.UpgradeFinal()) AfterChange(true); };
                }
                else
                {
                    Item max = AddItem(ItemKind.Max, 90f, null, "MAX", UITheme.Neutral, 0, false);
                    max.title = "Fully upgraded";
                    max.body = "This tower has reached its final tier.";
                    max.previewRange = tower.Range;
                    max.view.Interactable = false;
                }

                Item prio = AddItem(ItemKind.Priority, 0f, null, PriorityShort(tower.targetPriority), UITheme.Secondary, 0, false);
                prio.title = "Target: " + tower.targetPriority;
                prio.body = PriorityHelp(tower.targetPriority);
                prio.previewRange = tower.Range;
                prio.execute = CyclePriority;
            }

            int refund = tower != null ? tower.SellValue() : 0;
            Item sell = AddItem(ItemKind.Sell, -90f, UISprites.Coin, null, UITheme.Danger, 0, true, 0f, 0.62f);
            sell.title = "Sell tower";
            sell.body = "Refund " + refund + " gold.";
            sell.previewRange = tower != null ? tower.Range : 0f;
            sell.costLabel = UIFactory.CostTag(sell.view.transform, refund, 50f);
            sell.costLabel.text = "+" + refund;
            PlaceTag(sell.costLabel);
            sell.execute = () =>
            {
                TowerPlacement s = slot;
                if (s == null) return;
                Vector3 pos = s.transform.position;
                int gained = s.Sell();
                Close();
                if (gained > 0 && UIRoot.Instance != null) UIRoot.Instance.FloatText(pos, "+" + gained, UITheme.Gold, Cam);
                TowerChanged?.Invoke(s);
            };
        }

        private void AddBranch(Tower.UpgradePath path, float angle, string name, string body, float previewRange)
        {
            Item b = AddItem(ItemKind.Branch, angle, null, name, path == Tower.UpgradePath.PathA ? UITheme.Danger : UITheme.Secondary,
                             tower.BranchCost(path), false);
            b.title = name;
            b.body = body;
            b.previewRange = previewRange;
            b.execute = () => { if (tower != null && tower.ChooseBranch(path)) AfterChange(true); };
        }

        private Item AddItem(ItemKind kind, float angleDeg, Sprite icon, string text, Color color, int cost, bool whiteIcon,
                             float iconRotation = 0f, float iconScale = 0.55f)
        {
            int index = items.Count;
            UIButtonView view = UIFactory.IconButton(itemsRoot, icon, () => OnItemTapped(index), color, ItemSize, true, iconScale);
            float rad = angleDeg * Mathf.Deg2Rad;
            UIFactory.Center(view.Rect, new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * RingRadius, new Vector2(ItemSize, ItemSize));
            if (view.Icon != null)
            {
                if (whiteIcon) view.Icon.color = Color.white;
                view.Icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, iconRotation);
            }
            if (!string.IsNullOrEmpty(text))
                view.Label = UIFactory.FitLabel(view.Face.transform, text, text.Length <= 3 ? UITheme.FontLarge : UITheme.FontSmall,
                                                UITheme.Text, 14f);

            var item = new Item { kind = kind, view = view, cost = cost };
            if (cost > 0)
            {
                item.costLabel = UIFactory.CostTag(view.transform, cost, 50f);
                PlaceTag(item.costLabel);
            }

            // "Tap again to confirm" badge.
            Image badge = UIFactory.Image(view.transform, "Confirm", UISprites.Circle, UITheme.Primary);
            UIFactory.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-14f, -14f), new Vector2(58f, 58f));
            Image check = UIFactory.Image(badge.transform, "Check", UISprites.Check, Color.white);
            UIFactory.Center(check.rectTransform, Vector2.zero, new Vector2(40f, 40f));
            badge.gameObject.SetActive(false);
            item.checkBadge = badge;

            items.Add(item);
            return item;
        }

        private static void PlaceTag(Text costLabel)
        {
            var tag = (RectTransform)costLabel.transform.parent;
            UIFactory.Place(tag, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), tag.sizeDelta);
        }

        private void ClearItems()
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].view != null) Destroy(items[i].view.gameObject);
            items.Clear();
        }

        // ------------------------------------------------------------------ interaction

        private void OnItemTapped(int index)
        {
            if (!IsOpen || index < 0 || index >= items.Count) return;
            Item item = items[index];

            if (item.kind == ItemKind.Priority)
            {
                item.execute?.Invoke();
                Select(index);
                return;
            }
            if (item.kind == ItemKind.Max) { Select(index); return; }

            if (selected != index)
            {
                Select(index);
                return;
            }

            // Second tap on the selected item: confirm.
            if (item.cost > 0 && (gm == null || !gm.CanAfford(item.cost)))
            {
                UITween.Punch(item.view.transform, 0.12f, 0.25f);
                if (item.costLabel != null) UITween.Punch(item.costLabel.transform.parent, 0.35f, 0.3f);
                if (UIRoot.Instance != null) UIRoot.Instance.ShowToast("Not enough gold");
                return;
            }
            item.execute?.Invoke();
        }

        private void Select(int index)
        {
            selected = index;
            Item item = items[index];
            for (int i = 0; i < items.Count; i++)
            {
                bool on = i == index && items[i].kind != ItemKind.Priority && items[i].kind != ItemKind.Max &&
                          (items[i].cost <= 0 || gm == null || gm.CanAfford(items[i].cost));
                if (items[i].checkBadge != null && items[i].checkBadge.gameObject.activeSelf != on)
                {
                    items[i].checkBadge.gameObject.SetActive(on);
                    if (on) UITween.Scale(items[i].checkBadge.transform, Vector3.zero, Vector3.one, 0.2f, Ease.OutBack);
                }
            }

            tooltipTitle.text = item.title;
            tooltipBody.text = item.body;
            float bodyH = Mathf.Max(48f, tooltipBody.preferredHeight);
            tooltipBody.rectTransform.sizeDelta = new Vector2(TooltipWidth - 48f, bodyH);
            tooltip.sizeDelta = new Vector2(TooltipWidth, 74f + bodyH + 22f);
            if (!tooltip.gameObject.activeSelf)
            {
                tooltip.gameObject.SetActive(true);
                UITween.Scale(tooltip, Vector3.one * 0.85f, Vector3.one, 0.18f, Ease.OutBack);
            }
            UpdatePosition();

            if (slot != null && item.previewRange > 0f)
                rangeRing.ShowRange(slot.transform.position, item.previewRange, UITheme.RangeEdge);
        }

        private void CyclePriority()
        {
            if (tower == null) return;
            int count = Enum.GetValues(typeof(TargetPriority)).Length;
            tower.targetPriority = (TargetPriority)(((int)tower.targetPriority + 1) % count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].kind != ItemKind.Priority) continue;
                items[i].view.SetLabel(PriorityShort(tower.targetPriority));
                items[i].title = "Target: " + tower.targetPriority;
                items[i].body = PriorityHelp(tower.targetPriority);
            }
            TowerChanged?.Invoke(slot);
        }

        private void AfterChange(bool close)
        {
            TowerPlacement s = slot;
            if (close) Close();
            else Open(s);
            TowerChanged?.Invoke(s);
        }

        private void RefreshAffordability()
        {
            int gold = gm != null ? gm.Gold : int.MaxValue;
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                if (it.cost <= 0 || it.view == null) continue;
                bool ok = gold >= it.cost;
                it.view.Face.color = ok ? it.view.BaseColor : UITheme.Disabled;
                if (it.view.Icon != null) it.view.Icon.color = UITheme.WithAlpha(it.view.Icon.color, ok ? 1f : 0.45f);
                if (it.view.Label != null) it.view.Label.color = UITheme.WithAlpha(UITheme.Text, ok ? 1f : 0.5f);
                if (it.costLabel != null) it.costLabel.color = ok ? UITheme.Gold : UITheme.TextBad;
                if (i == selected && it.checkBadge != null && it.checkBadge.gameObject.activeSelf != ok)
                    it.checkBadge.gameObject.SetActive(ok);
            }
        }

        private void RefreshPips()
        {
            int level = tower != null ? tower.upgradeLevel : 0;
            for (int i = 0; i < pips.Length; i++) pips[i].color = i < level ? UITheme.Gold : UITheme.StarOff;
        }

        // ------------------------------------------------------------------ text helpers (built on open only)

        private static string Abbrev(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            string first = name.Trim();
            int space = first.IndexOf(' ');
            if (space > 0) first = first.Substring(0, space);
            return first.Length <= 7 ? first : first.Substring(0, 6) + ".";
        }

        private static string DescribeTower(TowerData d)
        {
            var sb = new System.Text.StringBuilder(160);
            if (!string.IsNullOrEmpty(d.description)) sb.Append(d.description).Append('\n');
            sb.Append("Damage ").Append(d.damage.ToString("0.#")).Append(" (").Append(d.damageType).Append(")\n");
            sb.Append("Range ").Append(d.range.ToString("0.#")).Append("   Rate ").Append(d.fireRate.ToString("0.##")).Append("/s");
            if (d.splashRadius > 0f) sb.Append("\nSplash damage");
            if (d.appliesSlow) sb.Append("\nSlows enemies");
            if (d.appliesPoison) sb.Append("\nPoisons enemies");
            if (!d.canTargetFlying) sb.Append("\nCannot hit flying");
            else if (!d.canTargetGround) sb.Append("\nAir targets only");
            return sb.ToString();
        }

        /// <summary>Upgrade preview from Tower.PreviewStats (the same math the upgrade applies), relative to current stats.</summary>
        private string Delta(TowerStats next)
        {
            string text = Mults(Ratio(next.Damage, tower.Damage), Ratio(next.Range, tower.Range), Ratio(next.FireRate, tower.FireRate));
            if (next.AppliesSlow && !tower.AppliesSlow) text += "\nAdds slow on hit";
            if (next.AppliesPoison && !tower.AppliesPoison) text += "\nAdds poison on hit";
            return text;
        }

        private static float Ratio(float next, float current) => current > 0f ? next / current : 1f;

        private static string Mults(float damage, float range, float rate)
        {
            var sb = new System.Text.StringBuilder(64);
            if (!Mathf.Approximately(damage, 1f)) sb.Append("Damage x").Append(damage.ToString("0.##")).Append('\n');
            if (!Mathf.Approximately(range, 1f)) sb.Append("Range x").Append(range.ToString("0.##")).Append('\n');
            if (!Mathf.Approximately(rate, 1f)) sb.Append("Fire rate x").Append(rate.ToString("0.##")).Append('\n');
            if (sb.Length == 0) sb.Append("Improved stats");
            else sb.Length -= 1;
            return sb.ToString();
        }

        private static string PriorityShort(TargetPriority p)
        {
            switch (p)
            {
                case TargetPriority.Last: return "Last";
                case TargetPriority.Strongest: return "Strong";
                case TargetPriority.Weakest: return "Weak";
                case TargetPriority.Closest: return "Close";
                default: return "First";
            }
        }

        private static string PriorityHelp(TargetPriority p)
        {
            switch (p)
            {
                case TargetPriority.Last: return "Shoots the enemy furthest from the exit.\nTap to change.";
                case TargetPriority.Strongest: return "Shoots the enemy with the most health.\nTap to change.";
                case TargetPriority.Weakest: return "Finishes off the weakest enemy.\nTap to change.";
                case TargetPriority.Closest: return "Shoots the nearest enemy.\nTap to change.";
                default: return "Shoots the enemy closest to the exit.\nTap to change.";
            }
        }
    }
}
